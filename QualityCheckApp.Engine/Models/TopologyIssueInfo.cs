using System;

namespace QualityCheckApp.Engine.Models
{
    public class TopologyIssueInfo
    {
        public TopologyIssueInfo()
        {
            RuleName = string.Empty;
            Description = string.Empty;
            GdbPath = string.Empty;
            DatasetName = string.Empty;
            LayerName = string.Empty;
        }

        public string RuleName { get; set; }

        public string Description { get; set; }

        public string GdbPath { get; set; }

        public string DatasetName { get; set; }

        public string LayerName { get; set; }

        public int FeatureId { get; set; }

        public int? RelatedFeatureId { get; set; }

        public bool HasFocusExtent { get; set; }

        public double FocusXMin { get; set; }

        public double FocusYMin { get; set; }

        public double FocusXMax { get; set; }

        public double FocusYMax { get; set; }
    }
}
