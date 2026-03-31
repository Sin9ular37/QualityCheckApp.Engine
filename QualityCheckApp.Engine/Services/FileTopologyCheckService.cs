using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;

using OSGeo.OGR;

using QualityCheckApp.Engine.Models;

using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using OgrGeometry = OSGeo.OGR.Geometry;

namespace QualityCheckApp.Engine.Services
{
    public class FileTopologyCheckService : ITopologyCheckService
    {
        private static readonly WKTReader WktReader = new WKTReader();

        public Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            return Task.Run(() => CheckLayer(layerInfo, cancellationToken), cancellationToken);
        }

        private static TopologyCheckResult CheckLayer(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath))
            {
                throw new ArgumentException("缺少 .gdb 路径，无法执行目录检查。", "layerInfo");
            }

            var result = new TopologyCheckResult
            {
                LayerName = layerInfo.LayerName,
                RuleSummary = "空几何、无效几何、自相交/环自相交、非简单几何、图层可读性"
            };

            var issues = new List<TopologyIssueInfo>();
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(layerInfo.GdbPath))
            {
                issues.Add(CreateIssue(layerInfo, -1, "目录缺失", ".gdb 目录不存在或已被删除。", null));
            }
            else
            {
                GdalRuntimeBootstrapper.EnsureInitialized();
                using (var dataSource = Ogr.Open(layerInfo.GdbPath, 0))
                {
                    if (dataSource == null)
                    {
                        issues.Add(CreateIssue(layerInfo, -1, "GDAL 打开失败", "GDAL/OGR 无法重新打开该 File Geodatabase。", null));
                    }
                    else if (!string.IsNullOrWhiteSpace(layerInfo.DatasetName))
                    {
                        using (var layer = dataSource.GetLayerByName(layerInfo.DatasetName))
                        {
                            if (layer == null)
                            {
                                issues.Add(CreateIssue(layerInfo, -1, "图层缺失", "GDAL/OGR 未在该 .gdb 中找到当前选中的图层。", null));
                            }
                            else
                            {
                                InspectLayer(layerInfo, layer, result, issues, cancellationToken);
                            }
                        }
                    }
                }
            }

            result.Issues = issues;
            result.Summary = BuildSummary(layerInfo.LayerName, result.RuleSummary, issues.Count);
            return result;
        }

        private static void InspectLayer(GdbLayerInfo layerInfo, Layer layer, TopologyCheckResult result, IList<TopologyIssueInfo> issues, CancellationToken cancellationToken)
        {
            result.FeatureCount = (int)Math.Max(layer.GetFeatureCount(1), 0);
            layer.ResetReading();

            Feature feature;
            while ((feature = layer.GetNextFeature()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (feature)
                {
                    var featureId = NormalizeFeatureId(feature.GetFID());
                    var ogrGeometry = feature.GetGeometryRef();

                    if (ogrGeometry == null || ogrGeometry.IsEmpty())
                    {
                        issues.Add(CreateIssue(layerInfo, featureId, "空几何", "要素没有可用几何。", null));
                        continue;
                    }

                    NtsGeometry ntsGeometry;
                    string wkt;

                    if (!TryReadGeometry(ogrGeometry, out ntsGeometry, out wkt))
                    {
                        issues.Add(CreateIssue(layerInfo, featureId, "几何转换失败", "OGR 几何无法转换为 NTS 几何。", null));
                        continue;
                    }

                    if (ntsGeometry.IsEmpty)
                    {
                        issues.Add(CreateIssue(layerInfo, featureId, "空几何", "转换后的几何为空。", ntsGeometry));
                        continue;
                    }

                    var validOp = new IsValidOp(ntsGeometry);
                    var validationError = validOp.ValidationError;
                    if (validationError != null)
                    {
                        issues.Add(CreateIssue(
                            layerInfo,
                            featureId,
                            TranslateValidationRule(validationError.ErrorType),
                            BuildValidationDescription(validationError, wkt),
                            ntsGeometry));
                    }

                    if (!ntsGeometry.IsSimple)
                    {
                        issues.Add(CreateIssue(layerInfo, featureId, "非简单几何", BuildSimpleDescription(ntsGeometry), ntsGeometry));
                    }
                }
            }
        }

        private static bool TryReadGeometry(OgrGeometry ogrGeometry, out NtsGeometry geometry, out string wkt)
        {
            geometry = null;
            wkt = string.Empty;

            if (ogrGeometry == null)
            {
                return false;
            }

            if (ogrGeometry.ExportToWkt(out wkt) != 0 || string.IsNullOrWhiteSpace(wkt))
            {
                return false;
            }

            try
            {
                geometry = WktReader.Read(wkt);
                return geometry != null;
            }
            catch
            {
                if (geometry != null)
                {
                    geometry = null;
                }

                return false;
            }
        }

        private static TopologyIssueInfo CreateIssue(GdbLayerInfo layerInfo, int featureId, string ruleName, string description, NtsGeometry geometry)
        {
            var issue = new TopologyIssueInfo
            {
                RuleName = ruleName,
                Description = description,
                GdbPath = layerInfo.GdbPath,
                DatasetName = layerInfo.DatasetName,
                LayerName = layerInfo.LayerName,
                FeatureId = featureId,
                RelatedFeatureId = null,
                HasFocusExtent = false
            };

            if (geometry != null && !geometry.IsEmpty)
            {
                var envelope = geometry.EnvelopeInternal;
                if (envelope != null)
                {
                    issue.HasFocusExtent = true;
                    issue.FocusXMin = envelope.MinX;
                    issue.FocusYMin = envelope.MinY;
                    issue.FocusXMax = envelope.MaxX;
                    issue.FocusYMax = envelope.MaxY;
                }
            }

            return issue;
        }

        private static int NormalizeFeatureId(long featureId)
        {
            if (featureId > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (featureId < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)featureId;
        }

        private static string TranslateValidationRule(TopologyValidationErrors errorType)
        {
            switch (errorType)
            {
                case TopologyValidationErrors.SelfIntersection:
                    return "自相交";
                case TopologyValidationErrors.RingSelfIntersection:
                    return "环自相交";
                case TopologyValidationErrors.HoleOutsideShell:
                    return "洞在壳外";
                case TopologyValidationErrors.NestedHoles:
                    return "洞嵌套";
                case TopologyValidationErrors.DisconnectedInteriors:
                    return "内部不连通";
                case TopologyValidationErrors.NestedShells:
                    return "壳嵌套";
                case TopologyValidationErrors.DuplicateRings:
                    return "重复环";
                case TopologyValidationErrors.TooFewPoints:
                    return "点数不足";
                case TopologyValidationErrors.InvalidCoordinate:
                    return "坐标无效";
                case TopologyValidationErrors.RingNotClosed:
                    return "环未闭合";
                default:
                    return "无效几何";
            }
        }

        private static string BuildValidationDescription(TopologyValidationError error, string wkt)
        {
            if (error == null)
            {
                return "几何无效。";
            }

            var description = error.Message;
            if (error.Coordinate != null)
            {
                description = string.Format("{0}，坐标：({1:0.###}, {2:0.###})", description, error.Coordinate.X, error.Coordinate.Y);
            }

            if (!string.IsNullOrWhiteSpace(wkt))
            {
                description = string.Format("{0}。WKT：{1}", description, ShortenText(wkt, 240));
            }

            return description;
        }

        private static string ShortenText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static string BuildSimpleDescription(NtsGeometry geometry)
        {
            return string.Format("几何不是 Simple 状态。类型：{0}；部件数：{1}；顶点数：{2}。",
                geometry.GeometryType,
                geometry.NumGeometries,
                geometry.NumPoints);
        }

        private static string BuildSummary(string layerName, string ruleSummary, int issueCount)
        {
            return string.Format("图层 {0} 已完成几何检查，执行项：{1}，发现 {2} 个问题。", layerName, ruleSummary, issueCount);
        }
    }
}
