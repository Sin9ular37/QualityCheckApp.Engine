namespace QualityCheckApp.Engine.Models
{
    public class PackageFormatInspectionResult
    {
        public PackageFormatInspectionResult()
        {
            PackageMode = string.Empty;
            StructureSummary = string.Empty;
            WarningMessage = string.Empty;
        }

        public string PackageMode { get; set; }

        public string StructureSummary { get; set; }

        public string WarningMessage { get; set; }

        public bool IsStandardFormat { get; set; }
    }
}
