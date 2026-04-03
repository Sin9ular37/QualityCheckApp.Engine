using System;

namespace QualityCheckApp.Engine.Models
{
    public class TopologyCheckProgressInfo
    {
        public TopologyCheckProgressInfo(int percent, string message)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            else if (percent > 100)
            {
                percent = 100;
            }

            Percent = percent;
            Message = message ?? string.Empty;
        }

        public int Percent { get; private set; }

        public string Message { get; private set; }
    }
}
