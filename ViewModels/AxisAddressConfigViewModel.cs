using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 轴调试点位配置窗口视图模型。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责加载、编辑、校验并保存 AxisDebugAddressConfig.json 中的轴点位地址。
    /// </remarks>
    public sealed class AxisAddressConfigViewModel : BaseViewModel
    {
        private const string AxisAddressConfigFileName = "AxisDebugAddressConfig.json";
        private readonly ConfigService<AxisDebugAddressConfig> _configService = new(AxisAddressConfigFileName);
        private AxisAddressConfigItemViewModel? _selectedAxis;
        private string _statusMessage = "轴点位配置已加载";

        /// <summary>
        /// 初始化轴点位配置视图模型并加载配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 AxisAddressConfigWindow 创建时调用。
        /// </remarks>
        public AxisAddressConfigViewModel()
        {
            ReloadCommand = new RelayCommand(_ => ReloadConfig());
            SaveCommand = new RelayCommand(_ => SaveConfig());
            ResetDefaultCommand = new RelayCommand(_ => ResetDefaultConfig());
            ReloadConfig();
        }

        /// <summary>
        /// 获取轴点位配置行集合。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 每一行对应 AxisDebugAddressConfig.json 中的一个轴配置。
        /// </remarks>
        public ObservableCollection<AxisAddressConfigItemViewModel> Axes { get; } = new();

        /// <summary>
        /// 获取或设置当前正在编辑的轴配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由轴切换选项卡绑定，右侧配置区域根据该值显示单轴地址。
        /// </remarks>
        public AxisAddressConfigItemViewModel? SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (_selectedAxis != value)
                {
                    _selectedAxis = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedAxisTitle));
                    StatusMessage = value == null
                        ? "未选择轴配置。"
                        : $"当前编辑 {value.AxisName}，保存配置后会写入本地配置文件。";
                }
            }
        }

        /// <summary>
        /// 获取当前选中轴的窗口标题文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 供窗口头部标题绑定，避免在 XAML 中拼接中文文本。
        /// </remarks>
        public string SelectedAxisTitle => SelectedAxis == null ? "轴点位配置" : $"{SelectedAxis.AxisName}点位配置";

        /// <summary>
        /// 获取重新加载配置命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由窗口重新加载按钮绑定。
        /// </remarks>
        public ICommand ReloadCommand { get; }

        /// <summary>
        /// 获取保存配置命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由窗口保存按钮绑定。
        /// </remarks>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 获取恢复默认配置命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由窗口恢复默认按钮绑定，仅恢复页面内容，保存后才写入文件。
        /// </remarks>
        public ICommand ResetDefaultCommand { get; }

        /// <summary>
        /// 获取或设置状态提示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 保存、加载、校验失败时更新给窗口底部显示。
        /// </remarks>
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

        /// <summary>
        /// 获取本次窗口会话是否保存过配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 窗口关闭后由轴调试页判断是否需要重新加载地址。
        /// </remarks>
        public bool HasSaved { get; private set; }

        /// <summary>
        /// 从配置文件重新加载轴点位地址。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数和重新加载按钮调用，加载失败时回退默认配置并保存。
        /// </remarks>
        private void ReloadConfig()
        {
            try
            {
                AxisDebugAddressConfig config = _configService.Load() ?? new AxisDebugAddressConfig();
                NormalizeConfig(config);
                LoadConfig(config, saveDefault: false);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 轴点位配置已重新加载。";
            }
            catch (Exception ex)
            {
                AxisDebugAddressConfig defaults = new();
                _configService.Save(defaults);
                LoadConfig(defaults, saveDefault: false);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 配置加载失败，已回退默认值: {ex.Message}";
            }
        }

        /// <summary>
        /// 将配置对象加载到页面集合。
        /// </summary>
        /// By:ChengLei
        /// <param name="config">待加载的轴地址配置。</param>
        /// <param name="saveDefault">是否立即保存默认配置。</param>
        /// <remarks>
        /// 由重新加载和恢复默认流程复用。
        /// </remarks>
        private void LoadConfig(AxisDebugAddressConfig config, bool saveDefault)
        {
            NormalizeConfig(config);
            Axes.Clear();
            Axes.Add(new AxisAddressConfigItemViewModel(1, config.Axis1));
            Axes.Add(new AxisAddressConfigItemViewModel(2, config.Axis2));
            Axes.Add(new AxisAddressConfigItemViewModel(3, config.Axis3));
            Axes.Add(new AxisAddressConfigItemViewModel(4, config.Axis4));
            SelectedAxis = Axes.FirstOrDefault();

            if (saveDefault)
            {
                _configService.Save(config);
            }
        }

        /// <summary>
        /// 保存当前页面中的轴点位配置到文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 保存前会执行重复地址和32位寄存器相邻关系校验。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                AxisDebugAddressConfig config = ExportConfig();
                var errors = config.Validate();
                if (errors.Count > 0)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败: {string.Join("；", errors.Take(4))}";
                    return;
                }

                _configService.Save(config);
                HasSaved = true;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 轴点位配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 将页面内容恢复为默认轴点位配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由恢复默认按钮调用，仅更新界面内容，点击保存后才写入配置文件。
        /// </remarks>
        private void ResetDefaultConfig()
        {
            LoadConfig(new AxisDebugAddressConfig(), saveDefault: false);
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已恢复默认点位，保存后生效。";
        }

        /// <summary>
        /// 将页面行集合导出为轴地址配置对象。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回可写入 AxisDebugAddressConfig.json 的配置对象。</returns>
        /// <remarks>
        /// 由保存流程调用。
        /// </remarks>
        private AxisDebugAddressConfig ExportConfig()
        {
            AxisDebugAddressConfig config = new();

            foreach (AxisAddressConfigItemViewModel axis in Axes)
            {
                AxisAddressProfile profile = axis.ToProfile();
                switch (axis.AxisNo)
                {
                    case 1:
                        config.Axis1 = profile;
                        break;
                    case 2:
                        config.Axis2 = profile;
                        break;
                    case 3:
                        config.Axis3 = profile;
                        break;
                    case 4:
                        config.Axis4 = profile;
                        break;
                }
            }

            NormalizeConfig(config);
            return config;
        }

        /// <summary>
        /// 补齐配置中为空的轴配置对象。
        /// </summary>
        /// By:ChengLei
        /// <param name="config">待补齐的轴地址配置。</param>
        /// <remarks>
        /// 兼容历史配置文件缺少新增字段或轴对象为空的情况。
        /// </remarks>
        private static void NormalizeConfig(AxisDebugAddressConfig config)
        {
            AxisDebugAddressConfig defaults = new();
            config.Axis1 ??= defaults.Axis1;
            config.Axis2 ??= defaults.Axis2;
            config.Axis3 ??= defaults.Axis3;
            config.Axis4 ??= defaults.Axis4;
        }
    }

    /// <summary>
    /// 单轴点位配置编辑行。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于 DataGrid 直接编辑 AxisAddressProfile 中的全部 M/D 地址。
    /// </remarks>
    public sealed class AxisAddressConfigItemViewModel : BaseViewModel
    {
        private string _axisName = string.Empty;
        private ushort _jogPlusCoil;
        private ushort _jogMinusCoil;
        private ushort _goHomeCoil;
        private ushort _homeDoneCoil;
        private ushort _positiveLimitCoil;
        private ushort _negativeLimitCoil;
        private ushort _homeSensorCoil;
        private ushort _manualLocateTriggerCoil;
        private ushort _currentPositionLowRegister;
        private ushort _currentPositionHighRegister;
        private ushort _manualSpeedRegister;
        private ushort _manualSpeedHighRegister;
        private ushort _autoSpeedRegister;
        private ushort _autoSpeedHighRegister;
        private ushort _manualTargetLowRegister;
        private ushort _manualTargetHighRegister;
        private ushort _accelerationTimeRegister;
        private ushort _decelerationTimeRegister;

        /// <summary>
        /// 初始化单轴点位配置编辑行。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisNo">轴编号。</param>
        /// <param name="profile">轴地址配置对象。</param>
        /// <remarks>
        /// 由 AxisAddressConfigViewModel 加载配置时创建。
        /// </remarks>
        public AxisAddressConfigItemViewModel(int axisNo, AxisAddressProfile profile)
        {
            AxisNo = axisNo;
            AxisName = profile.AxisName;
            JogPlusCoil = profile.JogPlusCoil;
            JogMinusCoil = profile.JogMinusCoil;
            GoHomeCoil = profile.GoHomeCoil;
            HomeDoneCoil = profile.HomeDoneCoil;
            PositiveLimitCoil = profile.PositiveLimitCoil;
            NegativeLimitCoil = profile.NegativeLimitCoil;
            HomeSensorCoil = profile.HomeSensorCoil;
            ManualLocateTriggerCoil = profile.ManualLocateTriggerCoil;
            CurrentPositionLowRegister = profile.CurrentPositionLowRegister;
            CurrentPositionHighRegister = profile.CurrentPositionHighRegister;
            ManualSpeedRegister = profile.ManualSpeedRegister;
            ManualSpeedHighRegister = profile.ManualSpeedHighRegister;
            AutoSpeedRegister = profile.AutoSpeedRegister;
            AutoSpeedHighRegister = profile.AutoSpeedHighRegister;
            ManualTargetLowRegister = profile.ManualTargetLowRegister;
            ManualTargetHighRegister = profile.ManualTargetHighRegister;
            AccelerationTimeRegister = profile.AccelerationTimeRegister;
            DecelerationTimeRegister = profile.DecelerationTimeRegister;
        }

        /// <summary>
        /// 获取轴编号。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 编号用于保存时映射到 AxisDebugAddressConfig 的 Axis1 到 Axis4。
        /// </remarks>
        public int AxisNo { get; }

        /// <summary>
        /// 获取轴编号显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于界面显示 M1 到 M4，不参与保存。
        /// </remarks>
        public string AxisDisplayName => $"M{AxisNo}";

        /// <summary>
        /// 获取轴配置标题文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于右侧主标题显示当前轴名称和点位配置含义。
        /// </remarks>
        public string AxisConfigTitle => $"{AxisName}点位配置";

        /// <summary>
        /// 获取轴选择徽标文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于左侧选择区和概览卡片的短标识显示。
        /// </remarks>
        public string AxisShortName => AxisNo switch
        {
            1 => "X",
            2 => "Y",
            3 => "Z",
            4 => "摇",
            _ => AxisNo.ToString()
        };

        /// <summary>
        /// 获取运动控制线圈地址范围文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于地址预览卡片快速核对线圈地址是否连续。
        /// </remarks>
        public string CoilRangeText => $"M{JogPlusCoil}-M{ManualLocateTriggerCoil}";

        /// <summary>
        /// 获取速度参数寄存器地址范围文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于地址预览卡片显示速度相关 D 寄存器范围。
        /// </remarks>
        public string SpeedRegisterRangeText => $"D{ManualSpeedRegister}-D{AutoSpeedHighRegister}";

        /// <summary>
        /// 获取定位参数寄存器地址范围文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于地址预览卡片显示手动定位和加减速时间范围。
        /// </remarks>
        public string LocateRegisterRangeText => $"D{ManualTargetLowRegister}-D{DecelerationTimeRegister}";

        public string AxisName { get => _axisName; set { if (SetValue(ref _axisName, value)) OnPropertyChanged(nameof(AxisConfigTitle)); } }
        public ushort JogPlusCoil { get => _jogPlusCoil; set { if (SetValue(ref _jogPlusCoil, value)) NotifyCoilPreviewChanged(); } }
        public ushort JogMinusCoil { get => _jogMinusCoil; set => SetValue(ref _jogMinusCoil, value); }
        public ushort GoHomeCoil { get => _goHomeCoil; set => SetValue(ref _goHomeCoil, value); }
        public ushort HomeDoneCoil { get => _homeDoneCoil; set => SetValue(ref _homeDoneCoil, value); }
        public ushort PositiveLimitCoil { get => _positiveLimitCoil; set => SetValue(ref _positiveLimitCoil, value); }
        public ushort NegativeLimitCoil { get => _negativeLimitCoil; set => SetValue(ref _negativeLimitCoil, value); }
        public ushort HomeSensorCoil { get => _homeSensorCoil; set => SetValue(ref _homeSensorCoil, value); }
        public ushort ManualLocateTriggerCoil { get => _manualLocateTriggerCoil; set { if (SetValue(ref _manualLocateTriggerCoil, value)) NotifyCoilPreviewChanged(); } }
        public ushort CurrentPositionLowRegister { get => _currentPositionLowRegister; set => SetValue(ref _currentPositionLowRegister, value); }
        public ushort CurrentPositionHighRegister { get => _currentPositionHighRegister; set => SetValue(ref _currentPositionHighRegister, value); }
        public ushort ManualSpeedRegister { get => _manualSpeedRegister; set { if (SetValue(ref _manualSpeedRegister, value)) NotifySpeedPreviewChanged(); } }
        public ushort ManualSpeedHighRegister { get => _manualSpeedHighRegister; set => SetValue(ref _manualSpeedHighRegister, value); }
        public ushort AutoSpeedRegister { get => _autoSpeedRegister; set => SetValue(ref _autoSpeedRegister, value); }
        public ushort AutoSpeedHighRegister { get => _autoSpeedHighRegister; set { if (SetValue(ref _autoSpeedHighRegister, value)) NotifySpeedPreviewChanged(); } }
        public ushort ManualTargetLowRegister { get => _manualTargetLowRegister; set { if (SetValue(ref _manualTargetLowRegister, value)) NotifyLocatePreviewChanged(); } }
        public ushort ManualTargetHighRegister { get => _manualTargetHighRegister; set => SetValue(ref _manualTargetHighRegister, value); }
        public ushort AccelerationTimeRegister { get => _accelerationTimeRegister; set => SetValue(ref _accelerationTimeRegister, value); }
        public ushort DecelerationTimeRegister { get => _decelerationTimeRegister; set { if (SetValue(ref _decelerationTimeRegister, value)) NotifyLocatePreviewChanged(); } }

        /// <summary>
        /// 通知线圈地址预览文本已经变化。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 起始或结束线圈地址变化后刷新地址预览卡片。
        /// </remarks>
        private void NotifyCoilPreviewChanged()
        {
            OnPropertyChanged(nameof(CoilRangeText));
        }

        /// <summary>
        /// 通知速度寄存器预览文本已经变化。
        /// </summary> 
        /// By:ChengLei
        /// <remarks>
        /// 速度寄存器范围端点变化后刷新地址预览卡片。
        /// </remarks>
        private void NotifySpeedPreviewChanged()
        {
            OnPropertyChanged(nameof(SpeedRegisterRangeText));
        }

        /// <summary>
        /// 通知定位寄存器预览文本已经变化。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 定位寄存器范围端点变化后刷新地址预览卡片。
        /// </remarks>
        private void NotifyLocatePreviewChanged()
        {
            OnPropertyChanged(nameof(LocateRegisterRangeText));
        }

        /// <summary>
        /// 导出当前编辑行为轴地址配置对象。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前行对应的轴地址配置。</returns>
        /// <remarks>
        /// 由保存流程调用。
        /// </remarks>
        public AxisAddressProfile ToProfile()
        {
            return new AxisAddressProfile
            {
                AxisName = AxisName,
                JogPlusCoil = JogPlusCoil,
                JogMinusCoil = JogMinusCoil,
                GoHomeCoil = GoHomeCoil,
                HomeDoneCoil = HomeDoneCoil,
                PositiveLimitCoil = PositiveLimitCoil,
                NegativeLimitCoil = NegativeLimitCoil,
                HomeSensorCoil = HomeSensorCoil,
                ManualLocateTriggerCoil = ManualLocateTriggerCoil,
                CurrentPositionLowRegister = CurrentPositionLowRegister,
                CurrentPositionHighRegister = CurrentPositionHighRegister,
                ManualSpeedRegister = ManualSpeedRegister,
                ManualSpeedHighRegister = ManualSpeedHighRegister,
                AutoSpeedRegister = AutoSpeedRegister,
                AutoSpeedHighRegister = AutoSpeedHighRegister,
                ManualTargetLowRegister = ManualTargetLowRegister,
                ManualTargetHighRegister = ManualTargetHighRegister,
                AccelerationTimeRegister = AccelerationTimeRegister,
                DecelerationTimeRegister = DecelerationTimeRegister
            };
        }

        /// <summary>
        /// 更新字段值并通知界面。
        /// </summary>
        /// By:ChengLei
        /// <typeparam name="T">字段类型。</typeparam>
        /// <param name="field">待更新字段。</param>
        /// <param name="value">新值。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>返回是否发生变化。</returns>
        /// <remarks>
        /// 由属性 setter 统一调用。
        /// </remarks>
        private bool SetValue<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
