using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using Microsoft.Win32;
using QualityCheckApp.Engine.Models;
using QualityCheckApp.Engine.Services;
using WinForms = System.Windows.Forms;

namespace QualityCheckApp.Integration
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ZipPackageService _zipService = new ZipPackageService();
        private readonly ArcGisLayerProvider _layerProvider = new ArcGisLayerProvider();
        private readonly ObservableCollection<GdbLayerInfo> _layers = new ObservableCollection<GdbLayerInfo>();

        private ZipExtractionResult _currentExtraction;
        private string _selectedZipPath = string.Empty;
        private string _statusMessage = "请先选择包含测试目录的 ZIP 压缩包";
        private bool _isBusy;
        private bool _isValidationSuccessful;
        private int _activeMapWindows;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _layers.CollectionChanged += OnLayersCollectionChanged;
            StatusMessage = _statusMessage;
            Closed += OnWindowClosed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<GdbLayerInfo> Layers
        {
            get { return _layers; }
        }

        public string SelectedZipPath
        {
            get { return _selectedZipPath; }
            set
            {
                var newValue = value ?? string.Empty;
                if (string.Equals(_selectedZipPath, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedZipPath = newValue;
                OnPropertyChanged();
                OnPropertyChanged("CanAnalyze");
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                var newValue = value ?? string.Empty;
                if (string.Equals(_statusMessage, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _statusMessage = newValue;
                OnPropertyChanged();
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
                OnPropertyChanged();
                OnPropertyChanged("IsNotBusy");
                OnPropertyChanged("CanAnalyze");
                OnPropertyChanged("CanViewDetails");
            }
        }

        public bool IsNotBusy
        {
            get { return !IsBusy; }
        }

        public bool CanAnalyze
        {
            get { return !IsBusy && !string.IsNullOrWhiteSpace(SelectedZipPath); }
        }

        public bool CanViewDetails
        {
            get { return !IsBusy && _isValidationSuccessful && Layers.Count > 0; }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            DisposeExtraction();
        }

        private void OnLayersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("CanViewDetails");
        }

        private void OnSelectZipClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZIP 压缩包 (*.zip)|*.zip",
                Title = "选择待校验的压缩包"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedZipPath = dialog.FileName;
                StatusMessage = "已选择压缩包，点击“校验并解析”开始检查内容。";
            }
        }

        private async void OnValidateZipClick(object sender, RoutedEventArgs e)
        {
            if (!CanAnalyze)
            {
                StatusMessage = "请先选择有效的 ZIP 压缩包。";
                return;
            }

            if (_activeMapWindows > 0)
            {
                StatusMessage = "已打开地图详情窗口，请先关闭后再执行新的校验。";
                return;
            }

            await RunWithBusyIndicator(async token =>
            {
                ClearResultsInternal();

                StatusMessage = "正在解压压缩包...";
                _currentExtraction = await _zipService.ExtractAsync(SelectedZipPath, token);

                string test1Path;
                string errorMessage;
                if (!TryValidateStructure(_currentExtraction.ExtractionRoot, out test1Path, out errorMessage))
                {
                    StatusMessage = errorMessage;
                    return;
                }

                var gdbDirectories = Directory.GetDirectories(test1Path, "*.gdb", SearchOption.AllDirectories);
                if (gdbDirectories.Length == 0)
                {
                    StatusMessage = "测试1 目录中未检测到 .gdb 数据。";
                    return;
                }

                StatusMessage = "正在读取 .gdb 图层信息...";
                var aggregated = new List<GdbLayerInfo>();

                foreach (var gdb in gdbDirectories)
                {
                    token.ThrowIfCancellationRequested();
                    var layers = await _layerProvider.LoadLayersAsync(gdb, token);
                    aggregated.AddRange(layers);
                }

                Layers.Clear();
                foreach (var layer in aggregated)
                {
                    Layers.Add(layer);
                }

                _isValidationSuccessful = true;
                OnPropertyChanged("CanViewDetails");

                StatusMessage = string.Format("校验成功：检测到 {0} 个 .gdb，共 {1} 个图层。",
                    gdbDirectories.Length,
                    Layers.Count);
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            if (_activeMapWindows > 0)
            {
                StatusMessage = "请先关闭地图详情窗口，再清空结果。";
                return;
            }

            ClearResultsInternal();
            StatusMessage = "已清空解析结果。";
        }

        private async Task RunWithBusyIndicator(Func<CancellationToken, Task> action)
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
                    await action(cts.Token);
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

        private void ClearResultsInternal()
        {
            Layers.Clear();
            _isValidationSuccessful = false;
            OnPropertyChanged("CanViewDetails");
            DisposeExtraction();
        }

        private void DisposeExtraction()
        {
            if (_currentExtraction != null)
            {
                _currentExtraction.Dispose();
                _currentExtraction = null;
            }
        }

        private bool TryValidateStructure(string extractionRoot, out string test1Path, out string message)
        {
            test1Path = string.Empty;
            var required = new[] { "测试1", "测试2", "测试3" };

            foreach (var folderName in required)
            {
                var matches = Directory.GetDirectories(extractionRoot, folderName, SearchOption.AllDirectories);
                if (matches.Length == 0)
                {
                    message = string.Format("压缩包缺少必需目录：{0}", folderName);
                    return false;
                }

                if (string.Equals(folderName, "测试1", StringComparison.OrdinalIgnoreCase))
                {
                    test1Path = matches[0];
                }
            }

            if (string.IsNullOrEmpty(test1Path))
            {
                message = "未能定位测试1 目录。";
                return false;
            }

            message = "校验通过";
            return true;
        }

        private void OnViewDetailsClick(object sender, RoutedEventArgs e)
        {
            if (!CanViewDetails)
            {
                StatusMessage = "暂无可查看的图层，请先完成校验。";
                return;
            }

            var layerSnapshot = Layers
                .Select(CloneLayerInfo)
                .ToList()
                .AsReadOnly();

            LaunchMapControl(layerSnapshot);
            StatusMessage = "已启动 MapControl 模块，正在推送图层。";
        }

        private GdbLayerInfo CloneLayerInfo(GdbLayerInfo source)
        {
            return new GdbLayerInfo
            {
                GdbPath = source.GdbPath,
                DatasetName = source.DatasetName,
                LayerName = source.LayerName,
                GeometryType = source.GeometryType,
                Displayable = source.Displayable,
                IsVisible = source.IsVisible
            };
        }

        private void LaunchMapControl(IReadOnlyList<GdbLayerInfo> layers)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    WinForms.Application.EnableVisualStyles();
                    WinForms.Application.SetCompatibleTextRenderingDefault(false);

                    var form = new MapControl.MainForm();
                    form.Load += (s, e) =>
                    {
                        try
                        {
                            PopulateMapControl(form, layers);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                StatusMessage = string.Format("写入地图失败：{0}", ex.Message);
                            }));
                        }
                    };

                    Interlocked.Increment(ref _activeMapWindows);
                    try
                    {
                        WinForms.Application.Run(form);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _activeMapWindows);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusMessage = string.Format("无法启动 MapControl 模块：{0}", ex.Message);
                        Interlocked.Exchange(ref _activeMapWindows, 0);
                    }));
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void PopulateMapControl(MapControl.MainForm form, IReadOnlyList<GdbLayerInfo> layers)
        {
            var axMapControl = GetAxMapControl(form);
            if (axMapControl == null)
            {
                throw new InvalidOperationException("无法找到 MapControl 控件实例。");
            }

            var map = axMapControl.Map;
            if (map == null)
            {
                throw new InvalidOperationException("地图尚未准备就绪。");
            }

            for (int i = map.LayerCount - 1; i >= 0; i--)
            {
                var existingLayer = map.get_Layer(i);
                map.DeleteLayer(existingLayer);
                if (existingLayer != null)
                {
                    Marshal.ReleaseComObject(existingLayer);
                }
            }

            ILayer firstLayer = null;

            foreach (var info in layers)
            {
                if (!info.Displayable)
                {
                    continue;
                }

                var layer = _layerProvider.CreateFeatureLayer(info);
                map.AddLayer(layer);
                if (firstLayer == null)
                {
                    firstLayer = layer;
                }
            }

            if (firstLayer != null)
            {
                axMapControl.Extent = firstLayer.AreaOfInterest;
            }

            axMapControl.Refresh();
        }

        private AxMapControl GetAxMapControl(MapControl.MainForm form)
        {
            var field = typeof(MapControl.MainForm).GetField("axMapControl1", BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(form) as AxMapControl;
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
