using System;
using System.IO;
using System.Text;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class TopologyReportExportService
    {
        public void Export(string filePath, string zipPath, int gdbCount, string packageMode, string packageFormatWarning, string structureSummary, TopologyCheckResult topologyResult)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("未指定报告输出路径。", "filePath");
            }

            if (topologyResult == null)
            {
                throw new ArgumentNullException("topologyResult");
            }

            var builder = new StringBuilder();
            builder.AppendLine("质量检查报告");
            builder.AppendLine(new string('=', 60));
            builder.AppendLine(string.Format("导出时间：{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            builder.AppendLine(string.Format("输入压缩包：{0}", string.IsNullOrWhiteSpace(zipPath) ? "未记录" : zipPath));
            builder.AppendLine(string.Format("识别到的 .gdb 数量：{0}", gdbCount));
            builder.AppendLine(string.Format("压缩包模式：{0}", string.IsNullOrWhiteSpace(packageMode) ? "未识别" : packageMode));
            builder.AppendLine(string.Format("结构摘要：{0}", string.IsNullOrWhiteSpace(structureSummary) ? "无" : structureSummary));
            if (!string.IsNullOrWhiteSpace(packageFormatWarning))
            {
                builder.AppendLine(string.Format("格式提示：{0}", packageFormatWarning));
            }

            builder.AppendLine();
            builder.AppendLine("拓扑检测结果");
            builder.AppendLine(new string('-', 60));
            builder.AppendLine(string.Format("图层：{0}", topologyResult.LayerName));
            builder.AppendLine(string.Format("检查规则：{0}", topologyResult.RuleSummary));
            builder.AppendLine(string.Format("摘要：{0}", topologyResult.Summary));
            builder.AppendLine(string.Format("问题数量：{0}", topologyResult.IssueCount));
            builder.AppendLine();
            builder.AppendLine("问题明细");
            builder.AppendLine(new string('-', 60));

            if (topologyResult.Issues == null || topologyResult.Issues.Count == 0)
            {
                builder.AppendLine("未发现拓扑问题。");
            }
            else
            {
                WriteIssues(builder, topologyResult);
            }

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(true));
        }

        private static void WriteIssues(StringBuilder builder, TopologyCheckResult topologyResult)
        {
            for (var index = 0; index < topologyResult.Issues.Count; index++)
            {
                var issue = topologyResult.Issues[index];
                builder.AppendLine(string.Format("{0}. 规则：{1}", index + 1, issue.RuleName));
                builder.AppendLine(string.Format("   图层：{0}", issue.LayerName));
                builder.AppendLine(string.Format("   要素OID：{0}", issue.FeatureId));
                if (issue.RelatedFeatureId.HasValue)
                {
                    builder.AppendLine(string.Format("   相关OID：{0}", issue.RelatedFeatureId.Value));
                }
                builder.AppendLine(string.Format("   说明：{0}", issue.Description));
                if (issue.HasFocusExtent)
                {
                    builder.AppendLine(string.Format("   问题范围：XMin={0:0.###}, YMin={1:0.###}, XMax={2:0.###}, YMax={3:0.###}",
                        issue.FocusXMin,
                        issue.FocusYMin,
                        issue.FocusXMax,
                        issue.FocusYMax));
                }

                builder.AppendLine();
            }
        }
    }
}
