using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QualityCheckApp.Engine.Models
{
    /// <summary>
    /// 表示一个来自 .gdb 的图层信息。
    /// </summary>
    public class GdbLayerInfo : INotifyPropertyChanged
    {
        private string _gdbPath;
        private string _datasetName;
        private string _layerName;
        private string _geometryType;
        private bool _displayable;
        private bool _isVisible;

        public GdbLayerInfo()
        {
            _gdbPath = string.Empty;
            _datasetName = string.Empty;
            _layerName = string.Empty;
            _geometryType = "未知";
            _displayable = false;
            _isVisible = false;
        }

        public string GdbPath
        {
            get { return _gdbPath; }
            set
            {
                if (_gdbPath == value)
                {
                    return;
                }

                _gdbPath = value ?? string.Empty;
                OnPropertyChanged("GdbPath");
            }
        }

        public string DatasetName
        {
            get { return _datasetName; }
            set
            {
                if (_datasetName == value)
                {
                    return;
                }

                _datasetName = value ?? string.Empty;
                OnPropertyChanged("DatasetName");
            }
        }

        public string LayerName
        {
            get { return _layerName; }
            set
            {
                if (_layerName == value)
                {
                    return;
                }

                _layerName = value ?? string.Empty;
                OnPropertyChanged("LayerName");
            }
        }

        public string GeometryType
        {
            get { return _geometryType; }
            set
            {
                if (_geometryType == value)
                {
                    return;
                }

                _geometryType = value ?? "未知";
                OnPropertyChanged("GeometryType");
            }
        }

        public bool Displayable
        {
            get { return _displayable; }
            set
            {
                if (_displayable == value)
                {
                    return;
                }

                _displayable = value;
                OnPropertyChanged("Displayable");
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible == value)
                {
                    return;
                }

                _isVisible = value;
                OnPropertyChanged("IsVisible");
            }
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
