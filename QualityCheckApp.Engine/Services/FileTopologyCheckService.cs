using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class FileTopologyCheckService : ITopologyCheckService
    {
        public Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            return Task.Run(() => CheckLayer(layerInfo, cancellationToken), cancellationToken);
        }

        private static TopologyCheckResult CheckLayer(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath))
            {
                throw new ArgumentException("缺少 .gdb 路径，无法执行目录检查。", "layerInfo");
            }

            var result = new TopologyCheckResult
            {
                LayerName = layerInfo.LayerName,
                RuleSummary = "目录存在、核心元数据、表文件、索引文件"
            };

            var issues = new List<TopologyIssueInfo>();
            var issueIndex = 1;
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(layerInfo.GdbPath))
            {
                issues.Add(CreateIssue(layerInfo, issueIndex++, "目录缺失", ".gdb 目录不存在或已被删除。"));
            }
            else
            {
                var files = Directory.GetFiles(layerInfo.GdbPath, "*", SearchOption.TopDirectoryOnly);
                var tableFiles = Directory.GetFiles(layerInfo.GdbPath, "*.gdbtable", SearchOption.TopDirectoryOnly);
                var indexFiles = Directory.GetFiles(layerInfo.GdbPath, "*.gdbindexes", SearchOption.TopDirectoryOnly);

                result.FeatureCount = files.Length;

                if (files.Length == 0)
                {
                    issues.Add(CreateIssue(layerInfo, issueIndex++, "空目录", ".gdb 目录中没有发现任何文件。"));
                }

                if (!File.Exists(Path.Combine(layerInfo.GdbPath, "gdb")))
                {
                    issues.Add(CreateIssue(layerInfo, issueIndex++, "缺少核心元数据", "未发现 gdb 元数据文件。"));
                }

                if (!File.Exists(Path.Combine(layerInfo.GdbPath, "timestamps")))
                {
                    issues.Add(CreateIssue(layerInfo, issueIndex++, "缺少时间戳文件", "未发现 timestamps 文件。"));
                }

                if (tableFiles.Length == 0)
                {
                    issues.Add(CreateIssue(layerInfo, issueIndex++, "缺少表文件", "未发现任何 .gdbtable 文件，无法作为有效的 File Geodatabase 使用。"));
                }

                if (indexFiles.Length == 0)
                {
                    issues.Add(CreateIssue(layerInfo, issueIndex++, "缺少索引文件", "未发现任何 .gdbindexes 文件，目录结构可能不完整。"));
                }
            }

            result.Issues = issues;
            result.Summary = BuildSummary(layerInfo.LayerName, result.RuleSummary, issues.Count);
            return result;
        }

        private static TopologyIssueInfo CreateIssue(GdbLayerInfo layerInfo, int featureId, string ruleName, string description)
        {
            return new TopologyIssueInfo
            {
                RuleName = ruleName,
                Description = description,
                GdbPath = layerInfo.GdbPath,
                DatasetName = layerInfo.DatasetName,
                LayerName = layerInfo.LayerName,
                FeatureId = featureId,
                RelatedFeatureId = null,
                HasFocusExtent = false
            };
        }

        private static string BuildSummary(string layerName, string ruleSummary, int issueCount)
        {
            return string.Format("容器 {0} 已完成目录检查，执行项：{1}，发现 {2} 个问题。", layerName, ruleSummary, issueCount);
        }
    }
}
