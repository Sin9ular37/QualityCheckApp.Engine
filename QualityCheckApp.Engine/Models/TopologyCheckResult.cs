using System.Collections.Generic;

namespace QualityCheckApp.Engine.Models
{
    public class TopologyCheckResult
    {
        private IReadOnlyList<TopologyIssueInfo> _issues;

        public TopologyCheckResult()
        {
            LayerName = string.Empty;
            RuleSummary = string.Empty;
            Summary = string.Empty;
            _issues = new List<TopologyIssueInfo>();
        }

        public string LayerName { get; set; }

        public int FeatureCount { get; set; }

        public string RuleSummary { get; set; }

        public string Summary { get; set; }

        public IReadOnlyList<TopologyIssueInfo> Issues
        {
            get { return _issues; }
            set { _issues = value ?? new List<TopologyIssueInfo>(); }
        }

        public int IssueCount
        {
            get { return Issues.Count; }
        }
    }
}
