using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 参数配置页面视图模型，用于维护工艺参数与初始化下发参数。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 ParameterConfigView 创建为 DataContext，负责参数加载、保存与默认值恢复。
    /// </remarks>
    public class ParameterConfigViewModel : BaseViewModel
    {
        private const string ConfigFileName = "ProcessParameterConfig.json";
        private readonly ConfigService<ProcessParameterConfig> _configService = new(ConfigFileName);

        private double _heatingBoxTemperature = 60.0;
        private double _quantitativeLoopTemperature = 80.0;
        private double _transferLineTemperature = 120.0;
        private int _shakeDurationSeconds = 10;
        private int _zDropNeedleRiseSlowSpeed = 0;
        private int _pipetteAspirateDelay100ms = 0;
        private int _pipetteDispenseDelay100ms = 0;
        private int _tubeShakeHomeDelay100ms = 0;
        private int _tubeShakeWorkDelay100ms = 0;
        private int _tubeShakeTargetCount = 0;
        private int _headspaceShakeHomeDelay100ms = 0;
        private int _headspaceShakeWorkDelay100ms = 0;
        private int _headspaceShakeTargetCount = 0;
        private int _butanolAspirateDelay100ms = 0;
        private int _butanolDispenseDelay100ms = 0;
        private int _sampleBottlePressureTime100ms = 0;
        private int _quantitativeLoopBalanceTime100ms = 0;
        private int _injectionTime100ms = 0;
        private int _sampleBottlePressurePosition = 0;
        private int _quantitativeLoopBalancePosition = 0;
        private int _injectionPosition = 0;
        private string _statusMessage = "参数配置已加载。";

        public double HeatingBoxTemperature
        {
            get => _heatingBoxTemperature;
            set
            {
                if (Math.Abs(_heatingBoxTemperature - value) > 0.000001d)
                {
                    _heatingBoxTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public double QuantitativeLoopTemperature
        {
            get => _quantitativeLoopTemperature;
            set
            {
                if (Math.Abs(_quantitativeLoopTemperature - value) > 0.000001d)
                {
                    _quantitativeLoopTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TransferLineTemperature
        {
            get => _transferLineTemperature;
            set
            {
                if (Math.Abs(_transferLineTemperature - value) > 0.000001d)
                {
                    _transferLineTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ShakeDurationSeconds
        {
            get => _shakeDurationSeconds;
            set
            {
                if (_shakeDurationSeconds != value)
                {
                    _shakeDurationSeconds = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ZDropNeedleRiseSlowSpeed
        {
            get => _zDropNeedleRiseSlowSpeed;
            set
            {
                if (_zDropNeedleRiseSlowSpeed != value)
                {
                    _zDropNeedleRiseSlowSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PipetteAspirateDelay100ms
        {
            get => _pipetteAspirateDelay100ms;
            set
            {
                if (_pipetteAspirateDelay100ms != value)
                {
                    _pipetteAspirateDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PipetteDispenseDelay100ms
        {
            get => _pipetteDispenseDelay100ms;
            set
            {
                if (_pipetteDispenseDelay100ms != value)
                {
                    _pipetteDispenseDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TubeShakeHomeDelay100ms
        {
            get => _tubeShakeHomeDelay100ms;
            set
            {
                if (_tubeShakeHomeDelay100ms != value)
                {
                    _tubeShakeHomeDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TubeShakeWorkDelay100ms
        {
            get => _tubeShakeWorkDelay100ms;
            set
            {
                if (_tubeShakeWorkDelay100ms != value)
                {
                    _tubeShakeWorkDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TubeShakeTargetCount
        {
            get => _tubeShakeTargetCount;
            set
            {
                if (_tubeShakeTargetCount != value)
                {
                    _tubeShakeTargetCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int HeadspaceShakeHomeDelay100ms
        {
            get => _headspaceShakeHomeDelay100ms;
            set
            {
                if (_headspaceShakeHomeDelay100ms != value)
                {
                    _headspaceShakeHomeDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int HeadspaceShakeWorkDelay100ms
        {
            get => _headspaceShakeWorkDelay100ms;
            set
            {
                if (_headspaceShakeWorkDelay100ms != value)
                {
                    _headspaceShakeWorkDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int HeadspaceShakeTargetCount
        {
            get => _headspaceShakeTargetCount;
            set
            {
                if (_headspaceShakeTargetCount != value)
                {
                    _headspaceShakeTargetCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ButanolAspirateDelay100ms
        {
            get => _butanolAspirateDelay100ms;
            set
            {
                if (_butanolAspirateDelay100ms != value)
                {
                    _butanolAspirateDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ButanolDispenseDelay100ms
        {
            get => _butanolDispenseDelay100ms;
            set
            {
                if (_butanolDispenseDelay100ms != value)
                {
                    _butanolDispenseDelay100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SampleBottlePressureTime100ms
        {
            get => _sampleBottlePressureTime100ms;
            set
            {
                if (_sampleBottlePressureTime100ms != value)
                {
                    _sampleBottlePressureTime100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int QuantitativeLoopBalanceTime100ms
        {
            get => _quantitativeLoopBalanceTime100ms;
            set
            {
                if (_quantitativeLoopBalanceTime100ms != value)
                {
                    _quantitativeLoopBalanceTime100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int InjectionTime100ms
        {
            get => _injectionTime100ms;
            set
            {
                if (_injectionTime100ms != value)
                {
                    _injectionTime100ms = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SampleBottlePressurePosition
        {
            get => _sampleBottlePressurePosition;
            set
            {
                if (_sampleBottlePressurePosition != value)
                {
                    _sampleBottlePressurePosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public int QuantitativeLoopBalancePosition
        {
            get => _quantitativeLoopBalancePosition;
            set
            {
                if (_quantitativeLoopBalancePosition != value)
                {
                    _quantitativeLoopBalancePosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public int InjectionPosition
        {
            get => _injectionPosition;
            set
            {
                if (_injectionPosition != value)
                {
                    _injectionPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }
        public ICommand ResetDefaultCommand { get; }

        /// <summary>
        /// 初始化参数配置视图模型并绑定页面命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面初始化调用，构造完成后会立即执行 LoadConfig 加载本地参数。
        /// </remarks>
        public ParameterConfigViewModel()
        {
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => LoadConfig());
            ResetDefaultCommand = new RelayCommand(_ => ResetDefault());

            LoadConfig();
        }

        /// <summary>
        /// 从配置文件读取工艺参数并刷新页面显示值。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数和“重新加载”按钮调用，用于恢复已保存参数。
        /// </remarks>
        private void LoadConfig()
        {
            try
            {
                ProcessParameterConfig config = _configService.Load() ?? new ProcessParameterConfig();
                HeatingBoxTemperature = config.HeatingBoxTemperature;
                QuantitativeLoopTemperature = config.QuantitativeLoopTemperature;
                TransferLineTemperature = config.TransferLineTemperature;
                ShakeDurationSeconds = config.ShakeDurationSeconds;
                ZDropNeedleRiseSlowSpeed = config.ZDropNeedleRiseSlowSpeed;
                PipetteAspirateDelay100ms = config.PipetteAspirateDelay100ms;
                PipetteDispenseDelay100ms = config.PipetteDispenseDelay100ms;
                TubeShakeHomeDelay100ms = config.TubeShakeHomeDelay100ms;
                TubeShakeWorkDelay100ms = config.TubeShakeWorkDelay100ms;
                TubeShakeTargetCount = config.TubeShakeTargetCount;
                HeadspaceShakeHomeDelay100ms = config.HeadspaceShakeHomeDelay100ms;
                HeadspaceShakeWorkDelay100ms = config.HeadspaceShakeWorkDelay100ms;
                HeadspaceShakeTargetCount = config.HeadspaceShakeTargetCount;
                ButanolAspirateDelay100ms = config.ButanolAspirateDelay100ms;
                ButanolDispenseDelay100ms = config.ButanolDispenseDelay100ms;
                SampleBottlePressureTime100ms = config.SampleBottlePressureTime100ms;
                QuantitativeLoopBalanceTime100ms = config.QuantitativeLoopBalanceTime100ms;
                InjectionTime100ms = config.InjectionTime100ms;
                SampleBottlePressurePosition = config.SampleBottlePressurePosition;
                QuantitativeLoopBalancePosition = config.QuantitativeLoopBalancePosition;
                InjectionPosition = config.InjectionPosition;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 参数配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 加载失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 将当前页面参数保存到配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“保存参数”按钮调用，保存前会对非负数参数执行最小值约束。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                ProcessParameterConfig config = new()
                {
                    HeatingBoxTemperature = HeatingBoxTemperature,
                    QuantitativeLoopTemperature = QuantitativeLoopTemperature,
                    TransferLineTemperature = TransferLineTemperature,
                    ShakeDurationSeconds = Math.Max(0, ShakeDurationSeconds),
                    ZDropNeedleRiseSlowSpeed = Math.Max(0, ZDropNeedleRiseSlowSpeed),
                    PipetteAspirateDelay100ms = Math.Max(0, PipetteAspirateDelay100ms),
                    PipetteDispenseDelay100ms = Math.Max(0, PipetteDispenseDelay100ms),
                    TubeShakeHomeDelay100ms = Math.Max(0, TubeShakeHomeDelay100ms),
                    TubeShakeWorkDelay100ms = Math.Max(0, TubeShakeWorkDelay100ms),
                    TubeShakeTargetCount = Math.Max(0, TubeShakeTargetCount),
                    HeadspaceShakeHomeDelay100ms = Math.Max(0, HeadspaceShakeHomeDelay100ms),
                    HeadspaceShakeWorkDelay100ms = Math.Max(0, HeadspaceShakeWorkDelay100ms),
                    HeadspaceShakeTargetCount = Math.Max(0, HeadspaceShakeTargetCount),
                    ButanolAspirateDelay100ms = Math.Max(0, ButanolAspirateDelay100ms),
                    ButanolDispenseDelay100ms = Math.Max(0, ButanolDispenseDelay100ms),
                    SampleBottlePressureTime100ms = Math.Max(0, SampleBottlePressureTime100ms),
                    QuantitativeLoopBalanceTime100ms = Math.Max(0, QuantitativeLoopBalanceTime100ms),
                    InjectionTime100ms = Math.Max(0, InjectionTime100ms),
                    SampleBottlePressurePosition = Math.Max(0, SampleBottlePressurePosition),
                    QuantitativeLoopBalancePosition = Math.Max(0, QuantitativeLoopBalancePosition),
                    InjectionPosition = Math.Max(0, InjectionPosition)
                };

                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 参数配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 将页面参数恢复为系统默认值。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“恢复默认”按钮调用，仅更新界面数据，需手动保存才会写入配置文件。
        /// </remarks>
        private void ResetDefault()
        {
            HeatingBoxTemperature = 60.0;
            QuantitativeLoopTemperature = 80.0;
            TransferLineTemperature = 120.0;
            ShakeDurationSeconds = 10;
            ZDropNeedleRiseSlowSpeed = 0;
            PipetteAspirateDelay100ms = 0;
            PipetteDispenseDelay100ms = 0;
            TubeShakeHomeDelay100ms = 0;
            TubeShakeWorkDelay100ms = 0;
            TubeShakeTargetCount = 0;
            HeadspaceShakeHomeDelay100ms = 0;
            HeadspaceShakeWorkDelay100ms = 0;
            HeadspaceShakeTargetCount = 0;
            ButanolAspirateDelay100ms = 0;
            ButanolDispenseDelay100ms = 0;
            SampleBottlePressureTime100ms = 0;
            QuantitativeLoopBalanceTime100ms = 0;
            InjectionTime100ms = 0;
            SampleBottlePressurePosition = 0;
            QuantitativeLoopBalancePosition = 0;
            InjectionPosition = 0;
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已恢复默认值（未保存）。";
        }
    }
}
