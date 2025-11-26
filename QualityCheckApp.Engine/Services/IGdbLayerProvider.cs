using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ESRI.ArcGIS.Carto;
using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public interface IGdbLayerProvider
    {
        Task<IReadOnlyList<GdbLayerInfo>> LoadLayersAsync(string gdbPath, CancellationToken cancellationToken);

        IFeatureLayer CreateFeatureLayer(GdbLayerInfo layerInfo);
    }
}
