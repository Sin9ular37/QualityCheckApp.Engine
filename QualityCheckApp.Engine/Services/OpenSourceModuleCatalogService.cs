using System.Collections.Generic;

using QualityCheckApp.Engine.Models;

namespace QualityCheckApp.Engine.Services
{
    public class OpenSourceModuleCatalogService
    {
        public IReadOnlyList<OpenSourceModuleInfo> BuildModules()
        {
            return new List<OpenSourceModuleInfo>
            {
                new OpenSourceModuleInfo
                {
                    ModuleName = "数据读取适配器",
                    PlannedLibrary = "GDAL/OGR",
                    Responsibility = "读取 File Geodatabase、枚举要素类与字段。",
                    Status = "接口骨架已建立",
                    NextStep = "引入 GDAL/OGR 包并替换 PlaceholderGdalDatasetReader。",
                    IsScaffolded = true
                },
                new OpenSourceModuleInfo
                {
                    ModuleName = "几何校验适配器",
                    PlannedLibrary = "NetTopologySuite",
                    Responsibility = "承接简单几何校验与后续拓扑规则实现。",
                    Status = "接口骨架已建立",
                    NextStep = "在读取层可用后实现 ValidateAsync。",
                    IsScaffolded = true
                },
                new OpenSourceModuleInfo
                {
                    ModuleName = "地图视口适配器",
                    PlannedLibrary = "Mapsui",
                    Responsibility = "替代 AxMapControl，提供平移、缩放、定位和问题浏览。",
                    Status = "接口骨架已建立",
                    NextStep = "接入 Mapsui WPF 控件并补充图层渲染。",
                    IsScaffolded = true
                },
                new OpenSourceModuleInfo
                {
                    ModuleName = "坐标参考桥接",
                    PlannedLibrary = "ProjNet",
                    Responsibility = "处理坐标转换与地图显示所需的投影转换。",
                    Status = "规划中",
                    NextStep = "等地图与读取层接通后补充投影转换服务。",
                    IsScaffolded = false
                }
            };
        }
    }
}
