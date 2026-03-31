using System;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class NtsWktValidationService
    {
        private readonly WKTReader _reader;

        public NtsWktValidationService()
        {
            _reader = new WKTReader();
        }

        public WktValidationResult Validate(string wkt)
        {
            if (string.IsNullOrWhiteSpace(wkt))
            {
                throw new ArgumentException("请输入需要验证的 WKT。", "wkt");
            }

            Geometry geometry = _reader.Read(wkt);
            var validity = new IsValidOp(geometry);
            var error = validity.ValidationError;
            var result = new WktValidationResult();

            result.IsValid = error == null;
            result.IsSimple = geometry.IsSimple;
            result.IsEmpty = geometry.IsEmpty;
            result.GeometryType = geometry.GeometryType;
            result.GeometryCount = geometry.NumGeometries;
            result.PointCount = geometry.NumPoints;
            result.Summary = result.IsValid
                ? "NetTopologySuite 校验通过：几何有效。"
                : string.Format("NetTopologySuite 校验失败：{0}", error.Message);
            result.Detail = BuildDetail(geometry, error);

            return result;
        }

        private static string BuildDetail(Geometry geometry, TopologyValidationError error)
        {
            var detail = string.Format("类型：{0}；部件数：{1}；顶点数：{2}；Simple：{3}；Empty：{4}",
                geometry.GeometryType,
                geometry.NumGeometries,
                geometry.NumPoints,
                geometry.IsSimple ? "是" : "否",
                geometry.IsEmpty ? "是" : "否");

            if (error != null && error.Coordinate != null)
            {
                detail = string.Format("{0}；问题坐标：({1:0.###}, {2:0.###})",
                    detail,
                    error.Coordinate.X,
                    error.Coordinate.Y);
            }

            return detail;
        }
    }
}
