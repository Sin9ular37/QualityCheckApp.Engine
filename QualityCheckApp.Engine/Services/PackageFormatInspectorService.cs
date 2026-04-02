using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class PackageFormatInspectorService
    {
        public PackageFormatInspectionResult Inspect(string extractionRoot, IReadOnlyList<string> gdbDirectories)
        {
            if (string.IsNullOrWhiteSpace(extractionRoot))
            {
                throw new ArgumentException("未指定解压目录。", "extractionRoot");
            }

            var inspectionRoot = ResolveInspectionRoot(extractionRoot);
            var topLevelDirectories = Directory.GetDirectories(inspectionRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var hasFirstDocumentFolder = topLevelDirectories.Any(IsFirstDocumentFolder);
            var hasSecondDocumentFolder = topLevelDirectories.Any(IsSecondDocumentFolder);
            var hasGdbFolder = topLevelDirectories.Any(IsGdbFolder);

            var result = new PackageFormatInspectionResult();
            if (hasFirstDocumentFolder && hasSecondDocumentFolder && hasGdbFolder)
            {
                result.IsStandardFormat = true;
                result.PackageMode = "标准汇交成果包";
                result.StructureSummary = "目录格式校验通过：已识别 1类文档目录、2类文档目录 和 3类 gdb 目录。";
                return result;
            }

            result.IsStandardFormat = false;
            result.PackageMode = "非标准汇交包";
            result.StructureSummary = string.Format("目录格式与“XX数据汇交成果.zip”示例不一致，但仍继续执行 .gdb 检查。当前顶层目录：{0}。",
                topLevelDirectories.Count == 0 ? "无" : string.Join("、", topLevelDirectories));
            result.WarningMessage = "标准汇交成果包应包含 3 个顶层目录：第 1 类文档目录、第 2 类文档目录，以及第 3 类 gdb 目录。";

            if (gdbDirectories != null && gdbDirectories.Count > 0)
            {
                result.WarningMessage = result.WarningMessage + " 当前压缩包虽然目录格式不标准，但已继续识别到 .gdb 数据。";
            }

            return result;
        }

        private static string ResolveInspectionRoot(string extractionRoot)
        {
            var topLevelFiles = Directory.GetFiles(extractionRoot, "*", SearchOption.TopDirectoryOnly);
            var topLevelDirectories = Directory.GetDirectories(extractionRoot, "*", SearchOption.TopDirectoryOnly);

            if (topLevelFiles.Length == 0 && topLevelDirectories.Length == 1)
            {
                var wrappedRoot = topLevelDirectories[0];
                var wrappedDirectories = Directory.GetDirectories(wrappedRoot, "*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (wrappedDirectories.Any(IsFirstDocumentFolder)
                    || wrappedDirectories.Any(IsSecondDocumentFolder)
                    || wrappedDirectories.Any(IsGdbFolder))
                {
                    return wrappedRoot;
                }
            }

            return extractionRoot;
        }

        private static bool IsFirstDocumentFolder(string name)
        {
            return StartsWithIndex(name, "1") && LooksLikeDocumentFolder(name);
        }

        private static bool IsSecondDocumentFolder(string name)
        {
            return StartsWithIndex(name, "2") && LooksLikeDocumentFolder(name);
        }

        private static bool IsGdbFolder(string name)
        {
            return StartsWithIndex(name, "3") && name.IndexOf("gdb", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeDocumentFolder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.IndexOf("pdf", StringComparison.OrdinalIgnoreCase) >= 0
                   && name.IndexOf("doc", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool StartsWithIndex(string name, string prefix)
        {
            return !string.IsNullOrWhiteSpace(name)
                   && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
