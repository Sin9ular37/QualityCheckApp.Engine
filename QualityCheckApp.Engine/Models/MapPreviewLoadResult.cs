using System.Collections.Generic;

using Mapsui.Geometries;

namespace QualityCheckApp.Engine.Models
{
    public class MapPreviewLoadResult
    {
        private IReadOnlyList<Geometry> _pointGeometries;
        private IReadOnlyList<Geometry> _lineGeometries;
        private IReadOnlyList<Geometry> _polygonGeometries;

        public MapPreviewLoadResult()
        {
            LayerName = string.Empty;
            Summary = string.Empty;
            _pointGeometries = new List<Geometry>();
            _lineGeometries = new List<Geometry>();
            _polygonGeometries = new List<Geometry>();
        }

        public string LayerName { get; set; }

        public int TotalFeatureCount { get; set; }

        public int LoadedFeatureCount { get; set; }

        public bool IsTruncated { get; set; }

        public string Summary { get; set; }

        public IReadOnlyList<Geometry> PointGeometries
        {
            get { return _pointGeometries; }
            set { _pointGeometries = value ?? new List<Geometry>(); }
        }

        public IReadOnlyList<Geometry> LineGeometries
        {
            get { return _lineGeometries; }
            set { _lineGeometries = value ?? new List<Geometry>(); }
        }

        public IReadOnlyList<Geometry> PolygonGeometries
        {
            get { return _polygonGeometries; }
            set { _polygonGeometries = value ?? new List<Geometry>(); }
        }
    }
}
