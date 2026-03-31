namespace QualityCheckApp.Engine.Services
{
    public interface IMapViewportAdapter
    {
        string AdapterName { get; }

        bool IsAvailable { get; }
    }
}
