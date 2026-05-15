using DM.Foundation.Logging.Interfaces;
using DM.Foundation.Motion.Interfaces;
using DM.Foundation.Shared.Constants;
using DM.Foundation.Shared.Events;
using DM.Foundation.Shared.Models;
using ONet.FAU.Tx._16_128.Extension.Model;
using ONet.FAU.Tx16_128.Extension.Model;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace ONet.FAU.Tx16_128.Extension.Common
{
    public class CouplingController
    {
        public async Task<Result1D> Run1DFullRangeAsync(IMotionControl motion, Parameter1D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService, int chartID)
        {

            Result1D result = new Result1D();
            List<CouplingData> couplingData = new List<CouplingData>();

            try
            {
                double StartPos = motion.GetPulsePosition();//获取当前位置

                double Max = StartPos + (para.Range / 2);//耦合位置最大值

                double Min = StartPos - (para.Range / 2);//耦合位置最小值

                if(para.Axisgroup == AxisGroup.Right && para.AxisName == MotionAxisNames.RightX)
                {
                    await motion.MoveAbsAsync(Max, 3, token);
                }
                else
                {
                    await motion.MoveAbsAsync(Min, 3, token);
                 
                }

                await Task.Delay(500);

                couplingData.Clear();//清除位置和功率记录列表

                eventAggregator.GetEvent<Event_Message>().Publish($"选择组：{(int)para.SelectedGroup}");

                couplingData.Add(await GetAdcAsync(motion, opticalModuleService, para, logger));

                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        motion.Stop(0);
                        result.Success = false;
                        await Task.Delay(200);

                        await motion.MoveAbsAsync(StartPos, 3, token);
                        eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:取消耦合。");
                        return result;
                    }

                    //判断当前位置是否超出最小位置限位
                    if (motion.GetPulsePosition() >= Max+0.001 || motion.GetPulsePosition() <= Min-0.001)
                    {
                        eventAggregator.GetEvent<Event_Message>().Publish(
                            $"{toolParameter.UserDefined}:1D耦合,超出耦合范围，退出耦合。" +
                            $"轴当前位置{motion.GetPulsePosition()}" +
                            $"耦合范围:Max:{Max},Min:{Min}");
                        break;
                    }

                    #region 轴移动->等待停止->延时
                    if(para.Axisgroup == AxisGroup.Right && para.AxisName == MotionAxisNames.RightX)
                    {
                        await motion.MoveRelAsync(para.StepDist, false, para.AxisVel, token);
                    }
                    else
                    {
                        await motion.MoveRelAsync(para.StepDist, true, para.AxisVel, token);
                    }
                   

                    await Task.Delay(para.DataDelay);
                    #endregion

                    couplingData.Add(await GetAdcAsync(motion, opticalModuleService, para, logger));

                    ShowData(couplingData, para, eventAggregator, chartID);
                }



                var flatRes = ExtractFlatPointsByChannel(couplingData);

                var bestpos = FindBestPos(flatRes);

                //CouplingData maxAdcItem = couplingData.OrderByDescending(x => x.ADC).First();

                await motion.MoveAbsAsync(bestpos, 2, token);

                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:耦合完成，当前位置:{bestpos}");

                result.Success = true;

                result.CouplingData = couplingData;

                if (toolParameter.IsSaveData)
                {
                    if (couplingData != null)
                    {
                        DataSaveHelper.ExportToCsv(couplingData, logger, $"{toolParameter.UserDefined}-{para.AxisName}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:1D耦合:{ex.ToString()}");
                logger.Error(ex.ToString());
                result.Success = false;
                return result;
            }


        }
        public async Task<Result1D> Run1DPartialRangeAsync(IMotionControl motion, Parameter1D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService, int chartID)
        {
            Result1D result = new Result1D();
            List<CouplingData> couplingData = new List<CouplingData>();

            try
            {
                double startPos = motion.GetPulsePosition();

            
                //await Task.Delay(500);

                couplingData.Clear();
                couplingData.Add(await GetAdcAsync(motion, opticalModuleService, para, logger));

                //正向扫描
                await SweepOneDirectionAsync(+1, motion, para, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, couplingData, startPos);

                //回到起始位置
                await motion.MoveAbsAsync(startPos, 3, token);
                await Task.Delay(500);
                //反向扫描
                await SweepOneDirectionAsync(-1, motion, para, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, couplingData, startPos);

                // 找最佳位置
                var flatRes = ExtractFlatPointsByChannel(couplingData);
                var bestpos = FindBestPos(flatRes);

                await motion.MoveAbsAsync(bestpos, 2, token);
                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:耦合完成，当前位置:{bestpos}");

                result.Success = true;
                result.CouplingData = couplingData;

                if (toolParameter.IsSaveData && couplingData != null)
                    DataSaveHelper.ExportToCsv(couplingData, logger, $"{toolParameter.UserDefined}-{para.AxisName}");

                return result;


            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:1D耦合:{ex}");
                logger.Error(ex.ToString());
                result.Success = false;
                return result;
            }
        }


        public async Task<Result2D> Run2DFullRangeAsync(IMotionControl motionX, IMotionControl motionY, Parameter2D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService, int chartID)
        {

            Result2D result = new Result2D();


            try
            {

                para.XParameter.Axisgroup =para.Axisgroup;
                para.YParameter.Axisgroup = para.Axisgroup;


                var resultA = await Run1DFullRangeAsync(motionX, para.XParameter, token, eventAggregator, toolParameter, logger,  opticalModuleService, chartID);

                await Task.Delay(200);

                var resultB = await Run1DFullRangeAsync(motionY, para.YParameter, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID);


                result.XResult = resultA;
                result.YResult = resultB;


                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:1D耦合:{ex.ToString()}");
                logger.Error(ex.ToString());
                result.Success = false;
                return result;
            }


        }



        public async Task<bool> RunFACouplingCorrectionAsync(IMotionSystemService motionSystem, IMotionControl motionX, IMotionControl motionY, Parameter2D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService , int chartID, string Text)
        {
            try
            {
                Result2D result = new Result2D();

                para.XParameter.Axisgroup = para.Axisgroup;

                para.YParameter.Axisgroup = para.Axisgroup;

                for (int i = 0; i < para.LoopCountt; i++)
                {
                    var CH_1_X = await Run1DCouplingCorrectionAsync(motionX, para.XParameter, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, CorrectChannel.Channle_1);

                    if(!CH_1_X.Success) return false;

                    await Task.Delay(200);

                    var CH_1_Y = await Run1DCouplingCorrectionAsync(motionY, para.YParameter, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, CorrectChannel.Channle_1);
                    
                    if (!CH_1_Y.Success) return false;

                    await Task.Delay(200);

                    var CH_8_X = await Run1DCouplingCorrectionAsync(motionX, para.XParameter, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, CorrectChannel.Channle_8);

                    if (!CH_8_X.Success) return false;

                    await Task.Delay(200);

                    var CH_8_Y = await Run1DCouplingCorrectionAsync(motionY, para.YParameter, token, eventAggregator, toolParameter, logger, opticalModuleService, chartID, CorrectChannel.Channle_8);

                    if (!CH_8_Y.Success) return false;



                     
                    eventAggregator.GetEvent<Event_Message>().Publish($"CH1_X:{CH_1_X.BestPos},CH8_X:{CH_8_X.BestPos}");

                    //计算矫正角度
                    double deltaX = CH_8_X.BestPos - CH_1_X.BestPos;

                    double angle = Math.Atan2(deltaX, 1.5) * (180 / Math.PI);

                    eventAggregator.GetEvent<Event_Message>().Publish($"{Text}:耦合角度:{angle.ToString("F4")}");

                    if (Math.Abs(angle) <= 0.01) return true;

                    if (Math.Abs(angle) < 1)
                    {
                        if (para.Axisgroup == AxisGroup.Left)
                        {
                            angle = (angle > 0) ? -angle : Math.Abs(angle);
                            eventAggregator.GetEvent<Event_Message>().Publish($"{Text}角度调整:{angle}");
                            var axis = motionSystem.GetAxis(MotionAxisNames.LeftRZ);
                            await axis.MoveRelAsync(angle, 1, token);
                        }
                        else
                        {
                            angle = (angle > 0) ? -angle : Math.Abs(angle);
                            eventAggregator.GetEvent<Event_Message>().Publish($"{Text}角度调整:{angle}");
                            var axis = motionSystem.GetAxis(MotionAxisNames.RightRZ);
                            await axis.MoveRelAsync(angle, 1, token);
                        }
                    }
                    else
                    {
                        eventAggregator.GetEvent<Event_Message>().Publish($"FA角度大于设定阈值，请检查。");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {

                logger.Error(ex.ToString());

                return false;
            }
        }
        public async Task<Result1D> Run1DCouplingCorrectionAsync(IMotionControl motion, Parameter1D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService, int chartID, CorrectChannel correctChannel)
        {

            Result1D result = new Result1D();
            List<CouplingData> couplingData = new List<CouplingData>();

            try
            {
                //if(para.AxisName == MotionAxisNames.RightX)return new Result1D() { BestPos = motion.GetPulsePosition(), Success=false };

                double StartPos = motion.GetPulsePosition();//获取当前位置

                double Max = StartPos + (para.Range / 2);//耦合位置最大值

                double Min = StartPos - (para.Range / 2);//耦合位置最小值

              //  await motion.MoveAbsAsync(Min, 3, token);
                if (para.Axisgroup == AxisGroup.Right && para.AxisName == MotionAxisNames.RightX)
                {
                    await motion.MoveAbsAsync(Max, 3, token);
                }
                else
                {
                    await motion.MoveAbsAsync(Min, 3, token);

                }

                await Task.Delay(500);

                couplingData.Clear();//清除位置和功率记录列表

                couplingData.Add(await GetAdc_CorrectionAsync(motion, opticalModuleService, para, logger, correctChannel));

                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        motion.Stop(0);
                        result.Success = false;
                        await Task.Delay(200);

                        await motion.MoveAbsAsync(StartPos, 3, token);
                        eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:取消耦合。");
                        return result;
                    }

                    //判断当前位置是否超出最小位置限位
                    //if (motion.GetPulsePosition() > Max)
                   if( motion.GetPulsePosition() >= Max + 0.001 || motion.GetPulsePosition() <= Min - 0.001)
                    {
                        eventAggregator.GetEvent<Event_Message>().Publish(
                            $"{toolParameter.UserDefined}:1D耦合,超过最大限位位置，退出耦合。" +
                            $"轴当前位置{motion.GetPulsePosition()}" +
                            $"最大限位:{Max}");
                        break;
                    }

                    #region 轴移动->等待停止->延时
                   // await motion.MoveRelAsync(para.StepDist, true, para.AxisVel, token);
                    if (para.Axisgroup == AxisGroup.Right && para.AxisName == MotionAxisNames.RightX)
                    {
                        await motion.MoveRelAsync(para.StepDist, false, para.AxisVel, token);
                    }
                    else
                    {
                        await motion.MoveRelAsync(para.StepDist, true, para.AxisVel, token);
                    }



                    await Task.Delay(para.DataDelay);
                    #endregion

                    couplingData.Add(await GetAdc_CorrectionAsync(motion, opticalModuleService, para, logger, correctChannel));

                    ShowDataSingle(couplingData, para, eventAggregator, chartID);
                }





                CouplingData bestpos = couplingData.OrderByDescending(x => x.Values[0]).First();

                await motion.MoveAbsAsync(bestpos.Pos, 2, token);

                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:耦合完成，当前位置:{bestpos.Pos}");

                result.Success = true;
                result.BestPos = bestpos.Pos;

                result.CouplingData = couplingData;

                if (toolParameter.IsSaveData)
                {
                    if (couplingData != null)
                    {
                        DataSaveHelper.ExportToCsv(couplingData, logger, $"{toolParameter.UserDefined}-{para.AxisName}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:1D耦合:{ex.ToString()}");
                logger.Error(ex.ToString());
                result.Success = false;
                return result;
            }


        }


        private async Task SweepOneDirectionAsync(int dir, IMotionControl motion, Parameter1D para, CancellationToken token, IEventAggregator eventAggregator, ToolParameter toolParameter, ILogger logger, OpticalModuleService opticalModuleService, int chartID, List<CouplingData> couplingData, double startPos, double epsilon = 0.02)
        {
            int downCount = 0;
            double? lastScore = null;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    motion.Stop(0);
                    await Task.Delay(200);
                    await motion.MoveAbsAsync(startPos, 3, token);
                    eventAggregator.GetEvent<Event_Message>().Publish($"{toolParameter.UserDefined}:取消耦合。");
                    return;
                }

                // 走一步
                await motion.MoveRelAsync(dir * para.StepDist, true, para.AxisVel, token);
                await Task.Delay(para.DataDelay);

                // 采集
                var data =await GetAdcAsync(motion, opticalModuleService, para, logger);
                couplingData.Add(data);
                ShowData(couplingData, para, eventAggregator, chartID);

                // 评分
                double score = ComputeScore(data);

                // 下降阈值判断
                if (lastScore.HasValue && score < lastScore.Value - epsilon)
                    downCount++;
                else
                    downCount = 0;

                if (downCount >= 3)
                    break;

                lastScore = score;
            }
        }



        private async Task<CouplingData> GetAdcAsync(IMotionControl motion, OpticalModuleService opticalModuleService, Parameter1D para, ILogger logger)
        {
            try
            {
               
                // 获取 A、B 两台设备的所有功率数据
                var datas = await opticalModuleService.ReadRSSIAsync((int)para.SelectedGroup, 8);
                double[] result = new double[8]; // 默认全为0


                if (para.CouplingDataBit == Converters.CouplingDataBit.FirstFour)
                {
                    Array.Copy(datas, 0, result, 0, 4);
                }
                else
                {
                    Array.Copy(datas, 4, result, 4, 4);
                }


                if (datas == null || datas.Count() == 0)
                {
                    logger?.Warn("AddCurrentData_1D: GetAllPower 返回空数据");
                    return null;
                }

                var results = CouplingData.CreateEight(motion.GetPulsePositionEx(), result.Select(x => (double)x).ToList());

                return results;

              
            }
            catch (Exception ex)
            {
                logger?.Error(ex.ToString(), "AddCurrentData_1D: 获取耦合数据失败");
                return null;
            }

        }
        private async Task<CouplingData> GetAdc_CorrectionAsync(IMotionControl motion, OpticalModuleService opticalModuleService, Parameter1D para, ILogger logger, CorrectChannel correctChannel)
        {
            try
            {

                // 获取 A、B 两台设备的所有功率数据
                var datas = await opticalModuleService.ReadRSSIAsync((int)para.SelectedGroup, 8);
                double[] result = new double[8]; // 默认全为0


                if (para.CouplingDataBit == Converters.CouplingDataBit.FirstFour)
                {
                    Array.Copy(datas, 0, result, 0, 4);
                }
                else
                {
                    Array.Copy(datas, 4, result, 4, 4);
                }


                if (datas == null || datas.Count() == 0)
                {
                    logger?.Warn("AddCurrentData_1D: GetAllPower 返回空数据");
                    return null;
                }


                double ResultData = 0;
                if(para.CouplingDataBit == Converters.CouplingDataBit.FirstFour)
                {
                    if (correctChannel == CorrectChannel.Channle_1)
                    {
                        //double.TryParse(result[0], out ResultData);

                        ResultData = result[0];
                    }
                    else if (correctChannel == CorrectChannel.Channle_8)
                    {
                        //double.TryParse(result[3], out ResultData);
                        ResultData = result[3];
                    }
                }
                else
                {
                    if (correctChannel == CorrectChannel.Channle_1)
                    {
                        //double.TryParse(result[0], out ResultData);

                        ResultData = result[4];
                    }
                    else if (correctChannel == CorrectChannel.Channle_8)
                    {
                        //double.TryParse(result[3], out ResultData);
                        ResultData = result[7];
                    }
                }
               


                var results = CouplingData.CreateSingle(motion.GetPulsePositionEx(), ResultData);


            

                return results;


            }
            catch (Exception ex)
            {
                logger?.Error(ex.ToString(), "AddCurrentData_1D: 获取耦合数据失败");
                return null;
            }

        }

        private void ShowData(List<CouplingData> couplingDatas, Parameter1D para, IEventAggregator eventAggregator, int chartID)
        {
            PlotDataMessage plotDataMessage = new PlotDataMessage()
            {
                ChartId = chartID,
                Label = para.AxisName,
                Xs = new List<double>(),       // 横轴数据
                Ys = new List<double>(), // 每个 double[8] 对应一个 X 点的 8 通道数据
                MultiChannel = new List<List<double>>()
            };

            plotDataMessage.Xs.AddRange(couplingDatas.Select(P => (double)P.Pos).ToList());


            plotDataMessage.MultiChannel.AddRange(couplingDatas.Select(P => P.Values));

            eventAggregator.GetEvent<MultiChannelPlotDataEvetn>().Publish(plotDataMessage);


        }

        private void ShowDataSingle(List<CouplingData> couplingDatas, Parameter1D para, IEventAggregator eventAggregator, int chartID)
        {
            PlotDataMessage plotDataMessage = new PlotDataMessage()
            {
                ChartId = chartID,
                Label = para.AxisName,
                Xs = new List<double>(),       // 横轴数据
                Ys = new List<double>(), // 每个 double[8] 对应一个 X 点的 8 通道数据
                MultiChannel = new List<List<double>>()
            };

            plotDataMessage.Xs.AddRange(couplingDatas.Select(P => (double)P.Pos).ToList());

            plotDataMessage.Ys.AddRange(couplingDatas.Select(P => P.Values[0]).ToList());

            eventAggregator.GetEvent<PlotDataEvent>().Publish(plotDataMessage);
        }

        public static Dictionary<int, List<FlatAreaData>> ExtractFlatPointsByChannel(List<CouplingData> data, double ratio = 0.8)
        {
            var result = new Dictionary<int, List<FlatAreaData>>();
            if (data == null || data.Count == 0) return result;

            int maxCh = data.Max(d => d.ChannelCount);

            for (int ch = 0; ch < maxCh; ch++)
            {
                var series = data
                    .Where(d => d.Values.Count > ch)
                    .Select(d => new { d.Pos, Value = d.Values[ch] })
                    .ToList();

                if (series.Count == 0)
                {
                    result[ch] = new List<FlatAreaData>();
                    continue;
                }

                double max = series.Max(p => p.Value);
                double min = series.Min(p => p.Value);
                double range = max - min;

                double threshold = min + range * ratio;

                var list = new List<FlatAreaData>();
                foreach (var p in series)
                {
                    if (p.Value >= threshold)
                        list.Add(new FlatAreaData(p.Pos, p.Value, ch));
                }

                result[ch] = list;
            }

            return result;
        }

        /// <summary>
        /// 查找最佳位置
        /// </summary>
        /// <param name="flatRes"></param>
        /// <param name="roundDigits"></param>
        /// <returns></returns>
        public static double FindBestPos(Dictionary<int, List<FlatAreaData>> flatRes, int roundDigits = 4)
        {
            if (flatRes == null || flatRes.Count == 0) return double.NaN;

            // 合并所有平坦区点
            var all = flatRes.SelectMany(kv => kv.Value).ToList();
            if (all.Count == 0) return double.NaN;

            // 按位置分组（四舍五入）
            var groups = all
                .GroupBy(p => Math.Round(p.Pos, roundDigits))
                .Select(g => new
                {
                    Pos = g.Key,
                    ChannelCount = g.Select(x => x.Channel).Distinct().Count(),
                    AvgValue = g.Average(x => x.Value)
                })
                .ToList();

            // 先看通道数最多，再看平均值最高
            var best = groups
                .OrderByDescending(g => g.ChannelCount)
                .ThenByDescending(g => g.AvgValue)
                .First();

            return best.Pos;
        }

        /// <summary>
        /// 计算得分
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private double ComputeScore(CouplingData d)
        {
            if (d.Values == null || d.Values.Count == 0) return double.NegativeInfinity;
            return d.Values.Average();
        }

    }
}
