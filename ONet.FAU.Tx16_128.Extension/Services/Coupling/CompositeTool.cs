using DM.Foundation.DataBinding.Interfaces;
using DM.Foundation.Logging.Interfaces;
using DM.Foundation.Motion.Interfaces;
using DM.Foundation.Shared.Attributes;
using DM.Foundation.Shared.Constants;
using DM.Foundation.Shared.Enums;
using DM.Foundation.Shared.Events;
using DM.Foundation.Shared.Interfaces;
using DM.Foundation.Shared.Models;
using DM.Vision.Services;
using Newtonsoft.Json.Linq;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ONet.FAU.Tx16_128.Extension.Services.Coupling
{
    [ToolVersion("1.0")]
    public class CompositeTool : BindableBase, IToolBase, IToolMigratable
    {
        private ToolParameter _parameter;
        public ToolParameter Parameter { get { return _parameter; } set { _parameter = value; RaisePropertyChanged(); } }

        private string _taskname;
        public string TaskName
        {
            get { return _taskname; }
            set { _taskname = value; RaisePropertyChanged(); }
        }


        private string _taskpara;
        public string TaskPara
        {
            get { return _taskpara; }
            set { _taskpara = value; RaisePropertyChanged(); }
        }


        public CompositeTool()
        {
            Parameter = new ToolParameter()
            {
                ToolGroupName = "ONetTool",
                ToolName = "CompositeTool",
                ViewName = "CompositeToolView",
                CompletionFlag = DMColor.Gray,
                ExecutionFlag = DMColor.Gray
            };

        }


        public async Task<bool> ExecuteAsync(CancellationToken token, IEventAggregator eventAggregator, ToolExecutionContext context)
        {
            try
            {
                var motionsystem = context.Get<IMotionSystemService>("IMotionSystemService");//电机控制相关服务
                var databinding = context.Get<IDataBindingContext>("DataBindingContext");//数据绑定容器
                var runtiem = context.Get<IRuntimeContext>("IRuntimeContext");//软件运行过程中更新全局数据
                var logger = context.Get<ILogger>("ILogger");
                var calibration = context.Get<CalibrationServices>("CalibrationServices");


                eventAggregator.GetEvent<Event_Message>().Publish($"{Parameter.UserDefined}:Sub:{TaskName}。");

                bool Result = false;


                switch (TaskName)
                {
                    case "点胶位置计算":

                        Result = await CalculationOfDispensingPositionAnsync(motionsystem, databinding, logger, calibration, TaskPara);

                        break;

                    default:
                        eventAggregator.GetEvent<Event_Message>().Publish($"{Parameter.UserDefined}:执行参数异常，请检查。");
                        Result = false;
                        break;
                }

                return Result;
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish(ex.ToString());
                return false;
            }
        }

        public JObject Migrate(JObject sourceData, string fromVersion, string toVersion)
        {
            return null;
        }


        /// <summary>
        /// 点胶位置计算
        /// </summary>
        private async Task<bool> CalculationOfDispensingPositionAnsync(IMotionSystemService motionSystem, IDataBindingContext dataBinding, ILogger logger, CalibrationServices calibration, string taskPara)
        {
            try
            {
                //var axis_Lx = motionSystem.GetAxis(MotionAxisNames.LeftX);
                //var axis_Ly = motionSystem.GetAxis(MotionAxisNames.LeftY);

                //var axis_Rx = motionSystem.GetAxis(MotionAxisNames.RightX);
                //var axis_Ry = motionSystem.GetAxis(MotionAxisNames.RightY);

                var LX_P1_offset = dataBinding.Get("点胶补偿", "Lx_P1").ToDouble();
                var LY_P1_offset = dataBinding.Get("点胶补偿", "Ly_P1").ToDouble();

                var LX_P2_offset = dataBinding.Get("点胶补偿", "Lx_P2").ToDouble();
                var LY_P2_offset = dataBinding.Get("点胶补偿", "Ly_P2").ToDouble();

                var RX_P1_offset = dataBinding.Get("点胶补偿", "Rx_P1").ToDouble();
                var RY_P1_offset = dataBinding.Get("点胶补偿", "Ry_P1").ToDouble();

                var RX_P2_offset = dataBinding.Get("点胶补偿", "Rx_P2").ToDouble();
                var RY_P2_offset = dataBinding.Get("点胶补偿", "Ry_P2").ToDouble();


                var BaseX = dataBinding.Get("点胶校准", "BaseX").ToDouble();
                var BaseY = dataBinding.Get("点胶校准", "BaseY").ToDouble();
                var BaseZ = dataBinding.Get("点胶校准", "BaseZ").ToDouble();

                var NewX = dataBinding.Get("点胶校准", "NewX").ToDouble();
                var NewY = dataBinding.Get("点胶校准", "NewY").ToDouble();
                var NewZ = dataBinding.Get("点胶校准", "NewZ").ToDouble();

                double GxCalib = NewX - BaseX;
                double GyCalib = NewY - BaseY;
                double GlueZCalib = NewZ - BaseZ;

                var LxPos = motionSystem.GetAxis(MotionAxisNames.LeftX).GetPulsePosition();
                var LyPos = motionSystem.GetAxis(MotionAxisNames.LeftY).GetPulsePosition();

                var RxPos = motionSystem.GetAxis(MotionAxisNames.RightX).GetPulsePosition();
                var RyPos = motionSystem.GetAxis(MotionAxisNames.RightY).GetPulsePosition();

                string path_R, path_L;



                double resRx_P1, resRy_P1;
                double resRx_P2, resRy_P2;


                double resLx_P1, resLy_P1;
                double resLx_P2, resLy_P2;

                float GR_X, GR_Y;
                float GL_X, GL_Y;

                ParameterModel resModel;


                switch (taskPara)
                {
                    case "G1:G6":

                        path_L = "D:\\MyApp\\CalibrationFile\\Glue_Left_6.xml";
                        calibration.AffineTransformation(path_L, "Glue_Left_6", (float)LxPos, (float)LyPos, out GL_X, out GL_Y);

                        resLx_P1 = GL_X + LX_P1_offset + GxCalib;
                        resLy_P1 = GL_Y + LY_P1_offset + GyCalib;

                        resLx_P2 = GL_X + LX_P2_offset + GxCalib;
                        resLy_P2 = GL_Y + LY_P2_offset + GyCalib;


                        path_R = "D:\\MyApp\\CalibrationFile\\Glue_Right_1.xml";
                        calibration.AffineTransformation(path_R, "Glue_Right_1", (float)RxPos, (float)RyPos, out  GR_X, out  GR_Y);

                        resRx_P1 = GR_X + RX_P1_offset + GxCalib;
                        resRy_P1 = GR_Y + RY_P1_offset + GyCalib;

                        resRx_P2 = GR_X + RX_P2_offset + GxCalib;
                        resRy_P2 = GR_Y + RY_P2_offset + GyCalib;



                        resModel = new ParameterModel()
                        {
                            Name = Parameter.UserDefined,
                            IsRoot = true,
                            IsAddDataBind = true,
                            Children = new ObservableCollection<ParameterModel>
                            {
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P1",   resLx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P1",   resLy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P2",   resLx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P2",   resLy_P2.ToString("F4"), ParameterType.Double),

                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P1",   resRx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P1",   resRy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P2",   resRx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P2",   resRy_P2.ToString("F4"), ParameterType.Double),
                            }
                        };

                        dataBinding.SetModel(resModel);

                        break;





                    case "G2:G5":

                        path_L = "D:\\MyApp\\CalibrationFile\\Glue_Left_5.xml";
                        calibration.AffineTransformation(path_L, "Glue_Left_5", (float)LxPos, (float)LyPos, out GL_X, out GL_Y);

                        resLx_P1 = GL_X + LX_P1_offset + GxCalib;
                        resLy_P1 = GL_Y + LY_P1_offset + GyCalib;

                        resLx_P2 = GL_X + LX_P2_offset + GxCalib;
                        resLy_P2 = GL_Y + LY_P2_offset + GyCalib;


                        path_R = "D:\\MyApp\\CalibrationFile\\Glue_Right_2.xml";
                        calibration.AffineTransformation(path_R, "Glue_Right_2", (float)RxPos, (float)RyPos, out GR_X, out GR_Y);

                        resRx_P1 = GR_X + RX_P1_offset + GxCalib;
                        resRy_P1 = GR_Y + RY_P1_offset + GyCalib;

                        resRx_P2 = GR_X + RX_P2_offset + GxCalib;
                        resRy_P2 = GR_Y + RY_P2_offset + GyCalib;



                        resModel = new ParameterModel()
                        {
                            Name = Parameter.UserDefined,
                            IsRoot = true,
                            IsAddDataBind = true,
                            Children = new ObservableCollection<ParameterModel>
                            {
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P1",   resLx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P1",   resLy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P2",   resLx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P2",   resLy_P2.ToString("F4"), ParameterType.Double),

                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P1",   resRx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P1",   resRy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P2",   resRx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P2",   resRy_P2.ToString("F4"), ParameterType.Double),
                            }
                        };

                        dataBinding.SetModel(resModel);

                        break;


                    case "G3:G8":

                        path_L = "D:\\MyApp\\CalibrationFile\\Glue_Left_8.xml";
                        calibration.AffineTransformation(path_L, "Glue_Left_8", (float)LxPos, (float)LyPos, out GL_X, out GL_Y);

                        resLx_P1 = GL_X + LX_P1_offset + GxCalib;
                        resLy_P1 = GL_Y + LY_P1_offset + GyCalib;

                        resLx_P2 = GL_X + LX_P2_offset + GxCalib;
                        resLy_P2 = GL_Y + LY_P2_offset + GyCalib;


                        path_R = "D:\\MyApp\\CalibrationFile\\Glue_Right_3.xml";
                        calibration.AffineTransformation(path_R, "Glue_Right_3", (float)RxPos, (float)RyPos, out GR_X, out GR_Y);

                        resRx_P1 = GR_X + RX_P1_offset + GxCalib;
                        resRy_P1 = GR_Y + RY_P1_offset + GyCalib;

                        resRx_P2 = GR_X + RX_P2_offset + GxCalib;
                        resRy_P2 = GR_Y + RY_P2_offset + GyCalib;



                        resModel = new ParameterModel()
                        {
                            Name = Parameter.UserDefined,
                            IsRoot = true,
                            IsAddDataBind = true,
                            Children = new ObservableCollection<ParameterModel>
                            {
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P1",   resLx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P1",   resLy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P2",   resLx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P2",   resLy_P2.ToString("F4"), ParameterType.Double),

                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P1",   resRx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P1",   resRy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P2",   resRx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P2",   resRy_P2.ToString("F4"), ParameterType.Double),
                            }
                        };

                        dataBinding.SetModel(resModel);

                        break;


                    case "G4:G7":

                        path_L = "D:\\MyApp\\CalibrationFile\\Glue_Left_7.xml";
                        calibration.AffineTransformation(path_L, "Glue_Left_7", (float)LxPos, (float)LyPos, out GL_X, out GL_Y);

                        resLx_P1 = GL_X + LX_P1_offset + GxCalib;
                        resLy_P1 = GL_Y + LY_P1_offset + GyCalib;

                        resLx_P2 = GL_X + LX_P2_offset + GxCalib;
                        resLy_P2 = GL_Y + LY_P2_offset + GyCalib;


                        path_R = "D:\\MyApp\\CalibrationFile\\Glue_Right_4.xml";
                        calibration.AffineTransformation(path_R, "Glue_Right_4", (float)RxPos, (float)RyPos, out GR_X, out GR_Y);

                        resRx_P1 = GR_X + RX_P1_offset + GxCalib;
                        resRy_P1 = GR_Y + RY_P1_offset + GyCalib;

                        resRx_P2 = GR_X + RX_P2_offset + GxCalib;
                        resRy_P2 = GR_Y + RY_P2_offset + GyCalib;


                        resModel = new ParameterModel()
                        {
                            Name = Parameter.UserDefined,
                            IsRoot = true,
                            IsAddDataBind = true,
                            Children = new ObservableCollection<ParameterModel>
                            {
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P1",   resLx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P1",   resLy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Lx_P2",   resLx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ly_P2",   resLy_P2.ToString("F4"), ParameterType.Double),

                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P1",   resRx_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P1",   resRy_P1.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Rx_P2",   resRx_P2.ToString("F4"), ParameterType.Double),
                                 ParameterModel.Create(Parameter.UserDefined,"Ry_P2",   resRy_P2.ToString("F4"), ParameterType.Double),
                            }
                        };

                        dataBinding.SetModel(resModel);

                        break;



                }

                await Task.Delay(10);

                return true;
            }
            catch (Exception ex)
            {
                logger?.Error(ex.ToString());
                return false;
            }

        }









    }
}
