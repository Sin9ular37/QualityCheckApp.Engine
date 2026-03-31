using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Win32;

using QualityCheckApp.Engine.Models;
using QualityCheckApp.Engine.Services;

namespace QualityCheckApp.Engine
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ZipPackageService _zipService = new ZipPackageService();
        private readonly GdbDirectoryInspectorService _gdbInspectorService = new GdbDirectoryInspectorService();
        private readonly ITopologyCheckService _topologyCheckService = new FileTopologyCheckService();
        private readonly OpenSourceModuleCatalogService _openSourceModuleCatalogService = new OpenSourceModuleCatalogService();
        private readonly ObservableCollection<GdbLayerInfo> _layers;
        private readonly ObservableCollection<OpenSourceModuleInfo> _openSourceModules;
        private readonly ObservableCollection<TopologyIssueInfo> _topologyIssues;

        private ZipExtractionResult _currentExtraction;
        private GdbLayerInfo _selectedLayer;
        private TopologyIssueInfo _selectedTopologyIssue;
        private string _selectedZipPath;
        private string _statusMessage = "请选择 ZIP 压缩包。当前分支已移除 ArcGIS 运行时依赖。";
        private string _packageMode = "待识别";
        private string _structureSummary = "尚未执行压缩包检查。";
        private string _topologySummary = "请选择 GDB 容器并执行目录检查。";
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
                OnPropertyChanged("SelectedLayer");
                OnPropertyChanged("SelectedLayerDisplayName");
                OnPropertyChanged("SelectedLayerSummary");
                OnPropertyChanged("CanRunTopologyCheck");
            }
        }

        public string SelectedLayerDisplayName
        {
            get { return SelectedLayer == null ? "未选择 GDB 容器" : SelectedLayer.LayerName; }
        }

        public string SelectedLayerSummary
        {
            get
            {
                return SelectedLayer == null
                    ? "当前分支只做 ZIP 与 .gdb 目录级检查，不再依赖 ArcGIS Engine。"
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
                return string.Format("已建立 {0}/{1} 个开源替代模块骨架：GDAL/OGR 读取、NetTopologySuite 校验、Mapsui 视口已留好接口，ProjNet 作为后续补充。",
                    ScaffoldedOpenSourceModuleCount,
                    PlannedOpenSourceModuleCount);
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
                OnPropertyChanged("IsNotBusy");
                OnPropertyChanged("IsAnalyzeEnabled");
                OnPropertyChanged("CanRunTopologyCheck");
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
                StatusMessage = "已选择压缩包，点击“开始检查”分析 ZIP 结构与 GDB 容器。";
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
                StatusMessage = "请先在 GDB 列表中选择一个容器。";
                return;
            }

            await RunWithBusyIndicator(async token =>
            {
                StatusMessage = string.Format("正在检查容器 {0} 的目录完整性...", SelectedLayer.LayerName);
                ClearTopologyResults();

                var result = await _topologyCheckService.CheckLayerAsync(SelectedLayer, token);
                ApplyTopologyResult(result);
                StatusMessage = TopologySummary;
            });
        }

        private void OnClearTopologyClick(object sender, RoutedEventArgs e)
        {
            ClearTopologyResults();
            StatusMessage = "已清空目录检查结果。";
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
            TopologySummary = "请选择 GDB 容器并执行目录检查。";
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
            StructureSummary = "结构校验通过：已识别 测试1、测试2、测试3 目录，并优先分析 测试1 中的 .gdb 容器。";
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
