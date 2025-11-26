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
        private ZipExtractionResult _currentExtraction;
        private string _selectedZipPath;
        private string _statusMessage = "请选择包含 File Geodatabase (.gdb) 的 ZIP 文件。";
        private string _mouseCoordinate = "当前坐标：--";
        private string _scaleInfo = "比例尺：--";
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
                OnPropertyChanged("IsAnalyzeEnabled");
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
                StatusMessage = "已选择压缩包，点击“解析并加载”开始处理。";
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

                if (_currentExtraction.GdbDirectories.Count == 0)
                {
                    StatusMessage = "压缩包中没有找到 .gdb 目录。";
                    return;
                }

                var aggregated = new List<GdbLayerInfo>();

                foreach (var gdb in _currentExtraction.GdbDirectories)
                {
                    token.ThrowIfCancellationRequested();
                    var layers = await _layerProvider.LoadLayersAsync(gdb, token);
                    aggregated.AddRange(layers);
                }

                foreach (var layer in aggregated)
                {
                    Layers.Add(layer);
                }

                StatusMessage = string.Format("解析完成，共发现 {0} 个图层。", Layers.Count);
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
        }

        private void InitializeMapControl()
        {
            if (_mapControl != null)
            {
                if (MapHost.Child == null)
                {
                    MapHost.Child = _mapControl;
                }
                IsMapReady = true;
                return;
            }

            _mapControl = new AxMapControl();
            _mapControl.BeginInit();
            MapHost.Child = _mapControl;
            _mapControl.EndInit();
            _mapControl.OnMouseMove += OnMapMouseMove;
            _mapControl.OnExtentUpdated += OnMapExtentUpdated;
            IsMapReady = true;
            UpdateScaleInfo();
        }

        private void DisposeMapControl()
        {
            RemoveAllMapLayers();

            if (_mapControl != null)
            {
                _mapControl.OnMouseMove -= OnMapMouseMove;
                _mapControl.OnExtentUpdated -= OnMapExtentUpdated;
                MapHost.Child = null;
                _mapControl.Dispose();
                _mapControl = null;
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
