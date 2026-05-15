using DM.Foundation.DataBinding.Interfaces;
using DM.Foundation.Logging.Interfaces;
using DM.Foundation.Shared.Events;
using ONet.FAU.Tx16_128.Extension.Common;
using ONet.FAU.Tx16_128.Extension.Converters;
using ONet.FAU.Tx16_128.Extension.Events;
using ONet.FAU.Tx16_128.Extension.Model;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ONet.FAU.Tx16_128.Extension.ViewModels
{
    public class OpticalModuleViewModel : BindableBase, IDestructible
    {
        private Brush _displayColor = Brushes.Gray;

        private IEventAggregator _eventAggregator = null;

        private IDataBindingContext _dataBinding;

        private CancellationTokenSource _loopCts;

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



        private ChannelGroup CurrentGroup = ChannelGroup.None;

        private bool SelectedGroupIsDone = false;



        private string _adcunit;
        public string AdcUnit
        {
            get => _adcunit;
            set => SetProperty(ref _adcunit, value);
        }

        private string _mpdiunit;
        public string MPDiUnit
        {
            get => _mpdiunit;
            set => SetProperty(ref _mpdiunit, value);
        }


        private string _mpdounit;
        public string MPDoUnit
        {
            get => _mpdounit;
            set => SetProperty(ref _mpdounit, value);
        }



        private double _voltage;
        public double Voltage
        {
            get => _voltage;
            set => SetProperty(ref _voltage, value);
        }


        private double _temp;
        public double Temp
        {
            get => _temp;
            set => SetProperty(ref _temp, value);
        }


        // 三組各自的 8 個通道
        //public ChannelData[] AdcChannels { get; } = new ChannelData[8];
        public ChannelData[] AdcChannelsA { get; } = new ChannelData[8];
        public ChannelData[] AdcChannelsB { get; } = new ChannelData[8];

        private readonly IContainerProvider _containerProvider;
        private OpticalModuleService _opticalModuleService;
        private ILogger _logger;

        //private System.Timers.Timer _refreshTimer;

      
        public DelegateCommand<object> CheckedLaserCmd { get; private set; }


 
        private const string PRODUCT_TYPE = "1_6T";
        private Task _pollingTask;
        private CancellationTokenSource _pollingCts;


        public OpticalModuleViewModel(IEventAggregator eventAggregator, IContainerProvider containerProvider, IDataBindingContext dataBinding)
        {
            try
            {
                _eventAggregator = eventAggregator;
                _containerProvider = containerProvider;
                _eventAggregator.GetEvent<InstrumentCommandEvent>().Subscribe(OnInstrumentCommandEvent);
                //CheckedLaserCmd = new DelegateCommand<object>(OnCheckedLaserCmdAsync);

                // 初始化所有通道
                for (int i = 0; i < 8; i++)
                {
                    AdcChannelsA[i] = new ChannelData();
                    AdcChannelsB[i] = new ChannelData();

                }

                AdcUnit = "uA";
                MPDiUnit = "uA";
                MPDoUnit = "uA";


                _opticalModuleService = _containerProvider.Resolve<OpticalModuleService>();
                _logger = _containerProvider.Resolve<ILogger>();
                _dataBinding = dataBinding;

                _eventAggregator.GetEvent<AppStartUpEvent>().Subscribe(OnAppStartUpEvent);
                //_eventAggregator.GetEvent<InstrmentKitCommandEvent>().Subscribe(OnInstrmentKitCommandEvent);
            }
            catch (Exception ex)
            {

                _logger?.Error(ex.ToString());
            }



        }

        private void OnAppStartUpEvent(object obj)
        {
            try
            {
                var portNum = _dataBinding.Get("仪表端口号", "光模块").Value;


                //string ProductType = _dataBinding.Get("产品类型", "产品名称").Value;

                //if (ProductType == PRODUCT_TYPE)
                //{
                //    if (!_opticalModuleService.Open(portNum))
                //    {
                //        _eventAggregator.GetEvent<Event_Message>().Publish("1.6T光模块：打开串口失败。");

                //        return;
                //    }
                //}

                if (!_opticalModuleService.Open(portNum))
                {
                    _eventAggregator.GetEvent<Event_Message>().Publish("1.6T光模块：打开串口失败。");

                    return;
                }
            }
            catch (Exception ex)
            {

                _eventAggregator.GetEvent<Event_Message>().Publish($"{ex.Message}");
                _logger?.Error(ex.ToString());
            }
        }

        private async void OnInstrumentCommandEvent(object obj)
        {
            try
            {
                var para = (InstrumentPara)obj;

                _eventAggregator.GetEvent<Event_Message>().Publish($"{para.Type.ToString()},{para.Switch}");

                if(para.Type == Enums.InstrumentType.MaynuoM8811)
                {
                    if(para.Switch == Enums.InstrumentSwitch.ON)
                    {
                       
                        StartPolling();
                    }
                    else if(para.Switch == Enums.InstrumentSwitch.OFF)
                    {
                        await StopPollingAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _eventAggregator.GetEvent<Event_Message>().Publish(ex.ToString());
            }
        }

        private async void OnGroupChangedAsync()
        {
            //if (SelectedGroup == ChannelGroup.None) return;

            //SelectedGroupIsDone = false;

            //int startCh = (int)SelectedGroup;

            //_eventAggregator.GetEvent<Event_Message>().Publish($"{SelectedGroup}");

        }
        public void Destroy()
        {
           
        }



        private async void StartPolling()
        {

            await StopPollingAsync();

            await Task.Delay(1000);
            var res = await _opticalModuleService.SetPawssword();


            _pollingTask = Task.Run(async () =>
            {
                try
                {

                    int startChannel = 0;
                    _pollingCts = new CancellationTokenSource();
                    while (!_pollingCts.IsCancellationRequested)
                    {
                        try
                        {


                            int startA = (int)SelectedGroupA;
                            int startB = (int)SelectedGroupB;

                            // 并行读两组
                            var adcValuesA = await _opticalModuleService.ReadRSSIAsync(startA, 8);
                             await Task.Delay(30);
                            var adcValuesB = await _opticalModuleService.ReadRSSIAsync(startB, 8);

                            //var adcValuesA = await taskA;
                            //var adcValuesB = await taskB;

                            if (adcValuesA == null || adcValuesA.Length < 8 || adcValuesB == null || adcValuesB.Length < 8)
                            {
                                await Task.Delay(100);
                                continue;
                            }

                            for (int i = 0; i < 8; i++)
                            {
                                AdcChannelsA[i].Current = adcValuesA[i];
                                AdcChannelsA[i].StatusColor = GetStatusColor(adcValuesA[i]);

                                AdcChannelsB[i].Current = adcValuesB[i];
                                AdcChannelsB[i].StatusColor = GetStatusColor(adcValuesB[i]);
                            }

                            RaisePropertyChanged(nameof(AdcChannelsA));
                            RaisePropertyChanged(nameof(AdcChannelsB));
                            await Task.Delay(100);

                        }
                        catch (OperationCanceledException)
                        {
                            // 正常取消，不记录错误
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"轮询刷新单次执行异常: {ex.Message}");
                            // 可选：短暂等待再继续，避免高频错误刷屏
                            await Task.Delay(100);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 最外层取消
                }
                catch (Exception ex)
                {
                    _logger?.Error($"轮询循环异常退出: {ex.Message}");
                }
                finally
                {
                    _logger?.Info($"轮询循环已结束");
                }
            });

        }

        private Brush GetStatusColor(ushort rawValue)
        {
            double value = rawValue; // 可加转换

            if (value > 3000) return Brushes.Green;     // 正常高值
            if (value > 1000) return Brushes.Orange;    // 中间
            if (value > 100) return Brushes.Red;       // 异常低
            return Brushes.Gray;                        // 无信号或极低
        }



        // 停止轮询（必须配套实现）
        private async Task StopPollingAsync()
        {

            try
            {
                _pollingCts?.Cancel();
            }
            catch { }

            // 可选：等待任务结束（非必须，但有助于清理）
            // 如果不等待，任务会在后台继续跑完（通常几秒内结束）
            // 如果你希望确保停止后再做其他事，可以加上：
            try { await _pollingTask.ConfigureAwait(false); } catch { }

            // 清理资源
            if (_pollingCts != null)
            {
                try { _pollingCts.Dispose(); } catch { }
                _pollingCts = null;
            }

            _pollingTask = null;  // 可选，帮助 GC

            _logger?.Info($"已停止轮询");
        }


    }
}
