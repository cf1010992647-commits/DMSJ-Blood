using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// PLC点位模型，封装点位地址、显示值与状态颜色等绑定字段。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 PointMonitorViewModel 管理并绑定到点位监控列表。
    /// </remarks>
    public class PlcPoint : INotifyPropertyChanged
    {
        private string _address = string.Empty;
        private string _description = string.Empty;
        private int _registerBitWidth = 16;
        private string _valueText = "--";
        private Brush _statusColor = Brushes.Gray;

        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PointTypeText));
                    OnPropertyChanged(nameof(IsWriteSupported));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RegisterBitWidth
        {
            get => _registerBitWidth;
            set
            {
                int normalized = value == 32 ? 32 : 16;
                if (_registerBitWidth != normalized)
                {
                    _registerBitWidth = normalized;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PointTypeText));
                }
            }
        }

        public string ValueText
        {
            get => _valueText;
            set
            {
                if (_valueText != value)
                {
                    _valueText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PointTypeText
        {
            get
            {
                if (Address.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    return "M线圈";
                }

                if (Address.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                {
                    return RegisterBitWidth == 32 ? "D寄存器(32位)" : "D寄存器(16位)";
                }

                return "未知";
            }
        }

        public bool IsWriteSupported => Address.StartsWith("M", StringComparison.OrdinalIgnoreCase);

        public Brush StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知。
        /// </summary>
        /// By:ChengLei
        /// <param name="name">发生变化的属性名。</param>
        /// <remarks>
        /// 由属性 setter 调用，驱动WPF绑定刷新。
        /// </remarks>
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
