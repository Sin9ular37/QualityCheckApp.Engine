using System;
using System.Collections.Generic;
using System.IO;

namespace QualityCheckApp.Engine.Models
{
    /// <summary>
    /// 封装 ZIP 解压后的结果。
    /// </summary>
    public sealed class ZipExtractionResult : IDisposable
    {
        private readonly string _extractionRoot;
        private readonly IReadOnlyList<string> _gdbDirectories;

        public ZipExtractionResult(string extractionRoot, IReadOnlyList<string> gdbDirectories)
        {
            _extractionRoot = extractionRoot;
            _gdbDirectories = gdbDirectories;
        }

        public string ExtractionRoot
        {
            get { return _extractionRoot; }
        }

        public IReadOnlyList<string> GdbDirectories
        {
            get { return _gdbDirectories; }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(ExtractionRoot))
                {
                    Directory.Delete(ExtractionRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // 忽略清理过程中出现的 IO 异常，留待后续手动处理。
            }
            catch (UnauthorizedAccessException)
            {
                // 同上。
            }
        }
    }
}
