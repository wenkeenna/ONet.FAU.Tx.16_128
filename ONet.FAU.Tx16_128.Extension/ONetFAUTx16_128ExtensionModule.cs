using ONet.FAU.Tx16_128.Extension.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONet.FAU.Tx16_128.Extension
{
    public class ONetFAUTx16_128ExtensionModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();

            regionManager.RegisterViewWithRegion("Region_GolightOSMWD41310View_A", typeof(GolightOSMWD41310_A_View));
            regionManager.RegisterViewWithRegion("Region_GolightOSMWD41310View_B", typeof(GolightOSMWD41310_B_View));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
           
        }
    }
}
