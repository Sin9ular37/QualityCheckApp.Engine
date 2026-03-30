using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public interface ITopologyCheckService
    {
        Task<TopologyCheckResult> CheckLayerAsync(GdbLayerInfo layerInfo, CancellationToken cancellationToken);
    }
}
