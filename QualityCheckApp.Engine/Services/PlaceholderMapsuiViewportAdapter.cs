namespace QualityCheckApp.Engine.Services
{
    public class PlaceholderMapsuiViewportAdapter : IMapViewportAdapter
    {
        public string AdapterName
        {
            get { return "Mapsui 预览适配器（占位）"; }
        }

        public bool IsAvailable
        {
            get { return false; }
        }
    }
}
