namespace QualityCheckApp.Engine.Models
{
    public class WktValidationResult
    {
        public WktValidationResult()
        {
            GeometryType = string.Empty;
            Summary = string.Empty;
            Detail = string.Empty;
        }

        public bool IsValid { get; set; }

        public bool IsSimple { get; set; }

        public bool IsEmpty { get; set; }

        public string GeometryType { get; set; }

        public int GeometryCount { get; set; }

        public int PointCount { get; set; }

        public string Summary { get; set; }

        public string Detail { get; set; }
    }
}
