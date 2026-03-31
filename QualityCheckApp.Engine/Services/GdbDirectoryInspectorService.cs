using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class GdbDirectoryInspectorService
    {
        public Task<IReadOnlyList<GdbLayerInfo>> InspectAsync(IReadOnlyList<string> gdbDirectories, CancellationToken cancellationToken)
        {
            return Task.Run(() => Inspect(gdbDirectories, cancellationToken), cancellationToken);
        }

        private static IReadOnlyList<GdbLayerInfo> Inspect(IReadOnlyList<string> gdbDirectories, CancellationToken cancellationToken)
        {
            var results = new List<GdbLayerInfo>();
            if (gdbDirectories == null)
            {
                return results;
            }

            foreach (var gdbPath in gdbDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var files = Directory.Exists(gdbPath)
                    ? Directory.GetFiles(gdbPath, "*", SearchOption.TopDirectoryOnly)
                    : new string[0];
                var tableFiles = Directory.Exists(gdbPath)
                    ? Directory.GetFiles(gdbPath, "*.gdbtable", SearchOption.TopDirectoryOnly)
                    : new string[0];
                var indexFiles = Directory.Exists(gdbPath)
                    ? Directory.GetFiles(gdbPath, "*.gdbindexes", SearchOption.TopDirectoryOnly)
                    : new string[0];
                var hasMetadata = File.Exists(Path.Combine(gdbPath, "gdb"));
                var hasTimestamp = File.Exists(Path.Combine(gdbPath, "timestamps"));
                var isInspectable = hasMetadata && hasTimestamp && tableFiles.Length > 0;

                results.Add(new GdbLayerInfo
                {
                    GdbPath = gdbPath,
                    DatasetName = "File Geodatabase",
                    LayerName = Path.GetFileNameWithoutExtension(gdbPath),
                    GeometryType = string.Format("{0} 个表文件 / {1} 个索引文件", tableFiles.Length, indexFiles.Length),
                    Summary = string.Format("目录文件数：{0}；核心元数据：{1}；时间戳：{2}",
                        files.Length,
                        hasMetadata ? "是" : "否",
                        hasTimestamp ? "是" : "否"),
                    Displayable = isInspectable,
                    IsVisible = isInspectable
                });
            }

            return results;
        }
    }
}
