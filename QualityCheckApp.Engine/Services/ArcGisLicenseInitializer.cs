using System;
using ESRI.ArcGIS;
using ESRI.ArcGIS.esriSystem;

namespace QualityCheckApp.Engine.Services
{
    /// <summary>
    /// 负责 ArcGIS Engine Runtime 许可的绑定与初始化。
    /// </summary>
    public static class ArcGisLicenseInitializer
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static AoInitialize _aoInitialize;

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                if (!RuntimeManager.Bind(ProductCode.Engine))
                {
                    throw new InvalidOperationException("无法绑定 ArcGIS Engine Runtime，请确认已安装正确的 Runtime。");
                }

                _aoInitialize = new AoInitializeClass();
                var status = _aoInitialize.Initialize(esriLicenseProductCode.esriLicenseProductCodeEngine);
                if (status != esriLicenseStatus.esriLicenseCheckedOut)
                {
                    throw new InvalidOperationException(string.Format("ArcGIS Engine 许可初始化失败：{0}。", status));
                }

                _initialized = true;
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_initialized || _aoInitialize == null)
                {
                    return;
                }

                _aoInitialize.Shutdown();
                _aoInitialize = null;
                _initialized = false;
            }
        }
    }
}
