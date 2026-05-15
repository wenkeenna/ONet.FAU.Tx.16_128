using ONet.FAU.Tx16_128.Extension.ViewModels;
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

            regionManager.RegisterViewWithRegion("OpticalModuleView", typeof(OpticalModuleView));
            regionManager.RegisterViewWithRegion("Region_MaynuoM8811ViewONet", typeof(MaynuoM8811View));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<ONetCoupling1DView, ONetCoupling1DViewModel>("ONetCoupling1DView");
            containerRegistry.RegisterDialog<ONetCoupling2DView, ONetCoupling2DViewModel>("ONetCoupling2DView");


            containerRegistry.RegisterDialog<ONetFAVisionCorrectionView, ONetFAVisionCorrectionViewModel>("ONetFAVisionCorrectionView");
            containerRegistry.RegisterDialog<ONetFACouplingCorrectionView, ONetFACouplingCorrectionViewModel>("ONetFACouplingCorrectionView");

            containerRegistry.RegisterDialog<ONetPDPositionView, ONetPDPositionViewModel>("ONetPDPositionView");

            containerRegistry.RegisterDialog<CompositeToolView, CompositeToolViewModel>("CompositeToolView");
        }
    }
}
