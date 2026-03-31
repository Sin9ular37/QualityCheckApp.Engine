using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Mapsui.Geometries;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using OSGeo.OGR;

using QualityCheckApp.Engine.Models;

using MapsuiLineString = Mapsui.Geometries.LineString;
using MapsuiLinearRing = Mapsui.Geometries.LinearRing;
using MapsuiPoint = Mapsui.Geometries.Point;
using MapsuiPolygon = Mapsui.Geometries.Polygon;
using MapsuiGeometry = Mapsui.Geometries.Geometry;

using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsGeometryCollection = NetTopologySuite.Geometries.GeometryCollection;
using NtsLineString = NetTopologySuite.Geometries.LineString;
using NtsMultiLineString = NetTopologySuite.Geometries.MultiLineString;
using NtsMultiPoint = NetTopologySuite.Geometries.MultiPoint;
using NtsMultiPolygon = NetTopologySuite.Geometries.MultiPolygon;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace QualityCheckApp.Engine.Services
{
    public class GdalMapsuiPreviewService
    {
        private const int MaxPreviewFeatures = 800;
        private static readonly WKTReader WktReader = new WKTReader();

        public Task<MapPreviewLoadResult> LoadPreviewAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            return Task.Run(() => LoadPreview(layerInfo, cancellationToken), cancellationToken);
        }

        private static MapPreviewLoadResult LoadPreview(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath) || string.IsNullOrWhiteSpace(layerInfo.DatasetName))
            {
                throw new ArgumentException("缺少图层路径或数据集信息，无法加载预览。", "layerInfo");
            }

            GdalRuntimeBootstrapper.EnsureInitialized();

            var points = new List<MapsuiGeometry>();
            var lines = new List<MapsuiGeometry>();
            var polygons = new List<MapsuiGeometry>();

            using (var dataSource = Ogr.Open(layerInfo.GdbPath, 0))
            {
                if (dataSource == null)
                {
                    throw new InvalidOperationException("GDAL/OGR 无法打开当前 File Geodatabase。");
                }

                using (var layer = dataSource.GetLayerByName(layerInfo.DatasetName))
                {
                    if (layer == null)
                    {
                        throw new InvalidOperationException("GDAL/OGR 未找到当前选中的图层。");
                    }

                    var totalFeatureCount = NormalizeFeatureCount(layer.GetFeatureCount(1));
                    var loadedFeatureCount = 0;

                    layer.ResetReading();
                    Feature feature;
                    while ((feature = layer.GetNextFeature()) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (feature)
                        {
                            var ogrGeometry = feature.GetGeometryRef();
                            if (ogrGeometry == null || ogrGeometry.IsEmpty())
                            {
                                continue;
                            }

                            string wkt;
                            if (ogrGeometry.ExportToWkt(out wkt) != 0 || string.IsNullOrWhiteSpace(wkt))
                            {
                                continue;
                            }

                            NtsGeometry geometry;
                            try
                            {
                                geometry = WktReader.Read(wkt);
                            }
                            catch
                            {
                                continue;
                            }

                            if (geometry == null || geometry.IsEmpty)
                            {
                                continue;
                            }

                            AppendGeometry(geometry, points, lines, polygons);
                            loadedFeatureCount++;

                            if (loadedFeatureCount >= MaxPreviewFeatures)
                            {
                                break;
                            }
                        }
                    }

                    var result = new MapPreviewLoadResult
                    {
                        LayerName = layerInfo.LayerName,
                        TotalFeatureCount = totalFeatureCount,
                        LoadedFeatureCount = loadedFeatureCount,
                        IsTruncated = totalFeatureCount > loadedFeatureCount,
                        PointGeometries = points,
                        LineGeometries = lines,
                        PolygonGeometries = polygons,
                        Summary = BuildSummary(layerInfo.LayerName, totalFeatureCount, loadedFeatureCount, points.Count, lines.Count, polygons.Count)
                    };

                    return result;
                }
            }
        }

        private static int NormalizeFeatureCount(long featureCount)
        {
            if (featureCount < 0)
            {
                return 0;
            }

            if (featureCount > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)featureCount;
        }

        private static string BuildSummary(string layerName, int totalFeatureCount, int loadedFeatureCount, int pointCount, int lineCount, int polygonCount)
        {
            var summary = string.Format("已渲染图层 {0} 的真实几何：读取 {1}/{2} 个要素，点 {3} 个、线 {4} 个、面 {5} 个。",
                layerName,
                loadedFeatureCount,
                totalFeatureCount,
                pointCount,
                lineCount,
                polygonCount);

            if (loadedFeatureCount < totalFeatureCount)
            {
                summary = summary + string.Format(" 为保证响应速度，当前预览最多加载前 {0} 个要素。", MaxPreviewFeatures);
            }

            return summary;
        }

        private static void AppendGeometry(NtsGeometry geometry, IList<MapsuiGeometry> points, IList<MapsuiGeometry> lines, IList<MapsuiGeometry> polygons)
        {
            var point = geometry as NtsPoint;
            if (point != null)
            {
                points.Add(new MapsuiPoint(point.X, point.Y));
                return;
            }

            var multiPoint = geometry as NtsMultiPoint;
            if (multiPoint != null)
            {
                for (var index = 0; index < multiPoint.NumGeometries; index++)
                {
                    AppendGeometry((NtsGeometry)multiPoint.GetGeometryN(index), points, lines, polygons);
                }

                return;
            }

            var lineString = geometry as NtsLineString;
            if (lineString != null && !(geometry is NetTopologySuite.Geometries.LinearRing))
            {
                var lineGeometry = CreateLineString(lineString.Coordinates);
                if (lineGeometry != null)
                {
                    lines.Add(lineGeometry);
                }

                return;
            }

            var multiLine = geometry as NtsMultiLineString;
            if (multiLine != null)
            {
                for (var index = 0; index < multiLine.NumGeometries; index++)
                {
                    AppendGeometry((NtsGeometry)multiLine.GetGeometryN(index), points, lines, polygons);
                }

                return;
            }

            var polygon = geometry as NtsPolygon;
            if (polygon != null)
            {
                var polygonGeometry = CreatePolygon(polygon);
                if (polygonGeometry != null)
                {
                    polygons.Add(polygonGeometry);
                }

                return;
            }

            var multiPolygon = geometry as NtsMultiPolygon;
            if (multiPolygon != null)
            {
                for (var index = 0; index < multiPolygon.NumGeometries; index++)
                {
                    AppendGeometry((NtsGeometry)multiPolygon.GetGeometryN(index), points, lines, polygons);
                }

                return;
            }

            var collection = geometry as NtsGeometryCollection;
            if (collection != null)
            {
                for (var index = 0; index < collection.NumGeometries; index++)
                {
                    AppendGeometry((NtsGeometry)collection.GetGeometryN(index), points, lines, polygons);
                }
            }
        }

        private static MapsuiLineString CreateLineString(IReadOnlyList<NtsCoordinate> coordinates)
        {
            var points = ToMapsuiPoints(coordinates, false);
            return points.Count < 2 ? null : new MapsuiLineString(points);
        }

        private static MapsuiPolygon CreatePolygon(NtsPolygon polygon)
        {
            var exterior = ToMapsuiPoints(polygon.ExteriorRing.Coordinates, true);
            if (exterior.Count < 4)
            {
                return null;
            }

            var holes = new List<MapsuiLinearRing>();
            for (var index = 0; index < polygon.NumInteriorRings; index++)
            {
                var holePoints = ToMapsuiPoints(polygon.GetInteriorRingN(index).Coordinates, true);
                if (holePoints.Count >= 4)
                {
                    holes.Add(new MapsuiLinearRing(holePoints));
                }
            }

            return holes.Count == 0
                ? new MapsuiPolygon(new MapsuiLinearRing(exterior))
                : new MapsuiPolygon(new MapsuiLinearRing(exterior), holes);
        }

        private static List<MapsuiPoint> ToMapsuiPoints(IReadOnlyList<NtsCoordinate> coordinates, bool ensureClosed)
        {
            var points = new List<MapsuiPoint>();
            if (coordinates == null)
            {
                return points;
            }

            for (var index = 0; index < coordinates.Count; index++)
            {
                var coordinate = coordinates[index];
                points.Add(new MapsuiPoint(coordinate.X, coordinate.Y));
            }

            if (ensureClosed && points.Count > 0)
            {
                var first = points[0];
                var last = points[points.Count - 1];
                if (first.X != last.X || first.Y != last.Y)
                {
                    points.Add(new MapsuiPoint(first.X, first.Y));
                }
            }

            return points;
        }
    }
}
