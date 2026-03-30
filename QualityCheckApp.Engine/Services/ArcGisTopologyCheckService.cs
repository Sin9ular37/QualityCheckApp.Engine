using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

using QualityCheckApp.Engine.Infrastructure;
using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class ArcGisTopologyCheckService : ITopologyCheckService
    {
        public Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            return StaTask.Run(() => CheckLayer(layerInfo, cancellationToken), cancellationToken);
        }

        private static TopologyCheckResult CheckLayer(GdbLayerInfo layerInfo, CancellationToken token)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath) || string.IsNullOrWhiteSpace(layerInfo.DatasetName))
            {
                throw new ArgumentException("缺少 .gdb 路径或数据集信息，无法执行拓扑检测。", "layerInfo");
            }

            IWorkspaceFactory factory = new FileGDBWorkspaceFactoryClass();
            IWorkspace workspace = null;
            IFeatureWorkspace featureWorkspace = null;
            IFeatureClass featureClass = null;

            try
            {
                workspace = factory.OpenFromFile(layerInfo.GdbPath, 0);
                featureWorkspace = (IFeatureWorkspace)workspace;
                featureClass = featureWorkspace.OpenFeatureClass(layerInfo.DatasetName);

                var result = new TopologyCheckResult();
                result.LayerName = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName;
                result.FeatureCount = featureClass.FeatureCount(null);
                result.RuleSummary = BuildRuleSummary(featureClass.ShapeType);

                var issues = new List<TopologyIssueInfo>();
                if (result.FeatureCount > 0)
                {
                    InspectFeatures(featureClass, layerInfo, issues, token);
                }

                result.Issues = issues;
                result.Summary = BuildSummary(result.LayerName, result.FeatureCount, result.RuleSummary, result.IssueCount);
                return result;
            }
            finally
            {
                ReleaseComObject(featureClass);
                ReleaseComObject(featureWorkspace);
                ReleaseComObject(workspace);
                ReleaseComObject(factory);
            }
        }

        private static void InspectFeatures(IFeatureClass featureClass, GdbLayerInfo layerInfo, IList<TopologyIssueInfo> issues, CancellationToken token)
        {
            IFeatureCursor cursor = null;
            IFeature feature = null;

            try
            {
                cursor = featureClass.Search(null, false);

                while ((feature = cursor.NextFeature()) != null)
                {
                    token.ThrowIfCancellationRequested();

                    IGeometry geometry = null;
                    try
                    {
                        geometry = feature.ShapeCopy;

                        if (geometry == null || geometry.IsEmpty)
                        {
                            issues.Add(CreateIssue(layerInfo, "空几何", "要素未包含有效几何。", feature.OID, null, null));
                            continue;
                        }

                        if (ShouldCheckSimpleGeometry(featureClass.ShapeType) && !((ITopologicalOperator)geometry).IsSimple)
                        {
                            issues.Add(CreateIssue(layerInfo, "非简单几何", "要素几何不是 simple 状态，可能存在自相交、自重叠或方向异常。", feature.OID, null, geometry));
                        }

                        if (featureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                        {
                            CheckDuplicatePoints(featureClass, layerInfo, feature, geometry, issues, token);
                        }

                        if (featureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                        {
                            CheckPolygonOverlaps(featureClass, layerInfo, feature, geometry, issues, token);
                        }
                    }
                    finally
                    {
                        ReleaseComObject(geometry);
                        ReleaseComObject(feature);
                        feature = null;
                    }
                }
            }
            finally
            {
                ReleaseComObject(feature);
                ReleaseComObject(cursor);
            }
        }

        private static void CheckDuplicatePoints(IFeatureClass featureClass, GdbLayerInfo layerInfo, IFeature feature, IGeometry geometry, IList<TopologyIssueInfo> issues, CancellationToken token)
        {
            ISpatialFilter filter = new SpatialFilterClass();
            IFeatureCursor cursor = null;
            IFeature candidate = null;

            try
            {
                filter.Geometry = geometry;
                filter.GeometryField = featureClass.ShapeFieldName;
                filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                filter.SubFields = string.Format("{0},{1}", featureClass.OIDFieldName, featureClass.ShapeFieldName);

                cursor = featureClass.Search(filter, false);
                while ((candidate = cursor.NextFeature()) != null)
                {
                    token.ThrowIfCancellationRequested();

                    if (candidate.OID <= feature.OID)
                    {
                        ReleaseComObject(candidate);
                        candidate = null;
                        continue;
                    }

                    IGeometry candidateGeometry = null;
                    try
                    {
                        candidateGeometry = candidate.ShapeCopy;
                        if (candidateGeometry != null && !candidateGeometry.IsEmpty && ((IRelationalOperator)geometry).Equals(candidateGeometry))
                        {
                            issues.Add(CreateIssue(layerInfo, "重复点", "检测到坐标完全相同的点要素。", feature.OID, candidate.OID, geometry));
                        }
                    }
                    finally
                    {
                        ReleaseComObject(candidateGeometry);
                        ReleaseComObject(candidate);
                        candidate = null;
                    }
                }
            }
            finally
            {
                ReleaseComObject(candidate);
                ReleaseComObject(cursor);
                ReleaseComObject(filter);
            }
        }

        private static void CheckPolygonOverlaps(IFeatureClass featureClass, GdbLayerInfo layerInfo, IFeature feature, IGeometry geometry, IList<TopologyIssueInfo> issues, CancellationToken token)
        {
            ISpatialFilter filter = new SpatialFilterClass();
            IFeatureCursor cursor = null;
            IFeature candidate = null;

            try
            {
                filter.Geometry = geometry;
                filter.GeometryField = featureClass.ShapeFieldName;
                filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                filter.SubFields = string.Format("{0},{1}", featureClass.OIDFieldName, featureClass.ShapeFieldName);

                cursor = featureClass.Search(filter, false);
                while ((candidate = cursor.NextFeature()) != null)
                {
                    token.ThrowIfCancellationRequested();

                    if (candidate.OID <= feature.OID)
                    {
                        ReleaseComObject(candidate);
                        candidate = null;
                        continue;
                    }

                    IGeometry candidateGeometry = null;
                    IGeometry intersection = null;

                    try
                    {
                        candidateGeometry = candidate.ShapeCopy;
                        if (candidateGeometry == null || candidateGeometry.IsEmpty)
                        {
                            continue;
                        }

                        intersection = ((ITopologicalOperator)geometry).Intersect(candidateGeometry, esriGeometryDimension.esriGeometry2Dimension);
                        if (intersection == null || intersection.IsEmpty)
                        {
                            continue;
                        }

                        var area = intersection as IArea;
                        if (area != null && area.Area > 0)
                        {
                            issues.Add(CreateIssue(layerInfo, "面重叠", "检测到与同图层其他面要素存在重叠区域。", feature.OID, candidate.OID, intersection));
                        }
                    }
                    finally
                    {
                        ReleaseComObject(intersection);
                        ReleaseComObject(candidateGeometry);
                        ReleaseComObject(candidate);
                        candidate = null;
                    }
                }
            }
            finally
            {
                ReleaseComObject(candidate);
                ReleaseComObject(cursor);
                ReleaseComObject(filter);
            }
        }

        private static TopologyIssueInfo CreateIssue(GdbLayerInfo layerInfo, string ruleName, string description, int featureId, int? relatedFeatureId, IGeometry focusGeometry)
        {
            var issue = new TopologyIssueInfo
            {
                RuleName = ruleName,
                Description = description,
                GdbPath = layerInfo.GdbPath,
                DatasetName = layerInfo.DatasetName,
                LayerName = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName,
                FeatureId = featureId,
                RelatedFeatureId = relatedFeatureId
            };

            if (focusGeometry != null && !focusGeometry.IsEmpty)
            {
                IEnvelope envelope = null;
                try
                {
                    envelope = focusGeometry.Envelope;
                    if (envelope != null && !envelope.IsEmpty)
                    {
                        double xMin;
                        double yMin;
                        double xMax;
                        double yMax;
                        envelope.QueryCoords(out xMin, out yMin, out xMax, out yMax);

                        issue.HasFocusExtent = true;
                        issue.FocusXMin = xMin;
                        issue.FocusYMin = yMin;
                        issue.FocusXMax = xMax;
                        issue.FocusYMax = yMax;
                    }
                }
                finally
                {
                    ReleaseComObject(envelope);
                }
            }

            return issue;
        }

        private static bool ShouldCheckSimpleGeometry(esriGeometryType geometryType)
        {
            return geometryType == esriGeometryType.esriGeometryLine
                   || geometryType == esriGeometryType.esriGeometryPolyline
                   || geometryType == esriGeometryType.esriGeometryPolygon;
        }

        private static string BuildRuleSummary(esriGeometryType geometryType)
        {
            if (geometryType == esriGeometryType.esriGeometryPoint)
            {
                return "空几何、重复点";
            }

            if (geometryType == esriGeometryType.esriGeometryPolygon)
            {
                return "空几何、非简单几何、面重叠";
            }

            if (geometryType == esriGeometryType.esriGeometryLine || geometryType == esriGeometryType.esriGeometryPolyline)
            {
                return "空几何、非简单几何";
            }

            return "空几何";
        }

        private static string BuildSummary(string layerName, int featureCount, string ruleSummary, int issueCount)
        {
            if (featureCount == 0)
            {
                return string.Format("图层 {0} 没有可检测要素。", layerName);
            }

            return string.Format("图层 {0} 已检查 {1} 个要素，执行规则：{2}，发现 {3} 个问题。", layerName, featureCount, ruleSummary, issueCount);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }
    }
}
