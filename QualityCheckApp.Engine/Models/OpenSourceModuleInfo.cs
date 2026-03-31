namespace QualityCheckApp.Engine.Models
{
    public class OpenSourceModuleInfo
    {
        public OpenSourceModuleInfo()
        {
            ModuleName = string.Empty;
            PlannedLibrary = string.Empty;
            Responsibility = string.Empty;
            Status = string.Empty;
            NextStep = string.Empty;
        }

        public string ModuleName { get; set; }

        public string PlannedLibrary { get; set; }

        public string Responsibility { get; set; }

        public string Status { get; set; }

        public string NextStep { get; set; }

        public bool IsScaffolded { get; set; }
    }
}
