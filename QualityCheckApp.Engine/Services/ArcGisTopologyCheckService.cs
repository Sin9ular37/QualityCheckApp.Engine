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
        public Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken, IProgress<string> progress)
        {
            return StaTask.Run(() => CheckLayer(layerInfo, cancellationToken, progress), cancellationToken);
        }

        private static TopologyCheckResult CheckLayer(GdbLayerInfo layerInfo, CancellationToken token, IProgress<string> progress)
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
                progress.Report("正在打开地理数据库...");
                gdbFactory = new FileGDBWorkspaceFactoryClass();
                workspace = gdbFactory.OpenFromFile(layerInfo.GdbPath, 0);
                featureWorkspace = (IFeatureWorkspace)workspace;

                progress.Report("正在打开目标图层...");
                featureClass = featureWorkspace.OpenFeatureClass(layerInfo.DatasetName);

                var result = new TopologyCheckResult();
                result.LayerName = string.IsNullOrWhiteSpace(layerInfo.LayerName) ? layerInfo.DatasetName : layerInfo.LayerName;
                result.FeatureCount = featureClass.FeatureCount(null);
                result.RuleSummary = "ArcGIS Check Geometry 官方几何质量检查";

                if (result.FeatureCount == 0)
                {
                    result.Summary = string.Format("图层 {0} 没有可检测要素。", result.LayerName);
                    result.Issues = new List<TopologyIssueInfo>();
                    return result;
                }

                tempDirectory = CreateTempDirectory();
                var outputTablePath = System.IO.Path.Combine(tempDirectory, "check_geometry_result.dbf");

                progress.Report("正在执行 ArcGIS Check Geometry...");
                ExecuteCheckGeometry(BuildInputFeatureClassPath(layerInfo), outputTablePath);

                progress.Report("正在读取官方几何检查结果...");
                var issues = ReadIssues(outputTablePath, featureClass, layerInfo, token, progress);
                result.Issues = issues;
                result.Summary = BuildSummary(result.LayerName, result.FeatureCount, result.IssueCount);
                progress.Report(string.Format("几何检查完成：共发现 {0} 个问题。", result.IssueCount));
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

        private static List<TopologyIssueInfo> ReadIssues(string outputTablePath, IFeatureClass featureClass, GdbLayerInfo layerInfo, CancellationToken token, IProgress<string> progress)
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

                cursor = issueTable.Search(null, false);
                var issueCount = 0;
                while ((row = cursor.NextRow()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    issueCount++;
                    if (issueCount == 1 || issueCount % 10 == 0)
                    {
                        progress.Report(string.Format("正在整理检查结果，当前已读取 {0} 条问题记录...", issueCount));
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

        private static TopologyIssueInfo CreateIssue(GdbLayerInfo layerInfo, string className, string problem, int featureId, IGeometry focusGeometry)
        {
            var issue = new TopologyIssueInfo
            {
                RuleName = string.IsNullOrWhiteSpace(problem) ? "几何质量问题" : problem,
                Description = BuildDescription(className, problem),
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

        private static string BuildDescription(string className, string problem)
        {
            if (!string.IsNullOrWhiteSpace(className) && !string.IsNullOrWhiteSpace(problem))
            {
                return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”，来源对象类别为“{1}”。", problem, className);
            }

            if (!string.IsNullOrWhiteSpace(problem))
            {
                return string.Format("ArcGIS Check Geometry 返回的问题类型为“{0}”。", problem);
            }

            return "ArcGIS Check Geometry 返回了未命名的几何质量问题。";
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
