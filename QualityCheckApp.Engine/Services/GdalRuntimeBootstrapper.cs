using System;

using MaxRev.Gdal.Core;

using OSGeo.GDAL;
using OSGeo.OGR;

namespace QualityCheckApp.Engine.Services
{
    internal static class GdalRuntimeBootstrapper
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                GdalBase.ConfigureAll();
                Gdal.AllRegister();
                Ogr.RegisterAll();
                _initialized = true;
            }
        }

        public static string GetOpenFileGdbDriverName()
        {
            EnsureInitialized();

            using (var driver = Ogr.GetDriverByName("OpenFileGDB"))
            {
                return driver == null ? string.Empty : driver.GetName();
            }
        }
    }
}
