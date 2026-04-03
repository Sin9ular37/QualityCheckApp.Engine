using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;

using QualityCheckApp.Engine.Infrastructure;
using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class ArcGisTopologyCheckService : ITopologyCheckService
    {
        private static readonly Dictionary<string, string> ProblemTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Null geometry", "空几何" },
            { "Short segment", "短线段" },
            { "Incorrect ring ordering", "环顺序错误" },
            { "Incorrect segment orientation", "线段方向错误" },
            { "Self intersections", "自相交" },
            { "Self-intersections", "自相交" },
            { "Unclosed rings", "环未闭合" },
            { "Empty parts", "空部件" },
            { "Duplicate vertex", "重复顶点" },
            { "Discontinuous parts", "不连续部件" },
            { "Bad envelope", "包络框异常" },
            { "Bad dataset extent", "数据集范围异常" },
            { "Geometry has no Z values", "缺少 Z 值" },
            { "Geometry has no M values", "缺少 M 值" },
            { "Empty Z values", "存在空 Z 值" },
            { "Empty M values", "存在空 M 值" },
            { "Mismatched attributes", "属性不匹配" },
            { "SE_INVALID_ENTITY_TYPE", "实体类型无效" },
            { "SE_INVALID_SHAPE_OBJECT", "图形对象无效" },
            { "SE_INVALID_PART_SEPARATOR", "部件分隔符无效" },
            { "SE_INVALID_POLYGON_CLOSURE", "面闭合无效" },
            { "SE_INVALID_OUTER_SHELL", "外壳无效" },
            { "SE_ZERO_AREA_POLYGON", "零面积面" },
            { "SE_POLYGON_HAS_VERTICAL_LINE", "面包含垂直线" },
            { "SE_OUTER_SHELLS_OVERLAP", "外壳重叠" },
            { "SE_SELF_INTERSECTING", "自相交" },
            { "SE_DUPLICATE_VERTEX", "重复顶点" },
            { "SE_INVALID_ENTITY", "实体无效" }
        };

        public Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken, IProgress<TopologyCheckProgressInfo> progress)
        {
            return StaTask.Run(() => CheckLayer(layerInfo, cancellationToken, progress), cancellationToken);
        }

        private static TopologyCheckResult CheckLayer(GdbLayerInfo layerInfo, CancellationToken token, IProgress<TopologyCheckProgressInfo> progress)
        {
            if (layerInfo == null)
            {
                throw new ArgumentNullException("layerInfo");
            }

            if (string.IsNullOrWhiteSpace(layerInfo.GdbPath) || string.IsNullOrWhiteSpace(layerInfo.DatasetName))
            {
                throw new ArgumentException("缺少 .gdb 路径或数据集信息，无法执行几何检查。", "layerInfo");
            }

            IWorkspaceFactory gdbFactory = null;
            IWorkspace workspace = null;
            IFeatureWorkspace featureWorkspace = null;
            IFeatureClass featureClass = null;

            var tempDirectory = string.Empty;

            try
            {
                ReportProgress(progress, 5, "正在打开地理数据库...");
                gdbFactory = new FileGDBWorkspaceFactoryClass();
                workspace = gdbFactory.OpenFromFile(layerInfo.GdbPath, 0);
                featureWorkspace = (IFeatureWorkspace)workspace;

                ReportProgress(progress, 12, "正在打开目标图层...");
                featureClass = featureWorkspace.OpenFeatureClass(layerInfo.DatasetName);

                var result = new TopologyCheckResult();
                result.LayerName = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName;
                result.FeatureCount = featureClass.FeatureCount(null);
                result.RuleSummary = "ArcGIS Check Geometry 官方几何质量检查";

                if (result.FeatureCount == 0)
                {
                    result.Summary = string.Format("图层 {0} 没有可检测要素。", result.LayerName);
                    result.Issues = new List<TopologyIssueInfo>();
                    ReportProgress(progress, 100, "图层没有可检测要素。");
                    return result;
                }

                tempDirectory = CreateTempDirectory();
                var outputTablePath = System.IO.Path.Combine(tempDirectory, "check_geometry_result.dbf");

                ReportProgress(progress, 30, "正在执行 ArcGIS Check Geometry...");
                ExecuteCheckGeometry(BuildInputFeatureClassPath(layerInfo), outputTablePath);

                ReportProgress(progress, 72, "正在读取官方几何检查结果...");
                var issues = ReadIssues(outputTablePath, featureClass, layerInfo, token, progress);
                result.Issues = issues;
                result.Summary = BuildSummary(result.LayerName, result.FeatureCount, result.IssueCount);
                ReportProgress(progress, 100, string.Format("几何检查完成：共发现 {0} 个问题。", result.IssueCount));
                return result;
            }
            finally
            {
                SafeDeleteDirectory(tempDirectory);
                ReleaseComObject(featureClass);
                ReleaseComObject(featureWorkspace);
                ReleaseComObject(workspace);
                ReleaseComObject(gdbFactory);
            }
        }

        private static void ExecuteCheckGeometry(string inputFeatureClassPath, string outputTablePath)
        {
            Geoprocessor geoprocessor = null;
            CheckGeometry tool = null;
            IGeoProcessorResult2 result = null;

            try
            {
                geoprocessor = new Geoprocessor();
                geoprocessor.OverwriteOutput = true;

                tool = new CheckGeometry();
                tool.in_features = inputFeatureClassPath;
                tool.out_table = outputTablePath;

                result = (IGeoProcessorResult2)geoprocessor.Execute(tool, null);
                if (result == null)
                {
                    throw new InvalidOperationException("Check Geometry 未返回执行结果。");
                }

                if (result.Status != esriJobStatus.esriJobSucceeded)
                {
                    throw new InvalidOperationException(BuildGeoprocessorMessage(result));
                }
            }
            finally
            {
                ReleaseComObject(result);
                ReleaseComObject(tool);
                ReleaseComObject(geoprocessor);
            }
        }

        private static List<TopologyIssueInfo> ReadIssues(string outputTablePath, IFeatureClass featureClass, GdbLayerInfo layerInfo, CancellationToken token, IProgress<TopologyCheckProgressInfo> progress)
        {
            IWorkspaceFactory tableFactory = null;
            IWorkspace tableWorkspace = null;
            IFeatureWorkspace tableFeatureWorkspace = null;
            ITable issueTable = null;
            ICursor cursor = null;
            IRow row = null;

            var issues = new List<TopologyIssueInfo>();

            try
            {
                tableFactory = new ShapefileWorkspaceFactoryClass();
                tableWorkspace = tableFactory.OpenFromFile(System.IO.Path.GetDirectoryName(outputTablePath), 0);
                tableFeatureWorkspace = (IFeatureWorkspace)tableWorkspace;
                issueTable = tableFeatureWorkspace.OpenTable(System.IO.Path.GetFileName(outputTablePath));

                var classFieldIndex = issueTable.FindField("CLASS");
                var featureIdFieldIndex = issueTable.FindField("FEATURE_ID");
                var problemFieldIndex = issueTable.FindField("PROBLEM");
                var totalIssueCount = issueTable.RowCount(null);

                if (totalIssueCount <= 0)
                {
                    ReportProgress(progress, 95, "未发现几何问题，正在整理检测结果...");
                }

                cursor = issueTable.Search(null, false);
                var issueCount = 0;
                while ((row = cursor.NextRow()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    issueCount++;
                    if (issueCount == 1 || issueCount % 10 == 0 || issueCount == totalIssueCount)
                    {
                        ReportReadProgress(progress, issueCount, totalIssueCount);
                    }

                    var featureId = ReadIntValue(row, featureIdFieldIndex);
                    var problem = ReadStringValue(row, problemFieldIndex);
                    var className = ReadStringValue(row, classFieldIndex);

                    IFeature feature = null;
                    IGeometry focusGeometry = null;
                    try
                    {
                        if (featureId >= 0)
                        {
                            feature = featureClass.GetFeature(featureId);
                            if (feature != null)
                            {
                                focusGeometry = feature.ShapeCopy;
                            }
                        }

                        issues.Add(CreateIssue(layerInfo, className, problem, featureId, focusGeometry));
                    }
                    finally
                    {
                        ReleaseComObject(focusGeometry);
                        ReleaseComObject(feature);
                        ReleaseComObject(row);
                        row = null;
                    }
                }
            }
            finally
            {
                ReleaseComObject(row);
                ReleaseComObject(cursor);
                ReleaseComObject(issueTable);
                ReleaseComObject(tableFeatureWorkspace);
                ReleaseComObject(tableWorkspace);
                ReleaseComObject(tableFactory);
            }

            return issues;
        }

        private static void ReportReadProgress(IProgress<TopologyCheckProgressInfo> progress, int issueCount, int totalIssueCount)
        {
            if (progress == null)
            {
                return;
            }

            if (totalIssueCount <= 0)
            {
                ReportProgress(progress, 95, "未发现几何问题，正在整理检测结果...");
                return;
            }

            var boundedIssueCount = issueCount > totalIssueCount ? totalIssueCount : issueCount;
            var percent = 72 + (int)Math.Round((double)boundedIssueCount * 23 / totalIssueCount);
            if (percent > 95)
            {
                percent = 95;
            }

            ReportProgress(progress, percent, string.Format("正在整理检查结果：已读取 {0}/{1} 条问题记录...", boundedIssueCount, totalIssueCount));
        }

        private static void ReportProgress(IProgress<TopologyCheckProgressInfo> progress, int percent, string message)
        {
            if (progress == null)
            {
                return;
            }

            progress.Report(new TopologyCheckProgressInfo(percent, message));
        }

        private static TopologyIssueInfo CreateIssue(GdbLayerInfo layerInfo, string className, string problem, int featureId, IGeometry focusGeometry)
        {
            var translatedProblem = TranslateProblem(problem);
            var issue = new TopologyIssueInfo
            {
                RuleName = translatedProblem,
                Description = BuildDescription(className, problem, translatedProblem),
                GdbPath = layerInfo.GdbPath,
                DatasetName = layerInfo.DatasetName,
                LayerName = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName,
                FeatureId = featureId,
                RelatedFeatureId = null
            };

            if (focusGeometry != null && !focusGeometry.IsEmpty)
            {
                IEnvelope envelope = null;
                try
                {
                    envelope = focusGeometry.Envelope;
                    if (envelope != null && !envelope.IsEmpty)
                    {
                        double xMin;
                        double yMin;
                        double xMax;
                        double yMax;
                        envelope.QueryCoords(out xMin, out yMin, out xMax, out yMax);
                        issue.HasFocusExtent = true;
                        issue.FocusXMin = xMin;
                        issue.FocusYMin = yMin;
                        issue.FocusXMax = xMax;
                        issue.FocusYMax = yMax;
                    }
                }
                finally
                {
                    ReleaseComObject(envelope);
                }
            }

            return issue;
        }

        private static string BuildDescription(string className, string problem, string translatedProblem)
        {
            if (string.IsNullOrWhiteSpace(problem))
            {
                return "ArcGIS Check Geometry 返回了未命名的几何质量问题。";
            }

            if (!string.IsNullOrWhiteSpace(className) && !string.IsNullOrWhiteSpace(problem))
            {
                if (!string.Equals(translatedProblem, problem, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”（原始值：{1}），来源对象类别为“{2}”。", translatedProblem, problem, className);
                }

                return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”，来源对象类别为“{1}”。", translatedProblem, className);
            }

            if (!string.Equals(translatedProblem, problem, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”（原始值：{1}）。", translatedProblem, problem);
            }

            return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”。", translatedProblem);
        }

        private static string TranslateProblem(string problem)
        {
            if (string.IsNullOrWhiteSpace(problem))
            {
                return "几何质量问题";
            }

            string translated;
            if (ProblemTranslations.TryGetValue(problem.Trim(), out translated))
            {
                return translated;
            }

            return problem;
        }

        private static string BuildInputFeatureClassPath(GdbLayerInfo layerInfo)
        {
            return System.IO.Path.Combine(layerInfo.GdbPath, layerInfo.DatasetName);
        }

        private static string BuildSummary(string layerName, int featureCount, int issueCount)
        {
            return string.Format("图层 {0} 已按 ArcGIS Check Geometry 官方工具检查 {1} 个要素，发现 {2} 个问题。", layerName, featureCount, issueCount);
        }

        private static string BuildGeoprocessorMessage(IGeoProcessorResult2 result)
        {
            if (result == null)
            {
                return "ArcGIS Geoprocessor 执行失败。";
            }

            var messages = new List<string>();
            for (var index = 0; index < result.MessageCount; index++)
            {
                var message = result.GetMessage(index);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    messages.Add(message);
                }
            }

            if (messages.Count == 0)
            {
                return "ArcGIS Check Geometry 执行失败，但未返回详细信息。";
            }

            return string.Join(" ", messages);
        }

        private static int ReadIntValue(IRow row, int fieldIndex)
        {
            if (row == null || fieldIndex < 0)
            {
                return -1;
            }

            var value = row.get_Value(fieldIndex);
            if (value == null || value == DBNull.Value)
            {
                return -1;
            }

            int parsedValue;
            return int.TryParse(Convert.ToString(value), out parsedValue) ? parsedValue : -1;
        }

        private static string ReadStringValue(IRow row, int fieldIndex)
        {
            if (row == null || fieldIndex < 0)
            {
                return string.Empty;
            }

            var value = row.get_Value(fieldIndex);
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
        }

        private static string CreateTempDirectory()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QualityCheckApp.Engine", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void SafeDeleteDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }
    }
}
