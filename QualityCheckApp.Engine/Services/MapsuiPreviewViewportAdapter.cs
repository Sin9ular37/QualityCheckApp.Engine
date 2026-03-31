namespace QualityCheckApp.Engine.Services
{
    public class MapsuiPreviewViewportAdapter : IMapViewportAdapter
    {
        public string AdapterName
        {
            get { return "Mapsui 几何预览适配器"; }
        }

        public bool IsAvailable
        {
            get { return true; }
        }
    }
}
