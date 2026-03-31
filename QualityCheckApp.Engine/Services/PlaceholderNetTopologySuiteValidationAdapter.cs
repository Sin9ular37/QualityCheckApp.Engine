using System;
using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class PlaceholderNetTopologySuiteValidationAdapter : IGeometryValidationAdapter
    {
        public Task<TopologyCheckResult> ValidateAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("当前仅建立 NetTopologySuite 校验接口骨架，尚未接入实际几何读取与规则实现。\n建议下一步在引入 GDAL/OGR 后实现该适配器。");
        }
    }
}
