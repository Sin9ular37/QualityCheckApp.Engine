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
        private readonly IGeometryDatasetReader _datasetReader;

        public GdbDirectoryInspectorService()
        {
            _datasetReader = new GdalOgrDatasetReader();
        }

        public Task<IReadOnlyList<GdbLayerInfo>> InspectAsync(IReadOnlyList<string> gdbDirectories, CancellationToken cancellationToken)
        {
            return InspectInternalAsync(gdbDirectories, cancellationToken);
        }

        private async Task<IReadOnlyList<GdbLayerInfo>> InspectInternalAsync(IReadOnlyList<string> gdbDirectories, CancellationToken cancellationToken)
        {
            var results = new List<GdbLayerInfo>();
            if (gdbDirectories == null)
            {
                return results;
            }

            foreach (var gdbPath in gdbDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var layers = await _datasetReader.ReadContainersAsync(gdbPath, cancellationToken);
                    foreach (var layer in layers)
                    {
                        results.Add(layer);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new GdbLayerInfo
                    {
                        GdbPath = gdbPath,
                        DatasetName = "OpenFileGDB",
                        LayerName = Path.GetFileNameWithoutExtension(gdbPath),
                        GeometryType = "读取失败",
                        Summary = string.Format("GDAL/OGR 读取失败：{0}", ex.Message),
                        Displayable = false,
                        IsVisible = false
                    });
                }
            }

            return results;
        }
    }
}
