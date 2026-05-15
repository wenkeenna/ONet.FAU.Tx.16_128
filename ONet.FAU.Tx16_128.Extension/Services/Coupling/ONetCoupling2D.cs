using DM.Foundation.DataBinding.Interfaces;
using DM.Foundation.Logging.Interfaces;
using DM.Foundation.Motion.Interfaces;
using DM.Foundation.Shared.Attributes;
using DM.Foundation.Shared.Events;
using DM.Foundation.Shared.Interfaces;
using DM.Foundation.Shared.Models;
using Newtonsoft.Json.Linq;
using ONet.FAU.Tx._16_128.Extension.Model;
using ONet.FAU.Tx16_128.Extension.Common;
using ONet.FAU.Tx16_128.Extension.Converters;
using ONet.FAU.Tx16_128.Extension.Enums;
using ONet.FAU.Tx16_128.Extension.Events;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ONet.FAU.Tx16_128.Extension.Services.Coupling
{
    [ToolVersion("1.0")]
    public class ONetCoupling2D : BindableBase, IToolBase, IToolMigratable
    {
        private ToolParameter _parameter;
        public ToolParameter Parameter { get { return _parameter; } set { _parameter = value; RaisePropertyChanged(); } }


        private Parameter2D _leftpara;
        public Parameter2D LeftPara
        {
            get { return _leftpara; }
            set { _leftpara = value; RaisePropertyChanged(); }
        }


        private Parameter2D _rightpara;
        public Parameter2D RightPara
        {
            get { return _rightpara; }
            set { _rightpara = value; RaisePropertyChanged(); }
        }

        private const string LD9204S_B = "LD9204S_B";
        private const string LD9204S_A = "LD9204S_A";

        private const string STATE_ON = "ON";
        private const string STATE_OFF = "OFF";

        private IContainerProvider _containerProvider;
      

        private OpticalModuleService _opticalModuleService;

        private int _axisvelocity;
        public int AxisVel
        {
            get { return _axisvelocity; }
            set
            {
                if (value > 5)
                {
                    _axisvelocity = 5;
                }
                else
                {
                    _axisvelocity = value;
                }

                RaisePropertyChanged();
            }
        }



        private int _datadealy;
        public int DataDelay { get { return _datadealy; } set { _datadealy = value; RaisePropertyChanged(); } }




        private ChannelGroup _selectedGroupA = ChannelGroup.None;
        public ChannelGroup SelectedGroupA
        {
            get => _selectedGroupA;
            set
            {
                if (value != ChannelGroup.None && value == SelectedGroupB)
                {
                    RaisePropertyChanged(nameof(SelectedGroupA)); // 让 UI 回弹
                    return;
                }

                SetProperty(ref _selectedGroupA, value);
            }
        }

        private ChannelGroup _selectedGroupB = ChannelGroup.None;
        public ChannelGroup SelectedGroupB
        {
            get => _selectedGroupB;
            set
            {
                if (value != ChannelGroup.None && value == SelectedGroupA)
                {
                    RaisePropertyChanged(nameof(SelectedGroupB)); // 让 UI 回弹
                    return;
                }

                SetProperty(ref _selectedGroupB, value);
            }
        }



        private CouplingDataBit _couplingdatabitA;
        public CouplingDataBit CouplingDataBitA
        {
            get => _couplingdatabitA;
            set { _couplingdatabitA = value; RaisePropertyChanged(); }
        }


        private CouplingDataBit _couplingdatabitB;
        public CouplingDataBit CouplingDataBitB
        {
            get => _couplingdatabitB;
            set { _couplingdatabitB = value; RaisePropertyChanged(); }
        }






        public ONetCoupling2D()
        {
            Parameter = new ToolParameter()
            {
                ToolGroupName = "ONetTool",
                ToolName = "ONet2D耦合",
                ViewName = "ONetCoupling2DView",
                CompletionFlag = DMColor.Gray,
                ExecutionFlag = DMColor.Gray
            };

            LeftPara = new Parameter2D();
            RightPara = new Parameter2D();
        }

        public async Task<bool> ExecuteAsync(CancellationToken token, IEventAggregator eventAggregator, ToolExecutionContext context)
        {
            try
            {
                var motionsystem = context.Get<IMotionSystemService>("IMotionSystemService");//电机控制相关服务
                var databinding = context.Get<IDataBindingContext>("DataBindingContext");//数据绑定容器
                var runtiem = context.Get<IRuntimeContext>("IRuntimeContext");//软件运行过程中更新全局数据
                var logger = context.Get<ILogger>("ILogger");

                _containerProvider = context.ContainerProvider;
               

                _opticalModuleService = _containerProvider.Resolve<OpticalModuleService>();

                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_A}:{STATE_OFF}");
                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_B}:{STATE_OFF}");

                await Task.Delay(1000);

                List<Task<Result2D>> tasks = new List<Task<Result2D>>();

                CouplingController controller = new CouplingController();


                /*左参数*/
                LeftPara.Axisgroup = AxisGroup.Left;
                LeftPara.XParameter.AxisVel = AxisVel;
                LeftPara.XParameter.DataDelay = DataDelay;
                LeftPara.XParameter.SelectedGroup = SelectedGroupA;
                LeftPara.XParameter.CouplingDataBit = CouplingDataBitA;
               


                LeftPara.YParameter.AxisVel = AxisVel;
                LeftPara.YParameter.DataDelay = DataDelay;
                LeftPara.YParameter.SelectedGroup = SelectedGroupA;
                LeftPara.YParameter.CouplingDataBit = CouplingDataBitA;
            




                /*右参数*/
                RightPara.Axisgroup = AxisGroup.Right;
                RightPara.XParameter.AxisVel = AxisVel;
                RightPara.XParameter.DataDelay = DataDelay;
                RightPara.XParameter.SelectedGroup = SelectedGroupB;
                RightPara.XParameter.CouplingDataBit = CouplingDataBitB;

                RightPara.YParameter.AxisVel = AxisVel;
                RightPara.YParameter.DataDelay = DataDelay;
                RightPara.YParameter.SelectedGroup = SelectedGroupB;
                RightPara.YParameter.CouplingDataBit = CouplingDataBitB;


                if (SelectedGroupA == ChannelGroup.None || SelectedGroupB == ChannelGroup.None) return false;
              


                var command = new InstrumentPara(InstrumentType.MaynuoM8811, InstrumentSwitch.OFF);
                eventAggregator.GetEvent<InstrumentCommandEvent>().Publish(command);


                //   await _opticalModuleService.SetLaserStateAsync(startChA, startChB);

                if (LeftPara.Enable)
                {
                    var motionX = motionsystem.GetAxis(LeftPara.XParameter.AxisName);
                    var motionY = motionsystem.GetAxis(LeftPara.YParameter.AxisName);
                    logger.Info($"[左]2D耦合：{Parameter.UserDefined}:Axis_X:{LeftPara.XParameter.AxisName},Axis_Y:{LeftPara.YParameter.AxisName}");
                    tasks.Add(controller.Run2DFullRangeAsync(motionX, motionY, LeftPara, token, eventAggregator, Parameter, logger, _opticalModuleService, 0));
                }

                if (RightPara.Enable)
                {
                    var motionX = motionsystem.GetAxis(RightPara.XParameter.AxisName);
                    var motionY = motionsystem.GetAxis(RightPara.YParameter.AxisName);

                    logger.Info($"[右]2D耦合：{Parameter.UserDefined}:Axis_X:{RightPara.XParameter.AxisName},Axis_Y:{RightPara.YParameter.AxisName}");

                    tasks.Add(controller.Run2DFullRangeAsync(motionX, motionY, RightPara, token, eventAggregator, Parameter, logger, _opticalModuleService, 1));
                }
                await Task.Delay(500);

                Result2D[] allTasks = await Task.WhenAll(tasks.ToArray());

                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_A}:{STATE_ON}");
                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_B}:{STATE_ON}");
                command = new InstrumentPara(InstrumentType.MaynuoM8811, InstrumentSwitch.ON);
                eventAggregator.GetEvent<InstrumentCommandEvent>().Publish(command);


                foreach (var task in allTasks)
                {
                    if (!task.Success)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<Event_Message>().Publish(ex.ToString());
                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_A}:{STATE_ON}");
                //eventAggregator.GetEvent<InstrmentKitCommandEvent>().Publish($"{LD9204S_B}:{STATE_ON}");

                var command = new InstrumentPara(InstrumentType.MaynuoM8811, InstrumentSwitch.ON);
                eventAggregator.GetEvent<InstrumentCommandEvent>().Publish(command);

                return false;
            }
        }

        public JObject Migrate(JObject sourceData, string fromVersion, string toVersion)
        {
            return null;
        }


    }
}
