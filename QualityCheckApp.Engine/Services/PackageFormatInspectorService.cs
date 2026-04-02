using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class PackageFormatInspectorService
    {
        private static readonly string[] AllowedDocumentExtensions = { ".pdf", ".doc", ".docx", ".wps" };

        public PackageFormatInspectionResult Inspect(string zipPath, string extractionRoot, IReadOnlyList<string> gdbDirectories)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                throw new ArgumentException("未指定压缩包路径。", "zipPath");
            }

            if (string.IsNullOrWhiteSpace(extractionRoot))
            {
                throw new ArgumentException("未指定解压目录。", "extractionRoot");
            }

            var issues = new List<string>();
            ValidateZipFileName(zipPath, issues);

            var topLevelDirectories = Directory.GetDirectories(extractionRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            var topLevelFiles = Directory.GetFiles(extractionRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (topLevelFiles.Count > 0)
            {
                issues.Add(string.Format("压缩包根目录只允许 3 个文件夹，当前发现额外文件：{0}。", string.Join("、", topLevelFiles)));
            }

            ValidateTopLevelDirectories(extractionRoot, topLevelDirectories, issues);

            ValidateDocumentFolder(extractionRoot, "1政策法规", issues);
            ValidateDocumentFolder(extractionRoot, "2数据库标准", issues);
            ValidateGdbFolder(extractionRoot, "3数据库文件", issues);

            var result = new PackageFormatInspectionResult();
            if (issues.Count == 0)
            {
                result.IsStandardFormat = true;
                result.PackageMode = "标准汇交成果包";
                result.StructureSummary = "目录格式校验通过：压缩包名称、3 个顶层目录及目录内容均符合汇交标准。";
                return result;
            }

            result.IsStandardFormat = false;
            result.PackageMode = "非标准汇交包";
            result.StructureSummary = "目录格式不符合汇交标准，但程序仍继续执行 .gdb 搜索与后续检查。";
            result.WarningMessage = BuildWarningMessage(issues, gdbDirectories);

            return result;
        }

        private static void ValidateZipFileName(string zipPath, IList<string> issues)
        {
            var fileName = Path.GetFileName(zipPath);
            if (!string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("输入文件不是 .zip 压缩包。");
            }

            if (fileName.IndexOf("数据汇交成果", StringComparison.OrdinalIgnoreCase) < 0)
            {
                issues.Add(string.Format("压缩包文件名必须包含“数据汇交成果”，当前文件名为：{0}。", fileName));
            }
        }

        private static void ValidateTopLevelDirectories(string extractionRoot, IList<string> topLevelDirectories, IList<string> issues)
        {
            var expectedDirectories = new[] { "1政策法规", "2数据库标准", "3数据库文件" };

            foreach (var expectedDirectory in expectedDirectories)
            {
                if (!topLevelDirectories.Contains(expectedDirectory, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(string.Format("压缩包根目录缺少文件夹：{0}。", expectedDirectory));
                }
            }

            foreach (var actualDirectory in topLevelDirectories)
            {
                if (!expectedDirectories.Contains(actualDirectory, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(string.Format("压缩包根目录存在非标准文件夹：{0}。", actualDirectory));
                }
            }

            if (topLevelDirectories.Count != expectedDirectories.Length)
            {
                issues.Add(string.Format("压缩包根目录必须且只允许包含 3 个文件夹，当前识别到 {0} 个。", topLevelDirectories.Count));
            }
        }

        private static void ValidateDocumentFolder(string extractionRoot, string folderName, IList<string> issues)
        {
            var folderPath = Path.Combine(extractionRoot, folderName);
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            var subDirectories = Directory.GetDirectories(folderPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            if (subDirectories.Count > 0)
            {
                issues.Add(string.Format("{0} 中只允许直接存放文档文件，不允许子文件夹：{1}。", folderName, string.Join("、", subDirectories)));
            }

            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (!AllowedDocumentExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(string.Format("{0} 中存在不允许的文件类型：{1}。仅允许 .pdf、.doc、.docx、.wps。", folderName, Path.GetFileName(file)));
                }
            }
        }

        private static void ValidateGdbFolder(string extractionRoot, string folderName, IList<string> issues)
        {
            var folderPath = Path.Combine(extractionRoot, folderName);
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            var childDirectories = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            var childFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            foreach (var childDirectory in childDirectories)
            {
                if (!childDirectory.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(string.Format("{0} 中只允许 .gdb 目录，当前存在不允许的目录：{1}。", folderName, childDirectory));
                }
            }

            foreach (var childFile in childFiles)
            {
                issues.Add(string.Format("{0} 中只允许 .gdb 目录，当前存在不允许的文件：{1}。", folderName, childFile));
            }

            if (childDirectories.Count == 0)
            {
                issues.Add(string.Format("{0} 中未发现任何 .gdb 目录。", folderName));
            }
        }

        private static string BuildWarningMessage(IList<string> issues, IReadOnlyList<string> gdbDirectories)
        {
            var lines = new List<string>();
            for (var index = 0; index < issues.Count; index++)
            {
                lines.Add(string.Format("{0}. {1}", index + 1, issues[index]));
            }

            if (gdbDirectories != null && gdbDirectories.Count > 0)
            {
                lines.Add(string.Format("{0}. 虽然目录格式不符合标准，但程序已继续识别到 {1} 个 .gdb 目录。", lines.Count + 1, gdbDirectories.Count));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
