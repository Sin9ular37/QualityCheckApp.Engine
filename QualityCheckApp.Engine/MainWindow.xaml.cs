using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Mapsui;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;

using Microsoft.Win32;

using QualityCheckApp.Engine.Models;
using QualityCheckApp.Engine.Services;

using MapsuiBoundingBox = Mapsui.Geometries.BoundingBox;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiLinearRing = Mapsui.Geometries.LinearRing;
using MapsuiPoint = Mapsui.Geometries.Point;
using MapsuiPolygon = Mapsui.Geometries.Polygon;

namespace QualityCheckApp.Engine
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ZipPackageService _zipService = new ZipPackageService();
        private readonly GdbDirectoryInspectorService _gdbInspectorService = new GdbDirectoryInspectorService();
        private readonly GdalMapsuiPreviewService _gdalMapsuiPreviewService = new GdalMapsuiPreviewService();
        private readonly NtsWktValidationService _ntsWktValidationService = new NtsWktValidationService();
        private readonly ITopologyCheckService _topologyCheckService = new FileTopologyCheckService();
        private readonly OpenSourceModuleCatalogService _openSourceModuleCatalogService = new OpenSourceModuleCatalogService();
        private readonly ObservableCollection<GdbLayerInfo> _layers;
        private readonly ObservableCollection<OpenSourceModuleInfo> _openSourceModules;
        private readonly ObservableCollection<TopologyIssueInfo> _topologyIssues;

        private ZipExtractionResult _currentExtraction;
        private MemoryLayer _polygonGeometryPreviewLayer;
        private MemoryLayer _lineGeometryPreviewLayer;
        private MemoryLayer _pointGeometryPreviewLayer;
        private MemoryLayer _layerPreviewLayer;
        private MemoryLayer _issuePreviewLayer;
        private FrameworkElement _previewMapControl;
        private GdbLayerInfo _selectedLayer;
        private TopologyIssueInfo _selectedTopologyIssue;
        private string _selectedZipPath;
        private string _statusMessage = "请选择 ZIP 压缩包。当前分支已移除 ArcGIS 运行时依赖。";
        private string _packageMode = "待识别";
        private string _structureSummary = "尚未执行压缩包检查。";
        private string _topologySummary = "请选择 GDB 图层并执行结构检查。";
        private string _mapGeometryLoadedSummary = "尚未加载真实几何。";
        private string _mapPreviewSummary = "Mapsui 预览尚未加载范围。";
        private string _wktInput = "POLYGON ((0 0, 10 10, 10 0, 0 10, 0 0))";
        private string _wktValidationSummary = "已接入 NetTopologySuite，可直接验证 WKT。";
        private string _wktValidationDetail = "默认示例是一个自相交面，用于确认开源几何内核已正常工作。";
        private int _detectedGdbCount;
        private bool _isBusy;

        public MainWindow()
        {
            InitializeComponent();

            _selectedZipPath = string.Empty;
            _layers = new ObservableCollection<GdbLayerInfo>();
            _layers.CollectionChanged += OnLayersCollectionChanged;

            _openSourceModules = new ObservableCollection<OpenSourceModuleInfo>();
            _topologyIssues = new ObservableCollection<TopologyIssueInfo>();

            foreach (var module in _openSourceModuleCatalogService.BuildModules())
            {
                _openSourceModules.Add(module);
            }

            DataContext = this;
            InitializeMapPreview();
        }

        public ObservableCollection<GdbLayerInfo> Layers
        {
            get { return _layers; }
        }

        public ObservableCollection<TopologyIssueInfo> TopologyIssues
        {
            get { return _topologyIssues; }
        }

        public ObservableCollection<OpenSourceModuleInfo> OpenSourceModules
        {
            get { return _openSourceModules; }
        }

        public string SelectedZipPath
        {
            get { return _selectedZipPath; }
            set
            {
                if (_selectedZipPath == value)
                {
                    return;
                }

                _selectedZipPath = value ?? string.Empty;
                OnPropertyChanged("SelectedZipPath");
                OnPropertyChanged("SelectedZipPathDisplay");
                OnPropertyChanged("IsAnalyzeEnabled");
            }
        }

        public string SelectedZipPathDisplay
        {
            get { return string.IsNullOrWhiteSpace(SelectedZipPath) ? "尚未选择压缩包" : SelectedZipPath; }
        }

        public GdbLayerInfo SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                if (_selectedLayer == value)
                {
                    return;
                }

                _selectedLayer = value;
                ClearTopologyResults();
                ClearRenderedMapGeometries();
                OnPropertyChanged("SelectedLayer");
                OnPropertyChanged("SelectedLayerDisplayName");
                OnPropertyChanged("SelectedLayerSummary");
                OnPropertyChanged("CanRunTopologyCheck");
                OnPropertyChanged("CanLoadGeometryPreview");
                RefreshMapPreview();
            }
        }

        public string SelectedLayerDisplayName
        {
            get { return SelectedLayer == null ? "未选择 GDB 图层" : SelectedLayer.LayerName; }
        }

        public string SelectedLayerSummary
        {
            get
            {
                return SelectedLayer == null
                    ? "当前分支已能用 GDAL/OGR 枚举 .gdb 图层，并在 Mapsui 中显示真实几何预览。"
                    : SelectedLayer.Summary;
            }
        }

        public TopologyIssueInfo SelectedTopologyIssue
        {
            get { return _selectedTopologyIssue; }
            set
            {
                if (_selectedTopologyIssue == value)
                {
                    return;
                }

                _selectedTopologyIssue = value;
                OnPropertyChanged("SelectedTopologyIssue");
                RefreshMapPreview();
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                _statusMessage = value ?? string.Empty;
                OnPropertyChanged("StatusMessage");
            }
        }

        public string PackageMode
        {
            get { return _packageMode; }
            private set
            {
                if (_packageMode == value)
                {
                    return;
                }

                _packageMode = value ?? string.Empty;
                OnPropertyChanged("PackageMode");
            }
        }

        public string StructureSummary
        {
            get { return _structureSummary; }
            private set
            {
                if (_structureSummary == value)
                {
                    return;
                }

                _structureSummary = value ?? string.Empty;
                OnPropertyChanged("StructureSummary");
            }
        }

        public string TopologySummary
        {
            get { return _topologySummary; }
            private set
            {
                if (_topologySummary == value)
                {
                    return;
                }

                _topologySummary = value ?? string.Empty;
                OnPropertyChanged("TopologySummary");
            }
        }

        public string MapPreviewSummary
        {
            get { return _mapPreviewSummary; }
            private set
            {
                if (_mapPreviewSummary == value)
                {
                    return;
                }

                _mapPreviewSummary = value ?? string.Empty;
                OnPropertyChanged("MapPreviewSummary");
            }
        }

        public bool CanLoadGeometryPreview
        {
            get { return !IsBusy && SelectedLayer != null && SelectedLayer.Displayable; }
        }

        public string WktInput
        {
            get { return _wktInput; }
            set
            {
                if (_wktInput == value)
                {
                    return;
                }

                _wktInput = value ?? string.Empty;
                OnPropertyChanged("WktInput");
            }
        }

        public string WktValidationSummary
        {
            get { return _wktValidationSummary; }
            private set
            {
                if (_wktValidationSummary == value)
                {
                    return;
                }

                _wktValidationSummary = value ?? string.Empty;
                OnPropertyChanged("WktValidationSummary");
            }
        }

        public string WktValidationDetail
        {
            get { return _wktValidationDetail; }
            private set
            {
                if (_wktValidationDetail == value)
                {
                    return;
                }

                _wktValidationDetail = value ?? string.Empty;
                OnPropertyChanged("WktValidationDetail");
            }
        }

        public int DetectedGdbCount
        {
            get { return _detectedGdbCount; }
            private set
            {
                if (_detectedGdbCount == value)
                {
                    return;
                }

                _detectedGdbCount = value;
                OnPropertyChanged("DetectedGdbCount");
            }
        }

        public int LayerCount
        {
            get { return Layers.Count; }
        }

        public int HealthyLayerCount
        {
            get
            {
                var count = 0;
                foreach (var layer in Layers)
                {
                    if (layer.Displayable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int NeedsAttentionLayerCount
        {
            get { return Math.Max(LayerCount - HealthyLayerCount, 0); }
        }

        public int TopologyIssueCount
        {
            get { return TopologyIssues.Count; }
        }

        public int ScaffoldedOpenSourceModuleCount
        {
            get
            {
                var count = 0;
                foreach (var module in OpenSourceModules)
                {
                    if (module.IsScaffolded)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PlannedOpenSourceModuleCount
        {
            get { return OpenSourceModules.Count; }
        }

        public string OpenSourceSkeletonSummary
        {
            get
            {
                return string.Format("已建立 {0}/{1} 个开源替代模块骨架，并已接入 GDAL/OGR 图层读取、NetTopologySuite 几何检查和 Mapsui 真实几何渲染。下一步补问题点位高亮和底图支持。",
                    ScaffoldedOpenSourceModuleCount,
                    PlannedOpenSourceModuleCount);
            }
        }

        private void InitializeMapPreview()
        {
            if (PreviewMapHost == null)
            {
                return;
            }

            if (_previewMapControl == null)
            {
                var controlType = ResolveMapsuiMapControlType();
                if (controlType == null)
                {
                    MapPreviewSummary = "Mapsui 预览控件加载失败：未找到 Mapsui.UI.Wpf.MapControl。";
                    return;
                }

                _previewMapControl = Activator.CreateInstance(controlType) as FrameworkElement;
                if (_previewMapControl == null)
                {
                    MapPreviewSummary = "Mapsui 预览控件创建失败。";
                    return;
                }

                PreviewMapHost.Children.Clear();
                PreviewMapHost.Children.Add(_previewMapControl);
            }

            var map = new Map();
            map.BackColor = MapsuiColor.FromArgb(255, 249, 251, 254);

            _layerPreviewLayer = CreatePreviewLayer(
                "图层范围",
                MapsuiColor.FromArgb(60, 86, 158, 255),
                MapsuiColor.FromArgb(255, 61, 126, 219));
            _polygonGeometryPreviewLayer = CreatePreviewLayer(
                "图层面",
                MapsuiColor.FromArgb(55, 86, 158, 255),
                MapsuiColor.FromArgb(255, 46, 96, 188));
            _lineGeometryPreviewLayer = CreateLinePreviewLayer(
                "图层线",
                MapsuiColor.FromArgb(255, 34, 102, 197));
            _pointGeometryPreviewLayer = CreatePointPreviewLayer(
                "图层点",
                MapsuiColor.FromArgb(255, 39, 110, 210),
                MapsuiColor.FromArgb(255, 18, 61, 126));
            _issuePreviewLayer = CreatePreviewLayer(
                "问题范围",
                MapsuiColor.FromArgb(90, 255, 120, 120),
                MapsuiColor.FromArgb(255, 214, 58, 58));

            map.Layers.Add(_layerPreviewLayer);
            map.Layers.Add(_polygonGeometryPreviewLayer);
            map.Layers.Add(_lineGeometryPreviewLayer);
            map.Layers.Add(_pointGeometryPreviewLayer);
            map.Layers.Add(_issuePreviewLayer);

            ((dynamic)_previewMapControl).ShowGridLines = true;
            ((dynamic)_previewMapControl).Map = map;
            RefreshMapPreview();
        }

        private static MemoryLayer CreatePreviewLayer(string name, MapsuiColor fillColor, MapsuiColor outlineColor)
        {
            return new MemoryLayer
            {
                Name = name,
                DataSource = new MemoryProvider(),
                Style = new VectorStyle
                {
                    Fill = new Brush(fillColor),
                    Outline = new Pen(outlineColor, 2)
                }
            };
        }

        private static MemoryLayer CreateLinePreviewLayer(string name, MapsuiColor lineColor)
        {
            return new MemoryLayer
            {
                Name = name,
                DataSource = new MemoryProvider(),
                Style = new VectorStyle
                {
                    Line = new Pen(lineColor, 2.5)
                }
            };
        }

        private static MemoryLayer CreatePointPreviewLayer(string name, MapsuiColor fillColor, MapsuiColor outlineColor)
        {
            return new MemoryLayer
            {
                Name = name,
                DataSource = new MemoryProvider(),
                Style = new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Brush(fillColor),
                    Outline = new Pen(outlineColor, 1.5),
                    SymbolScale = 0.9
                }
            };
        }

        private void RefreshMapPreview()
        {
            if (_previewMapControl == null
                || _layerPreviewLayer == null
                || _polygonGeometryPreviewLayer == null
                || _lineGeometryPreviewLayer == null
                || _pointGeometryPreviewLayer == null
                || _issuePreviewLayer == null)
            {
                return;
            }

            dynamic previewControl = _previewMapControl;
            if (previewControl.Map == null)
            {
                return;
            }

            SetLayerGeometry(_layerPreviewLayer, null);
            SetLayerGeometry(_issuePreviewLayer, null);

            MapsuiBoundingBox targetExtent = null;
            if (SelectedLayer != null && SelectedLayer.HasExtent)
            {
                var layerExtent = CreatePreviewBoundingBox(SelectedLayer.ExtentXMin, SelectedLayer.ExtentYMin, SelectedLayer.ExtentXMax, SelectedLayer.ExtentYMax);
                SetLayerGeometry(_layerPreviewLayer, CreateExtentGeometry(layerExtent));
                targetExtent = layerExtent;
                MapPreviewSummary = string.Format("当前图层：{0}。{1}", SelectedLayer.LayerName, _mapGeometryLoadedSummary);
            }
            else
            {
                MapPreviewSummary = "Mapsui 预览尚未加载范围。";
            }

            if (SelectedTopologyIssue != null && SelectedTopologyIssue.HasFocusExtent)
            {
                var issueExtent = CreatePreviewBoundingBox(
                    SelectedTopologyIssue.FocusXMin,
                    SelectedTopologyIssue.FocusYMin,
                    SelectedTopologyIssue.FocusXMax,
                    SelectedTopologyIssue.FocusYMax);
                SetLayerGeometry(_issuePreviewLayer, CreateExtentGeometry(issueExtent));
                targetExtent = issueExtent;
                MapPreviewSummary = string.Format("当前问题：{0}。{1}", SelectedTopologyIssue.RuleName, _mapGeometryLoadedSummary);
            }

            if (targetExtent != null && previewControl.Navigator != null)
            {
                previewControl.Navigator.NavigateTo(targetExtent, ScaleMethod.Fit, 0L, null);
            }
        }

        private static void SetLayerGeometry(MemoryLayer layer, Geometry geometry)
        {
            layer.DataSource = geometry == null ? new MemoryProvider() : new MemoryProvider(geometry);
        }

        private static void SetLayerGeometries(MemoryLayer layer, IReadOnlyList<Geometry> geometries)
        {
            layer.DataSource = geometries == null || geometries.Count == 0
                ? new MemoryProvider()
                : new MemoryProvider(geometries);
        }

        private void ClearRenderedMapGeometries()
        {
            _mapGeometryLoadedSummary = "尚未加载真实几何。";

            if (_polygonGeometryPreviewLayer != null)
            {
                SetLayerGeometries(_polygonGeometryPreviewLayer, null);
            }

            if (_lineGeometryPreviewLayer != null)
            {
                SetLayerGeometries(_lineGeometryPreviewLayer, null);
            }

            if (_pointGeometryPreviewLayer != null)
            {
                SetLayerGeometries(_pointGeometryPreviewLayer, null);
            }
        }

        private void ApplyGeometryPreview(MapPreviewLoadResult result)
        {
            if (result == null)
            {
                ClearRenderedMapGeometries();
                RefreshMapPreview();
                return;
            }

            SetLayerGeometries(_polygonGeometryPreviewLayer, result.PolygonGeometries);
            SetLayerGeometries(_lineGeometryPreviewLayer, result.LineGeometries);
            SetLayerGeometries(_pointGeometryPreviewLayer, result.PointGeometries);
            _mapGeometryLoadedSummary = result.Summary;
            RefreshMapPreview();
        }

        private static MapsuiBoundingBox CreatePreviewBoundingBox(double xMin, double yMin, double xMax, double yMax)
        {
            var minX = Math.Min(xMin, xMax);
            var minY = Math.Min(yMin, yMax);
            var maxX = Math.Max(xMin, xMax);
            var maxY = Math.Max(yMin, yMax);

            var width = Math.Abs(maxX - minX);
            var height = Math.Abs(maxY - minY);
            var paddingX = width < 0.0001 ? 1 : width * 0.08;
            var paddingY = height < 0.0001 ? 1 : height * 0.08;

            return new MapsuiBoundingBox(minX - paddingX, minY - paddingY, maxX + paddingX, maxY + paddingY);
        }

        private static Geometry CreateExtentGeometry(MapsuiBoundingBox boundingBox)
        {
            var points = new List<MapsuiPoint>
            {
                new MapsuiPoint(boundingBox.MinX, boundingBox.MinY),
                new MapsuiPoint(boundingBox.MaxX, boundingBox.MinY),
                new MapsuiPoint(boundingBox.MaxX, boundingBox.MaxY),
                new MapsuiPoint(boundingBox.MinX, boundingBox.MaxY),
                new MapsuiPoint(boundingBox.MinX, boundingBox.MinY)
            };

            return new MapsuiPolygon(new MapsuiLinearRing(points));
        }

        private static Type ResolveMapsuiMapControlType()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                var type = assemblies[index].GetType("Mapsui.UI.Wpf.MapControl", false);
                if (type != null)
                {
                    return type;
                }
            }

            var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mapsui.UI.Wpf.dll");
            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            return assembly.GetType("Mapsui.UI.Wpf.MapControl", false);
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged("IsBusy");
                OnPropertyChanged("IsNotBusy");
                OnPropertyChanged("IsAnalyzeEnabled");
                OnPropertyChanged("CanRunTopologyCheck");
                OnPropertyChanged("CanLoadGeometryPreview");
            }
        }

        public bool IsNotBusy
        {
            get { return !IsBusy; }
        }

        public bool IsAnalyzeEnabled
        {
            get { return !IsBusy && !string.IsNullOrWhiteSpace(SelectedZipPath); }
        }

        public bool CanRunTopologyCheck
        {
            get { return !IsBusy && SelectedLayer != null; }
        }

        private void OnSelectZipClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZIP 压缩包 (*.zip)|*.zip",
                Title = "选择包含 .gdb 的压缩包"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedZipPath = dialog.FileName;
                StatusMessage = "已选择压缩包，点击“开始检查”分析 ZIP 结构并用 GDAL/OGR 枚举 GDB 图层。";
            }
        }

        private async void OnAnalyzeZipClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedZipPath) || !File.Exists(SelectedZipPath))
            {
                StatusMessage = "请选择一个有效的 ZIP 文件。";
                return;
            }

            await RunWithBusyIndicator(async token =>
            {
                ClearResults();

                StatusMessage = "正在解压压缩包...";
                if (_currentExtraction != null)
                {
                    _currentExtraction.Dispose();
                }

                _currentExtraction = await _zipService.ExtractAsync(SelectedZipPath, token);

                StatusMessage = "正在检查压缩包结构...";
                var gdbDirectories = ResolvePackageGdbDirectories(_currentExtraction.ExtractionRoot, _currentExtraction.GdbDirectories);
                DetectedGdbCount = gdbDirectories.Count;

                if (gdbDirectories.Count == 0)
                {
                    StatusMessage = PackageMode == "标准测试包"
                        ? "测试1 目录中未检测到 .gdb 数据。"
                        : "压缩包中没有找到 .gdb 目录。";
                    return;
                }

                StatusMessage = "正在分析 .gdb 目录内容...";
                var containers = await _gdbInspectorService.InspectAsync(gdbDirectories, token);
                foreach (var container in containers)
                {
                    Layers.Add(container);
                }

                if (Layers.Count > 0)
                {
                    SelectedLayer = Layers[0];
                }

                StatusMessage = string.Format("{0}处理完成：识别到 {1} 个 .gdb，结构可用 {2} 个，待关注 {3} 个。",
                    PackageMode == "标准测试包" ? "标准包" : "普通包",
                    DetectedGdbCount,
                    HealthyLayerCount,
                    NeedsAttentionLayerCount);
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            ClearResults();
            StatusMessage = "已清空结果。";
        }

        private async void OnRunTopologyCheckClick(object sender, RoutedEventArgs e)
        {
            if (SelectedLayer == null)
            {
                StatusMessage = "请先在 GDB 图层列表中选择一个图层。";
                return;
            }

            await RunWithBusyIndicator(async token =>
            {
                StatusMessage = string.Format("正在检查图层 {0} 所属 GDB 的结构完整性...", SelectedLayer.LayerName);
                ClearTopologyResults();

                var result = await _topologyCheckService.CheckLayerAsync(SelectedLayer, token);
                ApplyTopologyResult(result);
                StatusMessage = TopologySummary;
            });
        }

        private async void OnLoadGeometryPreviewClick(object sender, RoutedEventArgs e)
        {
            if (SelectedLayer == null)
            {
                StatusMessage = "请先在 GDB 图层列表中选择一个图层。";
                return;
            }

            await RunWithBusyIndicator(async token =>
            {
                StatusMessage = string.Format("正在加载图层 {0} 的真实几何预览...", SelectedLayer.LayerName);
                var preview = await _gdalMapsuiPreviewService.LoadPreviewAsync(SelectedLayer, token);
                ApplyGeometryPreview(preview);
                StatusMessage = preview.Summary;
            });
        }

        private void OnClearGeometryPreviewClick(object sender, RoutedEventArgs e)
        {
            ClearRenderedMapGeometries();
            RefreshMapPreview();
            StatusMessage = "已清空真实几何预览。";
        }

        private void OnClearTopologyClick(object sender, RoutedEventArgs e)
        {
            ClearTopologyResults();
            StatusMessage = "已清空结构检查结果。";
        }

        private void OnLoadSampleWktClick(object sender, RoutedEventArgs e)
        {
            WktInput = "POLYGON ((0 0, 10 10, 10 0, 0 10, 0 0))";
            WktValidationSummary = "已载入自相交面示例。";
            WktValidationDetail = "点击“验证 WKT”可看到 NetTopologySuite 返回的无效原因。";
        }

        private void OnValidateWktClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _ntsWktValidationService.Validate(WktInput);
                WktValidationSummary = result.Summary;
                WktValidationDetail = result.Detail;
                StatusMessage = result.Summary;
            }
            catch (Exception ex)
            {
                WktValidationSummary = string.Format("WKT 校验失败：{0}", ex.Message);
                WktValidationDetail = "请确认输入是有效的 WKT 文本，例如 POINT、LINESTRING、POLYGON。";
                StatusMessage = WktValidationSummary;
            }
        }

        private async Task RunWithBusyIndicator(Func<CancellationToken, Task> operation)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    await operation(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消。";
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format("处理失败：{0}", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearResults()
        {
            SelectedLayer = null;

            Layers.CollectionChanged -= OnLayersCollectionChanged;
            foreach (var layer in Layers)
            {
                layer.PropertyChanged -= OnLayerPropertyChanged;
            }
            Layers.Clear();
            Layers.CollectionChanged += OnLayersCollectionChanged;

            if (_currentExtraction != null)
            {
                _currentExtraction.Dispose();
            }
            _currentExtraction = null;

            ResetInspectionSummary();
            ClearTopologyResults();
            ClearRenderedMapGeometries();
        }

        private void ResetInspectionSummary()
        {
            DetectedGdbCount = 0;
            PackageMode = "待识别";
            StructureSummary = "尚未执行压缩包检查。";
            NotifyLayerStatisticsChanged();
        }

        private void ClearTopologyResults()
        {
            SelectedTopologyIssue = null;
            TopologyIssues.Clear();
            TopologySummary = "请选择 GDB 图层并执行结构检查。";
            NotifyTopologyStateChanged();
        }

        private void ApplyTopologyResult(TopologyCheckResult result)
        {
            if (result == null)
            {
                ClearTopologyResults();
                return;
            }

            SelectedTopologyIssue = null;
            TopologyIssues.Clear();
            foreach (var issue in result.Issues)
            {
                TopologyIssues.Add(issue);
            }

            TopologySummary = result.Summary;
            NotifyTopologyStateChanged();
        }

        private IReadOnlyList<string> ResolvePackageGdbDirectories(string extractionRoot, IReadOnlyList<string> fallbackGdbDirectories)
        {
            var requiredFolders = new[] { "测试1", "测试2", "测试3" };
            var missingFolders = new List<string>();
            var foundFolders = 0;
            var test1Path = string.Empty;

            foreach (var folderName in requiredFolders)
            {
                var matches = Directory.GetDirectories(extractionRoot, folderName, SearchOption.AllDirectories);
                if (matches.Length == 0)
                {
                    missingFolders.Add(folderName);
                    continue;
                }

                foundFolders++;
                if (string.Equals(folderName, "测试1", StringComparison.OrdinalIgnoreCase))
                {
                    test1Path = matches[0];
                }
            }

            if (foundFolders == 0)
            {
                PackageMode = "普通 .gdb ZIP";
                StructureSummary = "未检测到 测试1/测试2/测试3 目录，已按普通 .gdb ZIP 处理。";
                return fallbackGdbDirectories;
            }

            if (missingFolders.Count > 0)
            {
                throw new InvalidOperationException(string.Format("检测到部分标准测试目录，但缺少：{0}。", string.Join("、", missingFolders)));
            }

            if (string.IsNullOrWhiteSpace(test1Path))
            {
                throw new InvalidOperationException("未能定位测试1 目录。");
            }

            PackageMode = "标准测试包";
            StructureSummary = "结构校验通过：已识别 测试1、测试2、测试3 目录，并优先分析 测试1 中的 .gdb 图层。";
            return (IReadOnlyList<string>)Directory.GetDirectories(test1Path, "*.gdb", SearchOption.AllDirectories);
        }

        private void OnLayersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GdbLayerInfo info in e.NewItems)
                {
                    info.PropertyChanged += OnLayerPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (GdbLayerInfo info in e.OldItems)
                {
                    info.PropertyChanged -= OnLayerPropertyChanged;
                }
            }

            if (SelectedLayer != null && !Layers.Contains(SelectedLayer))
            {
                SelectedLayer = null;
            }

            NotifyLayerStatisticsChanged();
        }

        private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Displayable")
            {
                NotifyLayerStatisticsChanged();
            }

            if (e.PropertyName == "Summary")
            {
                OnPropertyChanged("SelectedLayerSummary");
            }
        }

        private void NotifyLayerStatisticsChanged()
        {
            OnPropertyChanged("LayerCount");
            OnPropertyChanged("HealthyLayerCount");
            OnPropertyChanged("NeedsAttentionLayerCount");
            OnPropertyChanged("SelectedLayerSummary");
            OnPropertyChanged("CanRunTopologyCheck");
        }

        private void NotifyTopologyStateChanged()
        {
            OnPropertyChanged("TopologyIssueCount");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
