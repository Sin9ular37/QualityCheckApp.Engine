using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public interface IGeometryValidationAdapter
    {
        Task<TopologyCheckResult> ValidateAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken);
    }
}
