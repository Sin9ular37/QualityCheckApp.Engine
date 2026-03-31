using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class PlaceholderGdalDatasetReader : IGeometryDatasetReader
    {
        public Task<IReadOnlyList<GdbLayerInfo>> ReadContainersAsync(string gdbPath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("当前仅建立 GDAL/OGR 读取接口骨架，尚未接入实际包与驱动。\n建议下一步引入 GDAL/OGR 后替换该占位实现。");
        }
    }
}
