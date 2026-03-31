using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using OSGeo.OGR;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class GdalOgrDatasetReader : IGeometryDatasetReader
    {
        public Task<IReadOnlyList<GdbLayerInfo>> ReadContainersAsync(string gdbPath, CancellationToken cancellationToken)
        {
            return Task.Run(() => ReadLayers(gdbPath, cancellationToken), cancellationToken);
        }

        private static IReadOnlyList<GdbLayerInfo> ReadLayers(string gdbPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                throw new ArgumentException("未指定 .gdb 路径。", "gdbPath");
            }

            if (!Directory.Exists(gdbPath))
            {
                throw new DirectoryNotFoundException(string.Format("未找到目录：{0}", gdbPath));
            }

            GdalRuntimeBootstrapper.EnsureInitialized();

            var layers = new List<GdbLayerInfo>();
            var driverName = GdalRuntimeBootstrapper.GetOpenFileGdbDriverName();

            using (var dataSource = Ogr.Open(gdbPath, 0))
            {
                if (dataSource == null)
                {
                    layers.Add(CreateFailedLayer(gdbPath, driverName, "GDAL/OGR 无法打开该 File Geodatabase。"));
                    return layers;
                }

                var layerCount = dataSource.GetLayerCount();
                for (var index = 0; index < layerCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (var layer = dataSource.GetLayerByIndex(index))
                    {
                        if (layer == null)
                        {
                            continue;
                        }

                        using (var definition = layer.GetLayerDefn())
                        {
                            var geometryType = definition == null ? wkbGeometryType.wkbUnknown : definition.GetGeomType();
                            var featureCount = layer.GetFeatureCount(1);
                            var layerName = layer.GetName();
                            var envelope = new Envelope();
                            var hasExtent = layer.GetExtent(envelope, 1) == 0;

                            layers.Add(new GdbLayerInfo
                            {
                                GdbPath = gdbPath,
                                DatasetName = layerName,
                                LayerName = layerName,
                                GeometryType = TranslateGeometryType(geometryType),
                                Summary = string.Format("驱动：{0}；要素数：{1}；来源：{2}",
                                    string.IsNullOrWhiteSpace(driverName) ? "未知" : driverName,
                                    featureCount,
                                    Path.GetFileName(gdbPath)),
                                HasExtent = hasExtent,
                                ExtentXMin = envelope.MinX,
                                ExtentYMin = envelope.MinY,
                                ExtentXMax = envelope.MaxX,
                                ExtentYMax = envelope.MaxY,
                                Displayable = featureCount > 0 && geometryType != wkbGeometryType.wkbUnknown,
                                IsVisible = false
                            });
                        }
                    }
                }
            }

            if (layers.Count == 0)
            {
                layers.Add(CreateFailedLayer(gdbPath, driverName, "GDAL/OGR 已打开目录，但没有枚举到任何图层。"));
            }

            return layers;
        }

        private static GdbLayerInfo CreateFailedLayer(string gdbPath, string driverName, string message)
        {
            return new GdbLayerInfo
            {
                GdbPath = gdbPath,
                DatasetName = "OpenFileGDB",
                LayerName = Path.GetFileNameWithoutExtension(gdbPath),
                GeometryType = "未知",
                Summary = string.Format("驱动：{0}；结果：{1}", string.IsNullOrWhiteSpace(driverName) ? "不可用" : driverName, message),
                Displayable = false,
                IsVisible = false
            };
        }

        private static string TranslateGeometryType(wkbGeometryType geometryType)
        {
            var value = geometryType.ToString();

            if (value.StartsWith("wkb", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
            {
                value = value.Substring(3);
            }

            return string.IsNullOrWhiteSpace(value) ? "未知" : value;
        }
    }
}
