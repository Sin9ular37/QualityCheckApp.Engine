using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using QualityCheckApp.Engine.Infrastructure;
using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class ArcGisLayerProvider : IGdbLayerProvider
    {
        public Task<IReadOnlyList<GdbLayerInfo>> LoadLayersAsync(string gdbPath, CancellationToken cancellationToken)
        {
            return StaTask.Run(() => EnumerateLayers(gdbPath, cancellationToken), cancellationToken);
        }

        public IFeatureLayer CreateFeatureLayer(GdbLayerInfo layerInfo)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath) || string.IsNullOrWhiteSpace(layerInfo.DatasetName))
            {
                throw new ArgumentException("缺少 .gdb 路径或数据集信息，无法创建图层。");
            }

            IWorkspaceFactory factory = new FileGDBWorkspaceFactoryClass();
            IFeatureLayer featureLayer = null;
            IFeatureClass featureClass = null;
            IWorkspace workspace = null;
            IFeatureWorkspace featureWorkspace = null;

            try
            {
                workspace = factory.OpenFromFile(layerInfo.GdbPath, 0);
                featureWorkspace = (IFeatureWorkspace)workspace;
                featureClass = featureWorkspace.OpenFeatureClass(layerInfo.DatasetName);

                featureLayer = new FeatureLayerClass
                {
                    FeatureClass = featureClass,
                    Name = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName
                };

                Marshal.ReleaseComObject(featureClass);
                featureClass = null;

                return featureLayer;
            }
            catch
            {
                if (featureLayer != null)
                {
                    Marshal.ReleaseComObject(featureLayer);
                }

                throw;
            }
            finally
            {
                if (featureClass != null)
                {
                    Marshal.ReleaseComObject(featureClass);
                }

                if (featureWorkspace != null)
                {
                    Marshal.ReleaseComObject(featureWorkspace);
                }

                if (workspace != null)
                {
                    Marshal.ReleaseComObject(workspace);
                }

                if (factory != null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }

        private static IReadOnlyList<GdbLayerInfo> EnumerateLayers(string gdbPath, CancellationToken token)
        {
            var results = new List<GdbLayerInfo>();

            IWorkspaceFactory factory = new FileGDBWorkspaceFactoryClass();
            IWorkspace workspace = null;
            IEnumDataset enumDataset = null;
            IFeatureWorkspace featureWorkspace = null;

            try
            {
                workspace = factory.OpenFromFile(gdbPath, 0);
                enumDataset = workspace.get_Datasets(esriDatasetType.esriDTFeatureClass);
                IDataset dataset = null;

                featureWorkspace = (IFeatureWorkspace)workspace;

                while ((dataset = enumDataset.Next()) != null)
                {
                    token.ThrowIfCancellationRequested();

                var featureClass = featureWorkspace.OpenFeatureClass(dataset.Name);
                    try
                    {
                        results.Add(BuildLayerInfo(gdbPath, dataset.Name, featureClass));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(featureClass);
                        Marshal.ReleaseComObject(dataset);
                    }
                }

                return results;
            }
            finally
            {
                if (enumDataset != null)
                {
                    Marshal.ReleaseComObject(enumDataset);
                }

                if (featureWorkspace != null)
                {
                    Marshal.ReleaseComObject(featureWorkspace);
                }

                if (workspace != null)
                {
                    Marshal.ReleaseComObject(workspace);
                }

                if (factory != null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }

        private static GdbLayerInfo BuildLayerInfo(string gdbPath, string datasetName, IFeatureClass featureClass)
        {
            var alias = featureClass.AliasName;
            var geometryType = featureClass.ShapeType;
            var featureCount = featureClass.FeatureCount(null);
            var displayable = geometryType == esriGeometryType.esriGeometryPoint
                              || geometryType == esriGeometryType.esriGeometryLine
                              || geometryType == esriGeometryType.esriGeometryPolyline
                              || geometryType == esriGeometryType.esriGeometryPolygon;

            var isDisplayable = displayable && featureCount > 0;

            return new GdbLayerInfo
            {
                GdbPath = gdbPath,
                DatasetName = datasetName,
                LayerName = string.IsNullOrWhiteSpace(alias) ? datasetName : alias,
                GeometryType = geometryType.ToString(),
                Displayable = isDisplayable,
                IsVisible = isDisplayable
            };
        }
    }
}
