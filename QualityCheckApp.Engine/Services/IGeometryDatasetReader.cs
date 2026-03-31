using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public interface IGeometryDatasetReader
    {
        Task<IReadOnlyList<GdbLayerInfo>> ReadContainersAsync(string gdbPath, CancellationToken cancellationToken);
    }
}
