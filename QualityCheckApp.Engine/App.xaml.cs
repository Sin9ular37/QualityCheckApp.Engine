using System;
using System.Windows;
using QualityCheckApp.Engine.Services;

namespace QualityCheckApp.Engine
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                ArcGisLicenseInitializer.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("ArcGIS Engine 初始化失败：{0}", ex.Message),
                    "启动失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ArcGisLicenseInitializer.Shutdown();
            base.OnExit(e);
        }
    }
}
