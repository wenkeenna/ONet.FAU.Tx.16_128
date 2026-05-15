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
    public class ONetCoupling1D : BindableBase, IToolBase, IToolMigratable
    {
        private ToolParameter _parameter;
        public ToolParameter Parameter { get { return _parameter; } set { _parameter = value; RaisePropertyChanged(); } }


        private Parameter1D _leftpara;
        public Parameter1D LeftPara
        {
            get { return _leftpara; }
            set { _leftpara = value; RaisePropertyChanged(); }
        }


        private Parameter1D _rightpara;
        public Parameter1D RightPara
        {
            get { return _rightpara; }
            set { _rightpara = value; RaisePropertyChanged(); }
        }


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
        public ONetCoupling1D()
        {
            Parameter = new ToolParameter()
            {
                ToolGroupName = "ONetTool",
                ToolName = "ONet1D耦合",
                ViewName = "ONetCoupling1DView",
                CompletionFlag = DMColor.Gray,
                ExecutionFlag = DMColor.Gray
            };

            LeftPara = new Parameter1D();
            RightPara = new Parameter1D();
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

                List<Task<Result1D>> tasks = new List<Task<Result1D>>();

                CouplingController controller = new CouplingController();

                LeftPara.AxisVel = AxisVel;
                LeftPara.DataDelay = DataDelay;
                LeftPara.SelectedGroup = SelectedGroupA;
                LeftPara.CouplingDataBit = CouplingDataBitA;
                LeftPara.Axisgroup = AxisGroup.Left;



                RightPara.AxisVel = AxisVel;
                RightPara.DataDelay = DataDelay;
                RightPara.SelectedGroup = SelectedGroupB;
                RightPara.CouplingDataBit = CouplingDataBitB;
                RightPara.Axisgroup = AxisGroup.Right;


                if (LeftPara.SelectedGroup == ChannelGroup.None || RightPara.SelectedGroup == ChannelGroup.None) return false;
                int startChA = (int)LeftPara.SelectedGroup;
                int startChB = (int)RightPara.SelectedGroup;

                //  await _opticalModuleService.SetLaserStateAsync(startChA, startChB);

                var command = new InstrumentPara(InstrumentType.MaynuoM8811, InstrumentSwitch.OFF);

                eventAggregator.GetEvent<InstrumentCommandEvent>().Publish(command);



                if (LeftPara.Enable)
                {
                    var motion = motionsystem.GetAxis(LeftPara.AxisName);

                    if (LeftPara.SelectedGroup == ChannelGroup.None) return false;
                    int startCh = (int)LeftPara.SelectedGroup;
                    //await _opticalModuleService.SetLaserStateAsync(startCh);

                    tasks.Add(controller.Run1DFullRangeAsync(motion, LeftPara, token, eventAggregator, Parameter, logger, _opticalModuleService, 0));
                }

                if (RightPara.Enable)
                {
                    var motion = motionsystem.GetAxis(RightPara.AxisName);

                    if (RightPara.SelectedGroup == ChannelGroup.None) return false;
                    int startCh = (int)RightPara.SelectedGroup;

                    tasks.Add(controller.Run1DFullRangeAsync(motion, RightPara, token, eventAggregator, Parameter, logger, _opticalModuleService, 1));
                }
                await Task.Delay(500);

                Result1D[] allTasks = await Task.WhenAll(tasks.ToArray());


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
