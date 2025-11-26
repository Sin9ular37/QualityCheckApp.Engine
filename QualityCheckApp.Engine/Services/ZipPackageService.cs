using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    /// <summary>
    /// 负责 ZIP 解压和 .gdb 搜索。
    /// </summary>
    public class ZipPackageService
    {
        public async Task<ZipExtractionResult> ExtractAsync(string zipPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                throw new ArgumentException("未指定压缩包路径。", "zipPath");
            }

            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("压缩包不存在。", zipPath);
            }

            var extractionRoot = Path.Combine(Path.GetTempPath(), "QC_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionRoot);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractionRoot), cancellationToken);

            var gdbDirectories = await Task.Run(
                () => (IReadOnlyList<string>)Directory.GetDirectories(extractionRoot, "*.gdb", SearchOption.AllDirectories),
                cancellationToken);

            return new ZipExtractionResult(extractionRoot, gdbDirectories);
        }
    }
}
