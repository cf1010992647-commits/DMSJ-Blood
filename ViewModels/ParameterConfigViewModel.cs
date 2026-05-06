using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private string _heatingBoxTemperatureStation = "01";
        private double _quantitativeLoopTemperature = 80.0;
        private string _quantitativeLoopTemperatureStation = "02";
        private double _transferLineTemperature = 120.0;
        private string _transferLineTemperatureStation = "03";
        private double _reservedTemperature = 60.0;
        private string _reservedTemperatureStation = "04";
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

        public string HeatingBoxTemperatureStation
        {
            get => _heatingBoxTemperatureStation;
            set
            {
                string normalized = value ?? string.Empty;
                if (_heatingBoxTemperatureStation != normalized)
                {
                    _heatingBoxTemperatureStation = normalized;
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

        public string QuantitativeLoopTemperatureStation
        {
            get => _quantitativeLoopTemperatureStation;
            set
            {
                string normalized = value ?? string.Empty;
                if (_quantitativeLoopTemperatureStation != normalized)
                {
                    _quantitativeLoopTemperatureStation = normalized;
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

        public string TransferLineTemperatureStation
        {
            get => _transferLineTemperatureStation;
            set
            {
                string normalized = value ?? string.Empty;
                if (_transferLineTemperatureStation != normalized)
                {
                    _transferLineTemperatureStation = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public double ReservedTemperature
        {
            get => _reservedTemperature;
            set
            {
                if (Math.Abs(_reservedTemperature - value) > 0.000001d)
                {
                    _reservedTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReservedTemperatureStation
        {
            get => _reservedTemperatureStation;
            set
            {
                string normalized = value ?? string.Empty;
                if (_reservedTemperatureStation != normalized)
                {
                    _reservedTemperatureStation = normalized;
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
        public ICommand WriteParameterCommand { get; }

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
            WriteParameterCommand = new AsyncRelayCommand<string>(
                WriteParameterToPlcAsync,
                key => !string.IsNullOrWhiteSpace(key),
                ex => StatusMessage = $"{DateTime.Now:HH:mm:ss} 参数写入失败：{ex.Message}");

            LoadConfig();
        }

        /// <summary>
        /// 按参数键名把当前界面值写入PLC。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameterKey">参数键名。</param>
        /// <returns>返回PLC写入异步任务。</returns>
        /// <remarks>
        /// 由参数配置页每行“写入”按钮调用，写入标题中标注的D寄存器地址。
        /// </remarks>
        private async Task WriteParameterToPlcAsync(string parameterKey)
        {
            if (!CommunicationManager.Is485Open)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} PLC未连接，参数未写入。";
                return;
            }

                switch (parameterKey)
            {
                case nameof(ZDropNeedleRiseSlowSpeed):
                    await WriteInt32ParameterAsync("Z轴_丢枪头_上升慢速速度", 6000, ZDropNeedleRiseSlowSpeed);
                    break;
                case nameof(PipetteAspirateDelay100ms):
                    await WriteSingleRegisterParameterAsync("移液枪吸液延时时间", 6020, PipetteAspirateDelay100ms);
                    break;
                case nameof(PipetteDispenseDelay100ms):
                    await WriteSingleRegisterParameterAsync("移液枪打液延时时间", 6021, PipetteDispenseDelay100ms);
                    break;
                case nameof(TubeShakeHomeDelay100ms):
                    await WriteSingleRegisterParameterAsync("采血管摇匀原位延时时间", 6022, TubeShakeHomeDelay100ms);
                    break;
                case nameof(TubeShakeWorkDelay100ms):
                    await WriteSingleRegisterParameterAsync("采血管摇匀工位延时时间", 6023, TubeShakeWorkDelay100ms);
                    break;
                case nameof(TubeShakeTargetCount):
                    await WriteSingleRegisterParameterAsync("采血管摇匀目标次数", 6024, TubeShakeTargetCount);
                    break;
                case nameof(HeadspaceShakeHomeDelay100ms):
                    await WriteSingleRegisterParameterAsync("顶空瓶摇匀原位延时时间", 6026, HeadspaceShakeHomeDelay100ms);
                    break;
                case nameof(HeadspaceShakeWorkDelay100ms):
                    await WriteSingleRegisterParameterAsync("顶空瓶摇匀工位延时时间", 6027, HeadspaceShakeWorkDelay100ms);
                    break;
                case nameof(HeadspaceShakeTargetCount):
                    await WriteSingleRegisterParameterAsync("顶空瓶摇匀目标次数", 6028, HeadspaceShakeTargetCount);
                    break;
                case nameof(ButanolAspirateDelay100ms):
                    await WriteSingleRegisterParameterAsync("叔丁醇吸液延时时间", 6030, ButanolAspirateDelay100ms);
                    break;
                case nameof(ButanolDispenseDelay100ms):
                    await WriteSingleRegisterParameterAsync("叔丁醇打液延时时间", 6031, ButanolDispenseDelay100ms);
                    break;
                case nameof(SampleBottlePressureTime100ms):
                    await WriteSingleRegisterParameterAsync("样品瓶加压时间", 6040, SampleBottlePressureTime100ms);
                    break;
                case nameof(QuantitativeLoopBalanceTime100ms):
                    await WriteSingleRegisterParameterAsync("定量环平衡时间", 6041, QuantitativeLoopBalanceTime100ms);
                    break;
                case nameof(InjectionTime100ms):
                    await WriteSingleRegisterParameterAsync("进样时间", 6042, InjectionTime100ms);
                    break;
                case nameof(SampleBottlePressurePosition):
                    await WriteInt32ParameterAsync("样品瓶加压位置", 6302, SampleBottlePressurePosition);
                    break;
                case nameof(QuantitativeLoopBalancePosition):
                    await WriteInt32ParameterAsync("定量环平衡位置", 6304, QuantitativeLoopBalancePosition);
                    break;
                case nameof(InjectionPosition):
                    await WriteInt32ParameterAsync("进样位置", 6306, InjectionPosition);
                    break;
                default:
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 未识别的参数：{parameterKey}";
                    break;
            }
        }

        /// <summary>
        /// 写入单寄存器参数到PLC。
        /// </summary>
        /// By:ChengLei
        /// <param name="displayName">参数显示名称。</param>
        /// <param name="address">D寄存器地址。</param>
        /// <param name="value">待写入参数值。</param>
        /// <returns>返回PLC写入异步任务。</returns>
        /// <remarks>
        /// 由 WriteParameterToPlcAsync 写入延时、次数和速度类参数时调用。
        /// </remarks>
        private async Task WriteSingleRegisterParameterAsync(string displayName, ushort address, int value)
        {
            if (value < ushort.MinValue || value > ushort.MaxValue)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {displayName}写入失败：值必须在 0-65535 范围内。";
                return;
            }

            await WriteRegisterWithLockAsync(address, (ushort)value);
            StatusMessage = $"{DateTime.Now:HH:mm:ss} {displayName}已写入PLC，D{address}={value}。";
        }

        /// <summary>
        /// 写入32位参数到PLC。
        /// </summary>
        /// By:ChengLei
        /// <param name="displayName">参数显示名称。</param>
        /// <param name="lowAddress">低16位D寄存器地址。</param>
        /// <param name="value">待写入参数值。</param>
        /// <returns>返回PLC写入异步任务。</returns>
        /// <remarks>
        /// 由 WriteParameterToPlcAsync 写入位置类参数时调用，低位写入D地址，高位写入D地址+1。
        /// </remarks>
        private async Task WriteInt32ParameterAsync(string displayName, ushort lowAddress, int value)
        {
            if (value < 0)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {displayName}写入失败：值不能为负数。";
                return;
            }

            SplitInt32(value, out ushort lowWord, out ushort highWord);
            await WriteRegisterWithLockAsync(lowAddress, lowWord);
            await WriteRegisterWithLockAsync((ushort)(lowAddress + 1), highWord);
            StatusMessage = $"{DateTime.Now:HH:mm:ss} {displayName}已写入PLC，D{lowAddress}/D{lowAddress + 1}={value}。";
        }

        /// <summary>
        /// 在PLC访问锁保护下写入单个寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">D寄存器地址。</param>
        /// <param name="value">待写入寄存器值。</param>
        /// <returns>返回寄存器写入异步任务。</returns>
        /// <remarks>
        /// 由参数写入按钮调用，避免与其他PLC访问并发冲突。
        /// </remarks>
        private static async Task WriteRegisterWithLockAsync(ushort address, ushort value)
        {
            await CommunicationManager.PlcAccessLock.WaitAsync();
            try
            {
                var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(address, value);
                if (!write.Success)
                {
                    throw new InvalidOperationException(write.Error);
                }
            }
            finally
            {
                CommunicationManager.PlcAccessLock.Release();
            }
        }

        /// <summary>
        /// 将Int32拆分为低16位和高16位。
        /// </summary>
        /// By:ChengLei
        /// <param name="value">待拆分数值。</param>
        /// <param name="lowWord">输出低16位寄存器值。</param>
        /// <param name="highWord">输出高16位寄存器值。</param>
        /// <remarks>
        /// 由 WriteInt32ParameterAsync 写入32位参数时调用。
        /// </remarks>
        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                uint raw = (uint)value;
                lowWord = (ushort)(raw & 0xFFFF);
                highWord = (ushort)((raw >> 16) & 0xFFFF);
            }
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
                ApplyConfigValues(config);
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
        /// 由“保存参数”按钮调用，保存前会先规范化站号并严格执行配置校验，校验失败时拒绝落盘。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                ProcessParameterConfig config = BuildConfigSnapshot();
                List<string> validationErrors = config.Validate();
                if (validationErrors.Count > 0)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败：{string.Join("；", validationErrors)}";
                    return;
                }

                _configService.Save(config);
                ApplyConfigValues(config);
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
            HeatingBoxTemperatureStation = "01";
            QuantitativeLoopTemperature = 80.0;
            QuantitativeLoopTemperatureStation = "02";
            TransferLineTemperature = 120.0;
            TransferLineTemperatureStation = "03";
            ReservedTemperature = 60.0;
            ReservedTemperatureStation = "04";
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

        /// <summary>
        /// 规范化温控站号文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">原始站号文本。</param>
        /// <returns>返回保存到配置文件的两位站号文本。</returns>
        /// <remarks>
        /// 允许页面输入一位数字并自动补齐两位；非法文本保持原样交给配置校验拦截。
        /// </remarks>
        private static string NormalizeStation(string? station)
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                return string.Empty;
            }

            string normalized = station.Trim();
            if (!int.TryParse(normalized, out int stationValue) || stationValue < 0 || stationValue > 99)
            {
                return normalized;
            }

            return stationValue.ToString("D2");
        }

        /// <summary>
        /// 根据页面当前输入构建待保存的流程参数快照。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回待校验和保存的流程参数对象。</returns>
        /// <remarks>
        /// 仅做站号规范化，不再静默修正范围错误，确保非法值由配置校验明确拦截。
        /// </remarks>
        private ProcessParameterConfig BuildConfigSnapshot()
        {
            return new ProcessParameterConfig
            {
                HeatingBoxTemperature = HeatingBoxTemperature,
                HeatingBoxTemperatureStation = NormalizeStation(HeatingBoxTemperatureStation),
                QuantitativeLoopTemperature = QuantitativeLoopTemperature,
                QuantitativeLoopTemperatureStation = NormalizeStation(QuantitativeLoopTemperatureStation),
                TransferLineTemperature = TransferLineTemperature,
                TransferLineTemperatureStation = NormalizeStation(TransferLineTemperatureStation),
                ReservedTemperature = ReservedTemperature,
                ReservedTemperatureStation = NormalizeStation(ReservedTemperatureStation),
                ShakeDurationSeconds = ShakeDurationSeconds,
                ZDropNeedleRiseSlowSpeed = ZDropNeedleRiseSlowSpeed,
                PipetteAspirateDelay100ms = PipetteAspirateDelay100ms,
                PipetteDispenseDelay100ms = PipetteDispenseDelay100ms,
                TubeShakeHomeDelay100ms = TubeShakeHomeDelay100ms,
                TubeShakeWorkDelay100ms = TubeShakeWorkDelay100ms,
                TubeShakeTargetCount = TubeShakeTargetCount,
                HeadspaceShakeHomeDelay100ms = HeadspaceShakeHomeDelay100ms,
                HeadspaceShakeWorkDelay100ms = HeadspaceShakeWorkDelay100ms,
                HeadspaceShakeTargetCount = HeadspaceShakeTargetCount,
                ButanolAspirateDelay100ms = ButanolAspirateDelay100ms,
                ButanolDispenseDelay100ms = ButanolDispenseDelay100ms,
                SampleBottlePressureTime100ms = SampleBottlePressureTime100ms,
                QuantitativeLoopBalanceTime100ms = QuantitativeLoopBalanceTime100ms,
                InjectionTime100ms = InjectionTime100ms,
                SampleBottlePressurePosition = SampleBottlePressurePosition,
                QuantitativeLoopBalancePosition = QuantitativeLoopBalancePosition,
                InjectionPosition = InjectionPosition
            };
        }

        /// <summary>
        /// 将流程参数对象回填到页面属性。
        /// </summary>
        /// By:ChengLei
        /// <param name="config">需要回填到页面的流程参数对象。</param>
        /// <remarks>
        /// 由加载配置和保存成功后的规范化回显复用，确保界面与最终落盘内容一致。
        /// </remarks>
        private void ApplyConfigValues(ProcessParameterConfig config)
        {
            HeatingBoxTemperature = config.HeatingBoxTemperature;
            HeatingBoxTemperatureStation = config.HeatingBoxTemperatureStation;
            QuantitativeLoopTemperature = config.QuantitativeLoopTemperature;
            QuantitativeLoopTemperatureStation = config.QuantitativeLoopTemperatureStation;
            TransferLineTemperature = config.TransferLineTemperature;
            TransferLineTemperatureStation = config.TransferLineTemperatureStation;
            ReservedTemperature = config.ReservedTemperature;
            ReservedTemperatureStation = config.ReservedTemperatureStation;
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
        }
    }
}
