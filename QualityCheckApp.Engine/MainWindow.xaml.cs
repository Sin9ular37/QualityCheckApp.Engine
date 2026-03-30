using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SystemUI;
using Microsoft.Win32;
using QualityCheckApp.Engine.Models;
using QualityCheckApp.Engine.Services;

namespace QualityCheckApp.Engine
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ZipPackageService _zipService = new ZipPackageService();
        private readonly IGdbLayerProvider _layerProvider = new ArcGisLayerProvider();
        private readonly Dictionary<GdbLayerInfo, ILayer> _mapLayers = new Dictionary<GdbLayerInfo, ILayer>();
        private readonly ObservableCollection<GdbLayerInfo> _layers;

        private AxMapControl _mapControl;
        private ITool _mapPanTool;
        private ZipExtractionResult _currentExtraction;
        private GdbLayerInfo _selectedLayer;
        private string _selectedZipPath;
        private string _statusMessage = "请选择 ZIP 压缩包。支持普通 .gdb ZIP 和标准测试包。";
        private string _mouseCoordinate = "当前坐标：--";
        private string _scaleInfo = "比例尺：--";
        private string _packageMode = "待识别";
        private string _structureSummary = "尚未执行压缩包检查。";
        private int _detectedGdbCount;
        private bool _isBusy;
        private bool _isMapReady;

        public MainWindow()
        {
            InitializeComponent();

            _selectedZipPath = string.Empty;

            _layers = new ObservableCollection<GdbLayerInfo>();
            Layers.CollectionChanged += OnLayersCollectionChanged;

            DataContext = this;

            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }

        public ObservableCollection<GdbLayerInfo> Layers
        {
            get { return _layers; }
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
                OnPropertyChanged("SelectedLayer");
                OnPropertyChanged("CanLocateSelectedLayer");
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
                OnPropertyChanged("IsAnalyzeEnabled");
                OnPropertyChanged("IsNotBusy");
                OnPropertyChanged("CanLocateSelectedLayer");
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

        public int DisplayableLayerCount
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

        public int VisibleLayerCount
        {
            get
            {
                var count = 0;
                foreach (var layer in Layers)
                {
                    if (layer.Displayable && layer.IsVisible)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool CanLocateSelectedLayer
        {
            get { return !IsBusy && IsMapReady && SelectedLayer != null && SelectedLayer.Displayable; }
        }

        public bool IsMapReady
        {
            get { return _isMapReady; }
            private set
            {
                if (_isMapReady == value)
                {
                    return;
                }

                _isMapReady = value;
                OnPropertyChanged("IsMapReady");
                OnPropertyChanged("CanLocateSelectedLayer");
            }
        }

        public string MouseCoordinate
        {
            get { return _mouseCoordinate; }
            private set
            {
                if (_mouseCoordinate == value)
                {
                    return;
                }

                _mouseCoordinate = value ?? string.Empty;
                OnPropertyChanged("MouseCoordinate");
            }
        }

        public string ScaleInfo
        {
            get { return _scaleInfo; }
            private set
            {
                if (_scaleInfo == value)
                {
                    return;
                }

                _scaleInfo = value ?? string.Empty;
                OnPropertyChanged("ScaleInfo");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            InitializeMapControl();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            DisposeMapControl();
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
                StatusMessage = "已选择压缩包，点击“开始检查”分析结构并加载图层。";
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

                var aggregated = new List<GdbLayerInfo>();

                StatusMessage = "正在读取 .gdb 图层信息...";
                foreach (var gdb in gdbDirectories)
                {
                    token.ThrowIfCancellationRequested();
                    var layers = await _layerProvider.LoadLayersAsync(gdb, token);
                    aggregated.AddRange(layers);
                }

                foreach (var layer in aggregated)
                {
                    Layers.Add(layer);
                }

                StatusMessage = string.Format("{0}处理完成：识别到 {1} 个 .gdb，共 {2} 个图层，可显示 {3} 个。",
                    PackageMode == "标准测试包" ? "标准包" : "普通包",
                    DetectedGdbCount,
                    LayerCount,
                    DisplayableLayerCount);
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            ClearResults();
            StatusMessage = "已清空结果。";
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

            RemoveAllMapLayers();

            if (_currentExtraction != null)
            {
                _currentExtraction.Dispose();
            }
            _currentExtraction = null;

            ResetInspectionSummary();
        }

        private void ResetInspectionSummary()
        {
            DetectedGdbCount = 0;
            PackageMode = "待识别";
            StructureSummary = "尚未执行压缩包检查。";
            NotifyLayerStatisticsChanged();
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
            StructureSummary = "结构校验通过：已识别 测试1、测试2、测试3 目录，并优先分析 测试1 中的 .gdb 数据。";
            return (IReadOnlyList<string>)Directory.GetDirectories(test1Path, "*.gdb", SearchOption.AllDirectories);
        }

        private void NotifyLayerStatisticsChanged()
        {
            OnPropertyChanged("LayerCount");
            OnPropertyChanged("DisplayableLayerCount");
            OnPropertyChanged("VisibleLayerCount");
            OnPropertyChanged("CanLocateSelectedLayer");
        }

        private void InitializeMapControl()
        {
            if (_mapControl != null)
            {
                if (MapHost.Child == null)
                {
                    MapHost.Child = _mapControl;
                }

                ConfigureMapNavigation();
                IsMapReady = true;
                return;
            }

            _mapControl = new AxMapControl();
            _mapControl.BeginInit();
            MapHost.Child = _mapControl;
            _mapControl.EndInit();
            ConfigureMapNavigation();
            _mapControl.OnMouseMove += OnMapMouseMove;
            _mapControl.OnExtentUpdated += OnMapExtentUpdated;
            IsMapReady = true;
            UpdateScaleInfo();
        }

        private void ConfigureMapNavigation()
        {
            if (_mapControl == null)
            {
                return;
            }

            if (_mapPanTool == null)
            {
                _mapPanTool = new ControlsMapPanToolClass();
                ESRI.ArcGIS.SystemUI.ICommand command = (ESRI.ArcGIS.SystemUI.ICommand)_mapPanTool;
                command.OnCreate(_mapControl.Object);
            }

            _mapControl.CurrentTool = _mapPanTool;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerPan;
            _mapControl.AutoMouseWheel = true;
        }

        private void DisposeMapControl()
        {
            RemoveAllMapLayers();

            if (_mapControl != null)
            {
                _mapControl.CurrentTool = null;
                _mapControl.OnMouseMove -= OnMapMouseMove;
                _mapControl.OnExtentUpdated -= OnMapExtentUpdated;
                MapHost.Child = null;
                _mapControl.Dispose();
                _mapControl = null;
            }

            if (_mapPanTool != null)
            {
                Marshal.ReleaseComObject(_mapPanTool);
                _mapPanTool = null;
            }

            IsMapReady = false;
            MouseCoordinate = "当前坐标：--";
            ScaleInfo = "比例尺：--";
        }

        private void OnLayersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GdbLayerInfo info in e.NewItems)
                {
                    info.PropertyChanged += OnLayerPropertyChanged;
                    if (info.Displayable && info.IsVisible)
                    {
                        UpdateLayerVisibility(info);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (GdbLayerInfo info in e.OldItems)
                {
                    info.PropertyChanged -= OnLayerPropertyChanged;
                    RemoveLayerFromMap(info);
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
            if (e.PropertyName == "IsVisible")
            {
                var info = sender as GdbLayerInfo;
                if (info != null)
                {
                    UpdateLayerVisibility(info);
                }
            }

            if (e.PropertyName == "IsVisible" || e.PropertyName == "Displayable")
            {
                NotifyLayerStatisticsChanged();
            }
        }

        private void UpdateLayerVisibility(GdbLayerInfo info)
        {
            if (_mapControl == null)
            {
                return;
            }

            if (!info.Displayable)
            {
                RemoveLayerFromMap(info);
                return;
            }

            if (info.IsVisible)
            {
                ILayer existing;
                if (_mapLayers.TryGetValue(info, out existing))
                {
                    existing.Visible = true;
                    _mapControl.Refresh();
                    return;
                }

                var newLayer = _layerProvider.CreateFeatureLayer(info);
                _mapLayers[info] = newLayer;
                _mapControl.Map.AddLayer(newLayer);

                if (_mapLayers.Count == 1)
                {
                    _mapControl.Extent = newLayer.AreaOfInterest;
                }

                _mapControl.Refresh();
                UpdateScaleInfo();
            }
            else
            {
                RemoveLayerFromMap(info);
            }
        }

        private void RemoveAllMapLayers()
        {
            if (_mapControl == null)
            {
                return;
            }

            foreach (var kvp in _mapLayers)
            {
                _mapControl.Map.DeleteLayer(kvp.Value);
                Marshal.ReleaseComObject(kvp.Value);
            }

            _mapLayers.Clear();
            _mapControl.Refresh();
            UpdateScaleInfo();
        }

        private void RemoveLayerFromMap(GdbLayerInfo info)
        {
            if (_mapControl == null)
            {
                return;
            }

            ILayer layer;
            if (_mapLayers.TryGetValue(info, out layer))
            {
                _mapControl.Map.DeleteLayer(layer);
                Marshal.ReleaseComObject(layer);
                _mapLayers.Remove(info);
                _mapControl.Refresh();
                UpdateScaleInfo();
            }
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            ZoomMap(0.5);
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            ZoomMap(2.0);
        }

        private void OnZoomFullExtentClick(object sender, RoutedEventArgs e)
        {
            ZoomToAllLayers();
        }

        private void OnLayersGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null)
            {
                return;
            }

            var info = grid.SelectedItem as GdbLayerInfo;
            if (info != null)
            {
                ZoomToLayer(info);
            }
        }

        private void OnLocateSelectedLayerClick(object sender, RoutedEventArgs e)
        {
            if (SelectedLayer != null)
            {
                ZoomToLayer(SelectedLayer);
            }
        }

        private void ZoomToAllLayers()
        {
            if (_mapControl == null)
            {
                return;
            }

            IEnvelope fullExtent = null;

            foreach (var layer in _mapLayers.Values)
            {
                var area = layer.AreaOfInterest;
                if (area == null || area.IsEmpty)
                {
                    continue;
                }

                if (fullExtent == null)
                {
                    fullExtent = new EnvelopeClass();
                    fullExtent.Union(area);
                }
                else
                {
                    fullExtent.Union(area);
                }
            }

            if (fullExtent == null || fullExtent.IsEmpty)
            {
                if (_mapControl.Map.LayerCount > 0)
                {
                    var fallback = _mapControl.Map.get_Layer(0);
                    if (fallback != null)
                    {
                        var fallbackArea = fallback.AreaOfInterest;
                        if (fallbackArea != null && !fallbackArea.IsEmpty)
                        {
                            fullExtent = new EnvelopeClass();
                            fullExtent.Union(fallbackArea);
                        }
                    }
                }
            }

            if (fullExtent == null || fullExtent.IsEmpty)
            {
                return;
            }

            _mapControl.Extent = fullExtent;
            _mapControl.Refresh();
            UpdateScaleInfo();
        }

        private void ZoomToLayer(GdbLayerInfo info)
        {
            if (_mapControl == null)
            {
                return;
            }

            if (!info.Displayable)
            {
            StatusMessage = string.Format("{0} 无法在地图上显示。", info.LayerName);
                return;
            }

            if (!info.IsVisible)
            {
                info.IsVisible = true;
            }

            ILayer layer;
            if (!_mapLayers.TryGetValue(info, out layer))
            {
                UpdateLayerVisibility(info);
                if (!_mapLayers.TryGetValue(info, out layer))
                {
                    return;
                }
            }

            var area = layer.AreaOfInterest;
            if (area == null || area.IsEmpty)
            {
                return;
            }

            _mapControl.Extent = CloneEnvelope(area);
            _mapControl.Refresh();
            UpdateScaleInfo();
            StatusMessage = string.Format("已定位到图层：{0}", info.LayerName);
        }

        private void OnMapMouseMove(object sender, IMapControlEvents2_OnMouseMoveEvent e)
        {
            MouseCoordinate = string.Format("X: {0:0.###}  Y: {1:0.###}", e.mapX, e.mapY);
        }

        private void OnMapExtentUpdated(object sender, IMapControlEvents2_OnExtentUpdatedEvent e)
        {
            UpdateScaleInfo();
        }

        private void UpdateScaleInfo()
        {
            if (_mapControl == null)
            {
                ScaleInfo = "比例尺：--";
                return;
            }

            var scale = _mapControl.MapScale;
            if (scale <= 0)
            {
                ScaleInfo = "比例尺：--";
                return;
            }

            ScaleInfo = string.Format("比例尺 1:{0:N0}", scale);
        }

        private void ZoomMap(double factor)
        {
            if (_mapControl == null)
            {
                return;
            }

            var extent = _mapControl.Extent;
            if (extent == null || extent.IsEmpty)
            {
                return;
            }

            var newExtent = CloneEnvelope(extent);
            newExtent.Expand(factor, factor, true);
            _mapControl.Extent = newExtent;
            _mapControl.Refresh();
            UpdateScaleInfo();
        }

        private static IEnvelope CloneEnvelope(IEnvelope source)
        {
            var envelope = new EnvelopeClass();
            double xMin, yMin, xMax, yMax;
            source.QueryCoords(out xMin, out yMin, out xMax, out yMax);
            envelope.PutCoords(xMin, yMin, xMax, yMax);
            return envelope;
        }

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
