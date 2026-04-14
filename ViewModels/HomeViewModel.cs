using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Blood_Alcohol.Logs;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using Microsoft.Win32;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页业务视图模型，负责流程启动、初始化、日志展示与设备状态联动。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由 HomeView 创建为 DataContext，统一编排 WorkflowEngine、PLC信号与首页交互命令。
/// </remarks>
public class HomeViewModel : BaseViewModel, IDisposable
{
	private enum ProcessModeState
	{
		Standby,
		Pressure,
		Exhaust,
		Injection
	}

	private const int MaxTubeCount = 50;

	private const int MaxHeadspaceCount = 100;

	/// <summary>
	/// 采血管总数写入地址 D230
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由采血管数量同步流程调用 用于写入当前采血管总数
	/// </remarks>
	private const ushort PlcTubeCountRegisterAddress = 230;

	private const string ProcessParameterConfigFileName = "ProcessParameterConfig.json";

	private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";

	/// <summary>
	/// 初始化参数 Z轴丢枪头上升慢速速度地址 D6000
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入Z轴慢速参数
	/// </remarks>
	private const ushort PlcInitZDropNeedleRiseSlowSpeedRegisterAddress = 6000;

	/// <summary>
	/// 初始化参数 移液枪吸液延时地址 D6020
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入移液枪吸液延时
	/// </remarks>
	private const ushort PlcInitPipetteAspirateDelayRegisterAddress = 6020;

	/// <summary>
	/// 初始化参数 移液枪打液延时地址 D6021
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入移液枪打液延时
	/// </remarks>
	private const ushort PlcInitPipetteDispenseDelayRegisterAddress = 6021;

	/// <summary>
	/// 初始化参数 采血管摇匀原位延时地址 D6022
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入采血管摇匀原位延时
	/// </remarks>
	private const ushort PlcInitTubeShakeHomeDelayRegisterAddress = 6022;

	/// <summary>
	/// 初始化参数 采血管摇匀工位延时地址 D6023
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入采血管摇匀工位延时
	/// </remarks>
	private const ushort PlcInitTubeShakeWorkDelayRegisterAddress = 6023;

	/// <summary>
	/// 初始化参数 采血管摇匀目标次数地址 D6024
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入采血管摇匀目标次数
	/// </remarks>
	private const ushort PlcInitTubeShakeTargetCountRegisterAddress = 6024;

	/// <summary>
	/// 初始化参数 顶空瓶摇匀原位延时地址 D6026
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入顶空瓶摇匀原位延时
	/// </remarks>
	private const ushort PlcInitHeadspaceShakeHomeDelayRegisterAddress = 6026;

	/// <summary>
	/// 初始化参数 顶空瓶摇匀工位延时地址 D6027
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入顶空瓶摇匀工位延时
	/// </remarks>
	private const ushort PlcInitHeadspaceShakeWorkDelayRegisterAddress = 6027;

	/// <summary>
	/// 初始化参数 顶空瓶摇匀目标次数地址 D6028
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入顶空瓶摇匀目标次数
	/// </remarks>
	private const ushort PlcInitHeadspaceShakeTargetCountRegisterAddress = 6028;

	/// <summary>
	/// 初始化参数 叔丁醇吸液延时地址 D6030
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入叔丁醇吸液延时
	/// </remarks>
	private const ushort PlcInitButanolAspirateDelayRegisterAddress = 6030;

	/// <summary>
	/// 初始化参数 叔丁醇打液延时地址 D6031
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入叔丁醇打液延时
	/// </remarks>
	private const ushort PlcInitButanolDispenseDelayRegisterAddress = 6031;

	/// <summary>
	/// 初始化参数 样品瓶加压时间地址 D6040
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入样品瓶加压时间
	/// </remarks>
	private const ushort PlcInitSampleBottlePressureTimeRegisterAddress = 6040;

	/// <summary>
	/// 初始化参数 定量环平衡时间地址 D6041
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入定量环平衡时间
	/// </remarks>
	private const ushort PlcInitQuantitativeLoopBalanceTimeRegisterAddress = 6041;

	/// <summary>
	/// 初始化参数 进样时间地址 D6042
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入进样时间
	/// </remarks>
	private const ushort PlcInitInjectionTimeRegisterAddress = 6042;

	/// <summary>
	/// 初始化参数 样品瓶加压位置地址 D6302
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入样品瓶加压位置
	/// </remarks>
	private const ushort PlcInitSampleBottlePressurePositionRegisterAddress = 6302;

	/// <summary>
	/// 初始化参数 定量环平衡位置地址 D6304
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入定量环平衡位置
	/// </remarks>
	private const ushort PlcInitQuantitativeLoopBalancePositionRegisterAddress = 6304;

	/// <summary>
	/// 初始化参数 进样位置地址 D6306
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化参数下发流程调用 用于写入进样位置
	/// </remarks>
	private const ushort PlcInitInjectionPositionRegisterAddress = 6306;

	/// <summary>
	/// 初始化按钮地址 M13
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化命令发送流程调用 用于触发初始化动作
	/// </remarks>
	private const ushort PlcInitCommandCoilAddress = 13;

	/// <summary>
	/// 初始化完成状态位地址 M14
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化完成轮询流程调用 用于判断初始化是否结束
	/// </remarks>
	private const ushort PlcInitDoneCoilAddress = 14;

	/// <summary>
	/// 自动挡标记位地址 M10
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由模式切换流程读写 M10等于1表示自动挡
	/// </remarks>
	private const ushort PlcAutoModeCoilAddress = 10;

	/// <summary>
	/// 开始命令位地址 M5
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始检测流程调用 以脉冲方式下发开始命令
	/// </remarks>
	private const ushort PlcStartCommandCoilAddress = 5;

	/// <summary>
	/// 停止命令位地址 M900
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由停止检测流程调用 以脉冲方式下发停止命令
	/// </remarks>
	private const ushort PlcStopCommandCoilAddress = 900;

	/// <summary>
	/// 急停命令位地址 M3
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由急停流程调用 以脉冲方式下发急停命令
	/// </remarks>
	private const ushort PlcEmergencyStopCoilAddress = 3;

	/// <summary>
	/// 报警汇总位地址 M2
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由报警监听流程调用 用于检测当前是否存在报警
	/// </remarks>
	private const ushort PlcAlarmSummaryCoilAddress = 2;

	private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(10);

	private static readonly TimeSpan InitPollInterval = TimeSpan.FromMilliseconds(100);

	private static readonly TimeSpan AlarmPollInterval = TimeSpan.FromMilliseconds(200);

	private static readonly TimeSpan ProcessModePollInterval = TimeSpan.FromMilliseconds(300);

	private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(900);

	/// <summary>
	/// 待机模式标记位地址 M490
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由工艺模式监听流程调用 当前为占位地址
	/// </remarks>
	private const ushort PlcStandbyModeCoilAddress = 490;

	/// <summary>
	/// 压力模式标记位地址 M491
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由工艺模式监听流程调用 当前为占位地址
	/// </remarks>
	private const ushort PlcPressureModeCoilAddress = 491;

	/// <summary>
	/// 排气模式标记位地址 M492
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由工艺模式监听流程调用 当前为占位地址
	/// </remarks>
	private const ushort PlcExhaustModeCoilAddress = 492;

	/// <summary>
	/// 进样模式标记位地址 M493
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由工艺模式监听流程调用 当前为占位地址
	/// </remarks>
	private const ushort PlcInjectionModeCoilAddress = 493;

	/// <summary>
	/// 料架工序当前生产号起始地址 D233
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由料架工序监控流程调用 用于读取D233到D254寄存器区间
	/// </remarks>
	private const ushort PlcRackProcessStartRegisterAddress = 233;

	/// <summary>
	/// 料架工序当前生产号读取长度 22
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由料架工序监控流程调用 覆盖D233到D254共22个寄存器
	/// </remarks>
	private const ushort PlcRackProcessRegisterCount = 22;

	private static readonly int[] TubeRunningRegisterOffsets = new int[5] { 0, 1, 3, 4, 11 };

	private static readonly int[] TubeCompletedRegisterOffsets = new int[1] { 12 };

	private static readonly int[] HeadspaceRunningRegisterOffsets = new int[13]
	{
		2, 6, 7, 8, 9, 10, 13, 14, 15, 16,
		17, 20, 21
	};

	private static readonly int[] HeadspaceCompletedRegisterOffsets = new int[1] { 18 };

	private static readonly Brush ActiveSlotFill = BrushFromHex("#005ECC");

	private static readonly Brush ActiveSlotText = Brushes.White;

	private static readonly Brush IdleSlotFill = Brushes.White;

	private static readonly Brush IdleSlotText = BrushFromHex("#0F172A");

	private static readonly Brush RunningSlotFill = BrushFromHex("#7C3AED");

	private static readonly Brush CompletedSlotFill = BrushFromHex("#16A34A");

	private static readonly Brush NeedleUsedFill = BrushFromHex("#D1D5DB");

	private static readonly Brush NeedleIdleFill = Brushes.WhiteSmoke;

	private static readonly TimeSpan RackProcessPollInterval = TimeSpan.FromMilliseconds(300);

	private const string ExportPathConfigFileName = "HomeExportPathConfig.json";

	private readonly ConfigService<HomeExportPathConfig> _exportPathConfigService = new ConfigService<HomeExportPathConfig>("HomeExportPathConfig.json");

	private readonly ConfigService<ProcessParameterConfig> _processParameterConfigService = new ConfigService<ProcessParameterConfig>(ProcessParameterConfigFileName);

	private readonly ConfigService<WeightToZCalibrationConfig> _weightToZConfigService = new ConfigService<WeightToZCalibrationConfig>(WeightToZConfigFileName);

	private LogTool _logTool = new LogTool();

	private readonly string _logSessionId = $"RUN_{DateTime.Now:yyyyMMdd_HHmmss}";

	private readonly List<HomeLogItemViewModel> _allLogs = new List<HomeLogItemViewModel>();

	private readonly WorkflowEngine _workflowEngine = new WorkflowEngine();

	private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;

	private CancellationTokenSource? _tubeCountSyncCts;

	private Task? _tubeCountSyncTask;

	private CancellationTokenSource? _alarmMonitorCts;

	private Task? _alarmMonitorTask;

	private CancellationTokenSource? _processModeMonitorCts;

	private Task? _processModeMonitorTask;

	private CancellationTokenSource? _rackProcessMonitorCts;

	private Task? _rackProcessMonitorTask;

	private readonly HashSet<int> _tubeRunningSlots = new HashSet<int>();

	private readonly HashSet<int> _tubeCompletedSlots = new HashSet<int>();

	private readonly HashSet<int> _headspaceRunningSlots = new HashSet<int>();

	private readonly HashSet<int> _headspaceCompletedSlots = new HashSet<int>();

	private bool _isInitializing;

	private bool _isStartCommandProcessing;

	private volatile bool _isAlarmActive;

	private bool _isDetectionStarted;

	private int _infoCount;

	private int _warningCount;

	private int _errorCount;

	private string _sampleName = "血液";

	private string _sampleVolume = "0";

    private string _butanolName = "叔丁醇";

    private string _butanolVolume = "500";

    private string _scanCode = string.Empty;

	private string _headspaceASampleWeight = "0.0";

	private string _headspaceAButanolWeight = "0.0";

	private string _headspaceBSampleWeight = "0.0";

	private string _headspaceBButanolWeight = "0.0";

	private string _tubeCount = "0";

	private string _headspaceCount = "0";

	private string _countRuleText = "未开始检测：可自由选择采血管数量。";

	private string _lastExportPath = string.Empty;

	private string _exportDirectory = string.Empty;

	private int _selectedTubeCount;

	private int _selectedHeadspaceCount;

	private bool _showSystemLogs = true;

	private bool _showProcessLogs = true;

	private bool _showDebugLogs = true;

	private bool _showHardwareLogs = true;

	private bool _showOperationLogs = true;

	private bool _showDetectionLogs = true;

	private bool _showInfoLogs = true;

	private bool _showWarningLogs = true;

	private bool _showErrorLogs = true;

	private bool _sampleVolumeCoefficientWarningLogged;

	private OperationMode _operationMode = OperationModeService.CurrentMode;

	private ProcessModeState _currentProcessMode = ProcessModeState.Standby;

	private bool _disposed;

	public int InfoCount
	{
		get
		{
			return _infoCount;
		}
		private set
		{
			if (_infoCount != value)
			{
				_infoCount = value;
				OnPropertyChanged("InfoCount");
			}
		}
	}

	public int WarningCount
	{
		get
		{
			return _warningCount;
		}
		private set
		{
			if (_warningCount != value)
			{
				_warningCount = value;
				OnPropertyChanged("WarningCount");
			}
		}
	}

	public int ErrorCount
	{
		get
		{
			return _errorCount;
		}
		private set
		{
			if (_errorCount != value)
			{
				_errorCount = value;
				OnPropertyChanged("ErrorCount");
			}
		}
	}

	public string SampleName
	{
		get
		{
			return _sampleName;
		}
		set
		{
			if (_sampleName != value)
			{
				_sampleName = value;
				OnPropertyChanged("SampleName");
			}
		}
	}

	public string SampleVolume
	{
		get
		{
			return _sampleVolume;
		}
		set
		{
			if (_sampleVolume != value)
			{
				_sampleVolume = value;
				OnPropertyChanged("SampleVolume");
			}
		}
	}

	public string ButanolName
	{
		get
		{
			return _butanolName;
		}
		set
		{
			if (_butanolName != value)
			{
				_butanolName = value;
				OnPropertyChanged("ButanolName");
			}
		}
	}

	public string ButanolVolume
	{
		get
		{
			return _butanolVolume;
		}
		set
		{
			if (_butanolVolume != value)
			{
				_butanolVolume = value;
				OnPropertyChanged("ButanolVolume");
			}
		}
	}

	public string ScanCode
	{
		get
		{
			return _scanCode;
		}
		set
		{
			if (_scanCode != value)
			{
				_scanCode = value;
				OnPropertyChanged("ScanCode");
			}
		}
	}

	public string HeadspaceASampleWeight
	{
		get
		{
			return _headspaceASampleWeight;
		}
		set
		{
			if (_headspaceASampleWeight != value)
			{
				_headspaceASampleWeight = value;
				OnPropertyChanged("HeadspaceASampleWeight");
			}
		}
	}

	public string HeadspaceAButanolWeight
	{
		get
		{
			return _headspaceAButanolWeight;
		}
		set
		{
			if (_headspaceAButanolWeight != value)
			{
				_headspaceAButanolWeight = value;
				OnPropertyChanged("HeadspaceAButanolWeight");
			}
		}
	}

	public string HeadspaceBSampleWeight
	{
		get
		{
			return _headspaceBSampleWeight;
		}
		set
		{
			if (_headspaceBSampleWeight != value)
			{
				_headspaceBSampleWeight = value;
				OnPropertyChanged("HeadspaceBSampleWeight");
			}
		}
	}

	public string HeadspaceBButanolWeight
	{
		get
		{
			return _headspaceBButanolWeight;
		}
		set
		{
			if (_headspaceBButanolWeight != value)
			{
				_headspaceBButanolWeight = value;
				OnPropertyChanged("HeadspaceBButanolWeight");
			}
		}
	}

	public string TubeCount
	{
		get
		{
			return _tubeCount;
		}
		private set
		{
			if (_tubeCount != value)
			{
				_tubeCount = value;
				OnPropertyChanged("TubeCount");
			}
		}
	}

	public string HeadspaceCount
	{
		get
		{
			return _headspaceCount;
		}
		private set
		{
			if (_headspaceCount != value)
			{
				_headspaceCount = value;
				OnPropertyChanged("HeadspaceCount");
			}
		}
	}

	public string CountRuleText
	{
		get
		{
			return _countRuleText;
		}
		private set
		{
			if (_countRuleText != value)
			{
				_countRuleText = value;
				OnPropertyChanged("CountRuleText");
			}
		}
	}

	public string LastExportPath
	{
		get
		{
			return _lastExportPath;
		}
		private set
		{
			if (_lastExportPath != value)
			{
				_lastExportPath = value;
				OnPropertyChanged("LastExportPath");
			}
		}
	}

	public string ExportDirectory
	{
		get
		{
			return _exportDirectory;
		}
		private set
		{
			if (_exportDirectory != value)
			{
				_exportDirectory = value;
				OnPropertyChanged("ExportDirectory");
			}
		}
	}

	public bool IsTubeSelectionEnabled => !_isDetectionStarted;

	public bool ShowSystemLogs
	{
		get
		{
			return _showSystemLogs;
		}
		set
		{
			if (_showSystemLogs != value)
			{
				_showSystemLogs = value;
				OnPropertyChanged("ShowSystemLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowProcessLogs
	{
		get
		{
			return _showProcessLogs;
		}
		set
		{
			if (_showProcessLogs != value)
			{
				_showProcessLogs = value;
				OnPropertyChanged("ShowProcessLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowDebugLogs
	{
		get
		{
			return _showDebugLogs;
		}
		set
		{
			if (_showDebugLogs != value)
			{
				_showDebugLogs = value;
				OnPropertyChanged("ShowDebugLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowHardwareLogs
	{
		get
		{
			return _showHardwareLogs;
		}
		set
		{
			if (_showHardwareLogs != value)
			{
				_showHardwareLogs = value;
				OnPropertyChanged("ShowHardwareLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowOperationLogs
	{
		get
		{
			return _showOperationLogs;
		}
		set
		{
			if (_showOperationLogs != value)
			{
				_showOperationLogs = value;
				OnPropertyChanged("ShowOperationLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowDetectionLogs
	{
		get
		{
			return _showDetectionLogs;
		}
		set
		{
			if (_showDetectionLogs != value)
			{
				_showDetectionLogs = value;
				OnPropertyChanged("ShowDetectionLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowInfoLogs
	{
		get
		{
			return _showInfoLogs;
		}
		set
		{
			if (_showInfoLogs != value)
			{
				_showInfoLogs = value;
				OnPropertyChanged("ShowInfoLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowWarningLogs
	{
		get
		{
			return _showWarningLogs;
		}
		set
		{
			if (_showWarningLogs != value)
			{
				_showWarningLogs = value;
				OnPropertyChanged("ShowWarningLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool ShowErrorLogs
	{
		get
		{
			return _showErrorLogs;
		}
		set
		{
			if (_showErrorLogs != value)
			{
				_showErrorLogs = value;
				OnPropertyChanged("ShowErrorLogs");
				RefreshVisibleLogs();
			}
		}
	}

	public bool IsAutoMode => _operationMode == OperationMode.Auto;

	public bool IsManualMode => _operationMode == OperationMode.Manual;

	public string ModeDisplayText => IsAutoMode ? "当前档位：自动" : "当前档位：手动";

	public bool IsStandbyProcessMode => _currentProcessMode == ProcessModeState.Standby;

	public bool IsPressureProcessMode => _currentProcessMode == ProcessModeState.Pressure;

	public bool IsExhaustProcessMode => _currentProcessMode == ProcessModeState.Exhaust;

	public bool IsInjectionProcessMode => _currentProcessMode == ProcessModeState.Injection;

	public string ProcessModeDisplayText => _currentProcessMode switch
	{
		ProcessModeState.Pressure => "当前模式：压力模式",
		ProcessModeState.Exhaust => "当前模式：排气模式",
		ProcessModeState.Injection => "当前模式：注入模式",
		_ => "当前模式：待机模式"
	};

	public ObservableCollection<RackSlotItemViewModel> TubeRackSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<RackSlotItemViewModel> HeadspaceRackSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<RackSlotItemViewModel> NeedleHeadSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<ConditionItemViewModel> Conditions { get; } = new ObservableCollection<ConditionItemViewModel>();

	public ObservableCollection<HomeLogItemViewModel> VisibleLogs { get; } = new ObservableCollection<HomeLogItemViewModel>();

	public ICommand InitCommand { get; }

	public ICommand StartCommand { get; }

	public ICommand StopCommand { get; }

	public ICommand SaveCommand { get; }

	public ICommand LightCommand { get; }

	public ICommand DemoCommand { get; }

	public ICommand TubeRackClickCommand { get; }

	public ICommand SelectExportDirectoryCommand { get; }

	public ICommand ExportLogsCommand { get; }

	public ICommand SwitchToAutoModeCommand { get; }

	public ICommand SwitchToManualModeCommand { get; }

	/// <summary>
	/// 初始化首页视图模型并装配首页命令与数据源。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 HomeView 初始化时调用，构造完成后会启动必要监控并绑定事件。
	/// </remarks>
	public HomeViewModel()
	{
		InitCommand = new RelayCommand(delegate
		{
			InitializeSystem();
		}, (object? _) => !_isInitializing);
		StartCommand = new RelayCommand(delegate
		{
			StartDetection();
		}, (object? _) => !_isDetectionStarted && !_isStartCommandProcessing);
		StopCommand = new RelayCommand(delegate
		{
			StopDetection();
		}, (object? _) => _isDetectionStarted);
		SaveCommand = new RelayCommand(delegate
		{
			RunAction("参数保存", HomeLogSource.Process, HomeLogKind.Operation);
		});
		LightCommand = new RelayCommand(delegate
		{
			RunAction("照明切换", HomeLogSource.Hardware, HomeLogKind.Operation);
		});
		DemoCommand = new RelayCommand(delegate
		{
			EmergencyStop();
		});
		TubeRackClickCommand = new RelayCommand(delegate(object? p)
		{
			OnTubeSlotClick(p as RackSlotItemViewModel);
		}, (object? p) => p is RackSlotItemViewModel);
		SelectExportDirectoryCommand = new RelayCommand(delegate
		{
			SelectExportDirectory();
		});
		ExportLogsCommand = new RelayCommand(delegate
		{
			ExportLogs();
		});
		SwitchToAutoModeCommand = new RelayCommand(delegate(object? _)
		{
			_ = SwitchOperationModeAsync(OperationMode.Auto);
		}, (object? _) => IsManualMode);
		SwitchToManualModeCommand = new RelayCommand(delegate(object? _)
		{
			_ = SwitchOperationModeAsync(OperationMode.Manual);
		}, (object? _) => IsAutoMode);
		InitializeExportDirectory();
		OperationModeService.ModeChanged += OnOperationModeChanged;
		ApplyOperationMode(OperationModeService.CurrentMode, writeLog: false);
		BuildTubeRackSlots();
		BuildHeadspaceRackSlots();
		BuildNeedleHeadSlots();
		BuildConditions();
		BuildDefaultLogs();
		ApplyCount(0, writeLog: false);
		RefreshVisibleLogs();
		RegisterCorePlcPollingPoints();
		CommunicationManager.OnLogReceived += OnCommunicationLogReceived;
		_workflowEngine.OnLogGenerated += OnWorkflowLogGenerated;
		StartAlarmMonitor();
		StartProcessModeMonitor();
		StartRackProcessMonitor();
	}

	/// <summary>
	/// 注册首页核心PLC轮询点位。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程调用，用于给首页常用点位提供缓存轮询数据。
	/// </remarks>
	private void RegisterCorePlcPollingPoints()
	{
		CommunicationManager.PlcPolling.RegisterCoil(PlcAlarmSummaryCoilAddress, AlarmPollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcStandbyModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcPressureModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcExhaustModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcInjectionModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.Start();
	}

	/// <summary>
	/// 注销首页注册的PLC轮询点位。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 Dispose 调用，防止页面关闭后仍占用轮询资源。
	/// </remarks>
	private void UnregisterCorePlcPollingPoints()
	{
		CommunicationManager.PlcPolling.UnregisterCoil(PlcAlarmSummaryCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcStandbyModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcPressureModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcExhaustModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcInjectionModeCoilAddress);
	}

	/// <summary>
	/// 切换系统运行模式并同步服务状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <returns>返回模式切换异步任务。</returns>
	/// <remarks>
	/// 由模式切换命令触发，切换后会通知 OperationModeService。
	/// </remarks>
	private async Task SwitchOperationModeAsync(OperationMode mode)
	{
		if (_operationMode == mode)
		{
			return;
		}
		bool autoModeBit = mode == OperationMode.Auto;
		try
		{
			if (CommunicationManager.Is485Open)
			{
				await WriteCoilWithLockAsync(PlcAutoModeCoilAddress, autoModeBit);
			}
			else
			{
				AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC未连接，仅切换软件档位显示。");
			}
			OperationModeService.CurrentMode = mode;
			AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, (mode == OperationMode.Auto) ? "档位切换为自动（M10=1）。" : "档位切换为手动（M10=0）。");
		}
		catch (Exception ex)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "档位切换失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 响应运行模式变化事件并刷新首页显示。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <remarks>
	/// 由 OperationModeService.ModeChanged 回调触发。
	/// </remarks>
	private void OnOperationModeChanged(OperationMode mode)
	{
		RunOnUiThread(delegate
		{
			ApplyOperationMode(mode, writeLog: false);
		});
	}

	/// <summary>
	/// 应用运行模式到页面状态与提示信息。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <param name="writeLog">是否记录模式切换日志。</param>
	/// <remarks>
	/// 由构造函数初始化与 OnOperationModeChanged 调用。
	/// </remarks>
	private void ApplyOperationMode(OperationMode mode, bool writeLog)
	{
		if (_operationMode != mode)
		{
			_operationMode = mode;
			OnPropertyChanged("IsAutoMode");
			OnPropertyChanged("IsManualMode");
			OnPropertyChanged("ModeDisplayText");
			CommandManager.InvalidateRequerySuggested();
			if (writeLog)
			{
				AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, (mode == OperationMode.Auto) ? "档位切换为自动（M10=1）。" : "档位切换为手动（M10=0）。");
			}
		}
	}

	/// <summary>
	/// 初始化日志导出目录配置。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造函数调用，加载上次导出路径或回退默认目录。
	/// </remarks>
	private void InitializeExportDirectory()
	{
		HomeExportPathConfig homeExportPathConfig = _exportPathConfigService.Load() ?? new HomeExportPathConfig();
		string defaultProjectLogsDirectory = GetDefaultProjectLogsDirectory();
		string directoryPath = (string.IsNullOrWhiteSpace(homeExportPathConfig.ExportDirectory) ? defaultProjectLogsDirectory : homeExportPathConfig.ExportDirectory);
		ApplyExportDirectory(directoryPath, saveToConfig: true, writeLog: false);
		LastExportPath = ExportDirectory;
	}

	/// <summary>
	/// 弹出目录选择并更新导出路径。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由导出目录选择按钮调用。
	/// </remarks>
	private void SelectExportDirectory()
	{
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "选择日志导出目录",
				InitialDirectory = (Directory.Exists(ExportDirectory) ? ExportDirectory : GetDefaultProjectLogsDirectory()),
				FolderName = ExportDirectory,
				Multiselect = false
			};
			if (openFolderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
			{
				ApplyExportDirectory(openFolderDialog.FolderName, saveToConfig: true, writeLog: true);
				LastExportPath = ExportDirectory;
			}
		}
		catch (Exception ex)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "选择导出目录失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 应用并可选保存日志导出目录。
	/// </summary>
	/// By:ChengLei
	/// <param name="directoryPath">目标导出目录路径。</param>
	/// <param name="saveToConfig">是否将目录持久化到配置文件。</param>
	/// <param name="writeLog">是否记录模式切换日志。</param>
	/// <remarks>
	/// 由 InitializeExportDirectory 与 SelectExportDirectory 调用。
	/// </remarks>
	private void ApplyExportDirectory(string directoryPath, bool saveToConfig, bool writeLog)
	{
		string text = NormalizePathOrEmpty(directoryPath);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = GetDefaultProjectLogsDirectory();
		}
		Directory.CreateDirectory(text);
		ExportDirectory = text;
		_logTool = new LogTool(text);
		if (saveToConfig)
		{
			_exportPathConfigService.Save(new HomeExportPathConfig
			{
				ExportDirectory = text
			});
		}
		if (writeLog)
		{
			AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出目录已设置：" + text);
		}
	}

	/// <summary>
	/// 规范化路径文本并转换为可比较格式。
	/// </summary>
	/// By:ChengLei
	/// <param name="path">待规范化的路径文本。</param>
	/// <returns>返回规范化后的路径；无效时返回空字符串。</returns>
	/// <remarks>
	/// 由导出目录处理流程调用，统一比较与持久化格式。
	/// </remarks>
	private static string NormalizePathOrEmpty(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		try
		{
			return Path.GetFullPath(path.Trim());
		}
		catch
		{
			return string.Empty;
		}
	}

	/// <summary>
	/// 获取项目默认日志目录路径。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回默认日志目录绝对路径。</returns>
	/// <remarks>
	/// 由导出目录初始化流程调用。
	/// </remarks>
	private static string GetDefaultProjectLogsDirectory()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		for (DirectoryInfo? directoryInfo = new DirectoryInfo(baseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (directoryInfo.EnumerateFiles("*.csproj").Any())
			{
				return Path.Combine(directoryInfo.FullName, "Logs");
			}
		}
		return Path.Combine(baseDirectory, "Logs");
	}

	/// <summary>
	/// 触发初始化流程并进行防重入控制。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页初始化按钮调用。
	/// </remarks>
	private void InitializeSystem()
	{
		if (_isInitializing)
		{
			AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "初始化中...请等待");
			return;
		}
		_isInitializing = true;
		CommandManager.InvalidateRequerySuggested();
		_ = InitializeSystemAsync();
	}

	/// <summary>
	/// 执行初始化流程：下发参数、发送初始化命令并等待完成信号。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化执行异步任务。</returns>
	/// <remarks>
	/// 由 InitializeSystem 触发，包含完整初始化业务链路。
	/// </remarks>
	private async Task InitializeSystemAsync()
	{
		try
		{
			AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "初始化中...请等待");
			if (!(await SendInitSignalToPlcAsync()))
			{
				AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化失败：初始化命令发送失败。");
			}
			else if (await WaitForInitDoneAsync())
			{
				AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "初始化成功");
			}
			else
			{
				AddLog(HomeLogLevel.Error, HomeLogSource.System, HomeLogKind.Operation, "初始化失败：超时（10分钟）");
			}
		}
		finally
		{
			_isInitializing = false;
			CommandManager.InvalidateRequerySuggested();
		}
	}

	/// <summary>
	/// 轮询初始化完成位直到成功或超时。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化是否在超时前完成。</returns>
	/// <remarks>
	/// 由 InitializeSystemAsync 调用，通过轮询M14判断初始化完成。
	/// </remarks>
	private async Task<bool> WaitForInitDoneAsync()
	{
		DateTime deadline = DateTime.UtcNow.Add(InitTimeout);
		bool readErrorLogged = false;
		bool lastState;
		bool seenLow;
		try
		{
			lastState = await ReadInitDoneFlagAsync();
			seenLow = !lastState;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化状态读取失败：" + ex2.Message);
			lastState = false;
			seenLow = true;
			readErrorLogged = true;
		}
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				bool currentState = await ReadInitDoneFlagAsync();
				if (currentState)
				{
					return true;
				}
				if (!seenLow)
				{
					if (!currentState)
					{
						seenLow = true;
					}
				}
				else if (!lastState && currentState)
				{
					return true;
				}
				lastState = currentState;
				readErrorLogged = false;
			}
			catch (Exception ex3)
			{
				if (!readErrorLogged)
				{
					AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化状态读取失败：" + ex3.Message);
					readErrorLogged = true;
				}
			}
			await Task.Delay(InitPollInterval);
		}
		return false;
	}

	/// <summary>
	/// 读取初始化完成线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化完成位当前状态。</returns>
	/// <remarks>
	/// 由 WaitForInitDoneAsync 调用。
	/// </remarks>
	private async Task<bool> ReadInitDoneFlagAsync()
	{
		await _plcLock.WaitAsync();
		try
		{
			var read = await CommunicationManager.Plc.TryReadCoilsAsync(PlcInitDoneCoilAddress, 1);
			if (!read.Success)
			{
				throw new InvalidOperationException(read.Error);
			}

			bool[] state = read.Values;
			return state.Length != 0 && state[0];
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 触发检测启动流程并处理执行入口。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页开始按钮调用。
	/// </remarks>
	private void StartDetection()
	{
		_ = StartDetectionAsync();
	}

	/// <summary>
	/// 刷新开始/停止相关命令可用状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由检测状态变化节点调用，刷新按钮可用性。
	/// </remarks>
	private void RefreshDetectionCommandStates()
	{
		OnPropertyChanged("IsTubeSelectionEnabled");
		CommandManager.InvalidateRequerySuggested();
	}

	/// <summary>
	/// 执行检测启动前置校验并下发开始脉冲。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回检测启动异步任务。</returns>
	/// <remarks>
	/// 由 StartDetection 调用，完成开始前校验与信号下发。
	/// </remarks>
	private async Task StartDetectionAsync()
	{
		if (_isDetectionStarted || _isStartCommandProcessing)
		{
			return;
		}
		_isStartCommandProcessing = true;
		CommandManager.InvalidateRequerySuggested();
		try
		{
			if (_selectedTubeCount <= 0)
			{
				await EnsureStartCommandLowAsync();
				AddLog(HomeLogLevel.Warning, HomeLogSource.Process, HomeLogKind.Operation, "请先点击采血管架选择检测总数。");
			}
			else if (!CommunicationManager.Is485Open)
			{
				await EnsureStartCommandLowAsync();
				AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC未连接，已发送 M5=0，禁止开始检测。");
			}
			else if (await TrySendStartPulseByPreconditionsAsync())
			{
				_isDetectionStarted = true;
				RefreshDetectionCommandStates();
				_workflowEngine.Start();
				StartTubeCountSync();
				CountRuleText = "检测已开始：只能增加采血管数量，不能减少。";
				AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Detection, $"开始检测：采血管{_selectedTubeCount}，顶空瓶{_selectedHeadspaceCount}。");
			}
		}
		finally
		{
			_isStartCommandProcessing = false;
			CommandManager.InvalidateRequerySuggested();
		}
	}

	/// <summary>
	/// 执行检测停止流程并下发停止信号。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页停止按钮或自动停机流程调用。
	/// </remarks>
	private void StopDetection()
	{
		if (!_isDetectionStarted)
		{
			_ = SendStopSignalToPlcAsync();
			return;
		}
		_isDetectionStarted = false;
		RefreshDetectionCommandStates();
		StopTubeCountSync();
		ClearRackProcessStates();
		_workflowEngine.Stop();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "检测已停止：可重新选择采血管数量。";
		AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Detection, "检测已停止。");
	}

	/// <summary>
	/// 执行急停流程并记录关键日志。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由急停按钮调用，优先保障设备安全停机。
	/// </remarks>
	private void EmergencyStop()
	{
		_isDetectionStarted = false;
		RefreshDetectionCommandStates();
		StopTubeCountSync();
		ClearRackProcessStates();
		_workflowEngine.Stop();
		_ = SendEmergencyStopSignalToPlcAsync();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "急停已触发：请排查后复位。";
		AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "急停触发，已停止当前动作。");
	}

	/// <summary>
	/// 统一封装首页操作动作的执行与日志。
	/// </summary>
	/// By:ChengLei
	/// <param name="action">需要执行的业务委托。</param>
	/// <param name="source">日志来源分类。</param>
	/// <param name="kind">日志业务类别。</param>
	/// <remarks>
	/// 由多个按钮命令复用，统一日志和异常处理策略。
	/// </remarks>
	private void RunAction(string action, HomeLogSource source, HomeLogKind kind)
	{
		AddLog(HomeLogLevel.Info, source, kind, action);
	}

	/// <summary>
	/// 处理采血管槽位点击并更新选择状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="slot">被点击的料架槽位对象。</param>
	/// <remarks>
	/// 由料架槽位点击命令调用。
	/// </remarks>
	private void OnTubeSlotClick(RackSlotItemViewModel? slot)
	{
		if (slot != null)
		{
			int number = slot.Number;
			if (_isDetectionStarted)
			{
				AddLog(HomeLogLevel.Warning, HomeLogSource.Process, HomeLogKind.Detection, $"检测中禁止减少数量：当前{_selectedTubeCount}，请求{number}。", number);
			}
			else
			{
				ApplyCount(number, writeLog: false);
			}
		}
	}

	/// <summary>
	/// 启动报警监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由检测启动后调用。
	/// </remarks>
	private void StartAlarmMonitor()
	{
		StopAlarmMonitor();
		_alarmMonitorCts = new CancellationTokenSource();
		_alarmMonitorTask = Task.Run(() => AlarmMonitorLoopAsync(_alarmMonitorCts.Token));
	}

	/// <summary>
	/// 停止报警监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由停止/释放流程调用。
	/// </remarks>
	private void StopAlarmMonitor()
	{
		_alarmMonitorCts?.Cancel();
		_alarmMonitorCts?.Dispose();
		_alarmMonitorCts = null;
		_alarmMonitorTask = null;
	}

	/// <summary>
	/// 启动工艺模式监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造或检测流程调用。
	/// </remarks>
	private void StartProcessModeMonitor()
	{
		StopProcessModeMonitor();
		_processModeMonitorCts = new CancellationTokenSource();
		_processModeMonitorTask = Task.Run(() => ProcessModeMonitorLoopAsync(_processModeMonitorCts.Token));
	}

	/// <summary>
	/// 停止工艺模式监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由停止/释放流程调用。
	/// </remarks>
	private void StopProcessModeMonitor()
	{
		_processModeMonitorCts?.Cancel();
		_processModeMonitorCts?.Dispose();
		_processModeMonitorCts = null;
		_processModeMonitorTask = null;
	}

	/// <summary>
	/// 启动料架工序状态监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造函数调用，持续读取 D233~D254 映射槽位颜色状态。
	/// </remarks>
	private void StartRackProcessMonitor()
	{
		StopRackProcessMonitor();
		_rackProcessMonitorCts = new CancellationTokenSource();
		_rackProcessMonitorTask = Task.Run(() => RackProcessMonitorLoopAsync(_rackProcessMonitorCts.Token));
	}

	/// <summary>
	/// 停止料架工序状态监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 Dispose 调用，避免页面关闭后仍持续读取PLC寄存器。
	/// </remarks>
	private void StopRackProcessMonitor()
	{
		_rackProcessMonitorCts?.Cancel();
		_rackProcessMonitorCts?.Dispose();
		_rackProcessMonitorCts = null;
		_rackProcessMonitorTask = null;
	}

	/// <summary>
	/// 循环读取工序寄存器并刷新料架槽位状态集合。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回工序监控异步任务。</returns>
	/// <remarks>
	/// 由 StartRackProcessMonitor 启动，读取 D233~D254 对应当前生产号。
	/// </remarks>
	private async Task RackProcessMonitorLoopAsync(CancellationToken token)
	{
		bool readFaultLogged = false;
		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!CommunicationManager.Is485Open)
				{
					RunOnUiThread(ClearRackProcessStates);
					readFaultLogged = false;
					await Task.Delay(RackProcessPollInterval, token);
					continue;
				}
				if (!_isDetectionStarted)
				{
					RunOnUiThread(ClearRackProcessStates);
					readFaultLogged = false;
					await Task.Delay(RackProcessPollInterval, token);
					continue;
				}

				var read = await ReadHoldingRegistersWithLockAsync(PlcRackProcessStartRegisterAddress, PlcRackProcessRegisterCount, token);
				if (!read.Success)
				{
					if (!readFaultLogged)
					{
						AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取失败：" + read.Error);
						readFaultLogged = true;
					}

					await Task.Delay(RackProcessPollInterval, token);
					continue;
				}

				RunOnUiThread(delegate
				{
					ApplyRackProcessRegisters(read.Values);
				});

				if (readFaultLogged)
				{
					AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取已恢复。");
					readFaultLogged = false;
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				if (!readFaultLogged)
				{
					AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取失败：" + ex.Message);
					readFaultLogged = true;
				}
			}

			await Task.Delay(RackProcessPollInterval, token);
		}
	}

	/// <summary>
	/// 在PLC锁保护下读取连续保持寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="startAddress">起始D寄存器地址。</param>
	/// <param name="length">读取长度。</param>
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回读取结果和错误信息。</returns>
	/// <remarks>
	/// 由 RackProcessMonitorLoopAsync 调用，统一复用寄存器读取逻辑。
	/// </remarks>
	private async Task<(bool Success, ushort[] Values, string Error)> ReadHoldingRegistersWithLockAsync(ushort startAddress, ushort length, CancellationToken token)
	{
		await _plcLock.WaitAsync(token);
		try
		{
			return await CommunicationManager.Plc.TryReadHoldingRegistersAsync(startAddress, length);
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 根据工序寄存器值更新料架运行与完成集合。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">D233~D254读取结果。</param>
	/// <remarks>
	/// 由 RackProcessMonitorLoopAsync 在UI线程调用，更新后触发颜色刷新。
	/// </remarks>
	private void ApplyRackProcessRegisters(IReadOnlyList<ushort> registers)
	{
		HashSet<int> tubeRunning = ExtractSlotsFromRegisters(registers, TubeRunningRegisterOffsets, MaxTubeCount);
		HashSet<int> tubeCompleted = ExtractSlotsFromRegisters(registers, TubeCompletedRegisterOffsets, MaxTubeCount);
		HashSet<int> headspaceRunning = ExtractSlotsFromRegisters(registers, HeadspaceRunningRegisterOffsets, MaxHeadspaceCount);
		HashSet<int> headspaceCompleted = ExtractSlotsFromRegisters(registers, HeadspaceCompletedRegisterOffsets, MaxHeadspaceCount);

		bool changed = ReplaceSlotSet(_tubeRunningSlots, tubeRunning);
		changed |= ReplaceSlotSet(_tubeCompletedSlots, tubeCompleted);
		changed |= ReplaceSlotSet(_headspaceRunningSlots, headspaceRunning);
		changed |= ReplaceSlotSet(_headspaceCompletedSlots, headspaceCompleted);
		if (changed)
		{
			UpdateRackVisuals();
		}
	}

	/// <summary>
	/// 从寄存器集合按偏移提取有效槽位编号。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">读取到的寄存器集合。</param>
	/// <param name="offsets">需要提取的偏移集合。</param>
	/// <param name="maxSlotNumber">槽位最大编号限制。</param>
	/// <returns>返回提取到的槽位编号集合。</returns>
	/// <remarks>
	/// 由 ApplyRackProcessRegisters 分别提取采血管和顶空瓶工序槽位时调用。
	/// </remarks>
	private static HashSet<int> ExtractSlotsFromRegisters(IReadOnlyList<ushort> registers, IEnumerable<int> offsets, int maxSlotNumber)
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (int offset in offsets)
		{
			if (offset >= 0 && offset < registers.Count)
			{
				int num = registers[offset];
				if (num > 0 && num <= maxSlotNumber)
				{
					hashSet.Add(num);
				}
			}
		}
		return hashSet;
	}

	/// <summary>
	/// 用新集合替换槽位状态集合并返回是否发生变化。
	/// </summary>
	/// By:ChengLei
	/// <param name="target">当前状态集合。</param>
	/// <param name="source">新状态集合。</param>
	/// <returns>返回集合内容是否发生变化。</returns>
	/// <remarks>
	/// 由 ApplyRackProcessRegisters 调用，用于减少无效界面刷新。
	/// </remarks>
	private static bool ReplaceSlotSet(HashSet<int> target, HashSet<int> source)
	{
		if (target.SetEquals(source))
		{
			return false;
		}

		target.Clear();
		foreach (int item in source)
		{
			target.Add(item);
		}
		return true;
	}

	/// <summary>
	/// 清空料架工序状态集合并刷新默认颜色。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 RackProcessMonitorLoopAsync 在PLC离线时调用，避免显示旧工序状态。
	/// </remarks>
	private void ClearRackProcessStates()
	{
		if (_tubeRunningSlots.Count == 0 && _tubeCompletedSlots.Count == 0 && _headspaceRunningSlots.Count == 0 && _headspaceCompletedSlots.Count == 0)
		{
			return;
		}

		_tubeRunningSlots.Clear();
		_tubeCompletedSlots.Clear();
		_headspaceRunningSlots.Clear();
		_headspaceCompletedSlots.Clear();
		UpdateRackVisuals();
	}

	/// <summary>
	/// 循环轮询工艺模式点位并刷新工艺状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回工艺模式监控异步任务。</returns>
	/// <remarks>
	/// 由 StartProcessModeMonitor 启动的后台任务循环调用。
	/// </remarks>
	private async Task ProcessModeMonitorLoopAsync(CancellationToken token)
	{
		bool readFaultLogged = false;
		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!CommunicationManager.Is485Open)
				{
					readFaultLogged = false;
					await Task.Delay(ProcessModePollInterval, token);
					continue;
				}

				(bool Success, bool Value, string Error) standbyRead = await TryReadCoilStateWithLockAsync(PlcStandbyModeCoilAddress, token);
				(bool Success, bool Value, string Error) pressureRead = standbyRead.Success
					? await TryReadCoilStateWithLockAsync(PlcPressureModeCoilAddress, token)
					: (Success: false, Value: false, Error: standbyRead.Error);
				(bool Success, bool Value, string Error) exhaustRead = (standbyRead.Success && pressureRead.Success)
					? await TryReadCoilStateWithLockAsync(PlcExhaustModeCoilAddress, token)
					: (Success: false, Value: false, Error: standbyRead.Success ? pressureRead.Error : standbyRead.Error);
				(bool Success, bool Value, string Error) injectionRead = (standbyRead.Success && pressureRead.Success && exhaustRead.Success)
					? await TryReadCoilStateWithLockAsync(PlcInjectionModeCoilAddress, token)
					: (Success: false, Value: false, Error: !standbyRead.Success ? standbyRead.Error : !pressureRead.Success ? pressureRead.Error : exhaustRead.Error);

				if (!(standbyRead.Success && pressureRead.Success && exhaustRead.Success && injectionRead.Success))
				{
					if (!readFaultLogged)
					{
						string readError = !standbyRead.Success
							? standbyRead.Error
							: !pressureRead.Success
								? pressureRead.Error
								: !exhaustRead.Success
									? exhaustRead.Error
									: injectionRead.Error;
						AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听失败：" + readError);
						readFaultLogged = true;
					}

					await Task.Delay(ProcessModePollInterval, token);
					continue;
				}

				bool standby = standbyRead.Item2;
				bool pressure = pressureRead.Item2;
				bool exhaust = exhaustRead.Item2;
				bool injection = injectionRead.Item2;

				ProcessModeState mode = ResolveProcessModeState(standby, pressure, exhaust, injection);
				if (mode != _currentProcessMode)
				{
					RunOnUiThread(() => SetCurrentProcessMode(mode));
				}
				if (readFaultLogged)
				{
					AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听已恢复。");
					readFaultLogged = false;
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				if (!readFaultLogged)
				{
					AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听失败：" + ex.Message);
					readFaultLogged = true;
				}
			}

			await Task.Delay(ProcessModePollInterval, token);
		}
	}

	/// <summary>
	/// 根据模式线圈组合解析当前工艺模式。
	/// </summary>
	/// By:ChengLei
	/// <param name="standby">待机模式信号。</param>
	/// <param name="pressure">增压模式信号。</param>
	/// <param name="exhaust">排气模式信号。</param>
	/// <param name="injection">进样模式信号。</param>
	/// <returns>返回解析后的工艺模式状态。</returns>
	/// <remarks>
	/// 由 ProcessModeMonitorLoopAsync 解析工艺状态时调用。
	/// </remarks>
	private static ProcessModeState ResolveProcessModeState(bool standby, bool pressure, bool exhaust, bool injection)
	{
		// Priority when multiple bits are high at the same time.
		if (injection)
		{
			return ProcessModeState.Injection;
		}

		if (exhaust)
		{
			return ProcessModeState.Exhaust;
		}

		if (pressure)
		{
			return ProcessModeState.Pressure;
		}

		if (standby)
		{
			return ProcessModeState.Standby;
		}

		// Fallback when no mode bit is high.
		return ProcessModeState.Standby;
	}

	/// <summary>
	/// 设置当前工艺模式并刷新显示文本。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <remarks>
	/// 由工艺模式监控流程调用。
	/// </remarks>
	private void SetCurrentProcessMode(ProcessModeState mode)
	{
		if (_currentProcessMode == mode)
		{
			return;
		}

		_currentProcessMode = mode;
		OnPropertyChanged("IsStandbyProcessMode");
		OnPropertyChanged("IsPressureProcessMode");
		OnPropertyChanged("IsExhaustProcessMode");
		OnPropertyChanged("IsInjectionProcessMode");
		OnPropertyChanged("ProcessModeDisplayText");
		AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Operation, "流程模式切换为：" + GetProcessModeText(mode) + "。");
	}

	/// <summary>
	/// 将工艺模式枚举转换为页面显示文本。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <returns>返回工艺模式显示文本。</returns>
	/// <remarks>
	/// 由 SetCurrentProcessMode 与界面展示逻辑调用。
	/// </remarks>
	private static string GetProcessModeText(ProcessModeState mode)
	{
		return mode switch
		{
			ProcessModeState.Standby => "待机",
			ProcessModeState.Pressure => "加压",
			ProcessModeState.Exhaust => "排气",
			ProcessModeState.Injection => "进样",
			_ => "未知"
		};
	}

	/// <summary>
	/// 循环检测报警状态并驱动报警联动逻辑。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回报警监控异步任务。</returns>
	/// <remarks>
	/// 由 StartAlarmMonitor 启动，持续监控报警汇总位。
	/// </remarks>
	private async Task AlarmMonitorLoopAsync(CancellationToken token)
	{
		bool hasLastState = false;
		bool lastState = false;
		bool readErrorLogged = false;
		bool hasConnectionState = false;
		bool lastConnectionState = false;
		bool commFaultActive = false;
		while (!token.IsCancellationRequested)
		{
			try
			{
				bool isConnected = CommunicationManager.Is485Open;
				if (!hasConnectionState)
				{
					hasConnectionState = true;
					lastConnectionState = isConnected;
				}
				else if (lastConnectionState != isConnected)
				{
					lastConnectionState = isConnected;
					if (!isConnected)
					{
						AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC连接已断开（RS485离线）。");
					}
					else
					{
						AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC连接已恢复（RS485在线）。");
					}
				}
				if (!isConnected)
				{
					_isAlarmActive = false;
					hasLastState = false;
					readErrorLogged = false;
					await Task.Delay(AlarmPollInterval, token);
					continue;
				}
				var alarmRead = await TryReadAlarmSummaryAsync(token);
				if (!alarmRead.Success)
				{
					if (!commFaultActive)
					{
						RunOnUiThread(delegate
						{
							AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯中断：" + alarmRead.Error);
						});
						commFaultActive = true;
					}

					if (!readErrorLogged)
					{
						RunOnUiThread(delegate
						{
							AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总监听失败：" + alarmRead.Error);
						});
						readErrorLogged = true;
					}

					await Task.Delay(AlarmPollInterval, token);
					continue;
				}

				bool currentState = alarmRead.Value;
				if (commFaultActive)
				{
					AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯已恢复。");
					commFaultActive = false;
				}
				_isAlarmActive = currentState;
				if (!hasLastState)
				{
					hasLastState = true;
					lastState = currentState;
					if (currentState)
					{
						RunOnUiThread(delegate
						{
							AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总触发(M2=1)。");
							CountRuleText = "报警中：请先排查并清除报警，再开始检测。";
							if (_isDetectionStarted)
							{
								AutoStopDetectionByAlarm();
							}
						});
					}
				}
				else if (!lastState && currentState)
				{
					RunOnUiThread(delegate
					{
						AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总触发(M2=1)。");
						CountRuleText = "报警中：请先排查并清除报警，再开始检测。";
						if (_isDetectionStarted)
						{
							AutoStopDetectionByAlarm();
						}
					});
				}
				else if (lastState && !currentState)
				{
					RunOnUiThread(delegate
					{
						AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总解除(M2=0)。");
						if (!_isDetectionStarted)
						{
							CountRuleText = "报警已解除：可重新开始检测。";
						}
					});
				}
				lastState = currentState;
				readErrorLogged = false;
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				Exception ex4 = ex3;
				if (!commFaultActive)
				{
					RunOnUiThread(delegate
					{
						AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯中断：" + ex4.Message);
					});
					commFaultActive = true;
				}
				if (!readErrorLogged)
				{
					RunOnUiThread(delegate
					{
						AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总监听失败：" + ex4.Message);
					});
					readErrorLogged = true;
				}
			}
			await Task.Delay(AlarmPollInterval, token);
		}
	}

	private Task<(bool Success, bool Value, string Error)> TryReadAlarmSummaryAsync(CancellationToken token)
	{
		return TryReadCoilStateWithLockAsync(PlcAlarmSummaryCoilAddress, token);
	}

	/// <summary>
	/// 在报警触发时自动停止检测流程。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 AlarmMonitorLoopAsync 在报警触发后调用。
	/// </remarks>
	private void AutoStopDetectionByAlarm()
	{
		_isDetectionStarted = false;
		RefreshDetectionCommandStates();
		StopTubeCountSync();
		ClearRackProcessStates();
		_workflowEngine.Stop();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "报警触发：检测已自动停止，请排查后复位。";
		AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Detection, "检测过程中报警汇总(M2=1)，已自动停止检测。");
	}

	/// <summary>
	/// 满足前置条件后尝试发送开始脉冲。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回是否成功下发开始脉冲。</returns>
	/// <remarks>
	/// 由 StartDetectionAsync 调用。
	/// </remarks>
	private async Task<bool> TrySendStartPulseByPreconditionsAsync()
	{
		try
		{
			bool m2Alarm = await ReadCoilStateWithLockAsync(PlcAlarmSummaryCoilAddress);
			bool m0AutoMode = await ReadCoilStateWithLockAsync(PlcAutoModeCoilAddress);
			bool m14InitDone = await ReadCoilStateWithLockAsync(PlcInitDoneCoilAddress);
			_isAlarmActive = m2Alarm;
			if (m2Alarm || !m0AutoMode || !m14InitDone)
			{
				await EnsureStartCommandLowAsync();
				AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"开始前置条件不满足：M2={(m2Alarm ? 1 : 0)}，M10={(m0AutoMode ? 1 : 0)}，M14={(m14InitDone ? 1 : 0)}，已发送 M5=0。");
				if (m2Alarm)
				{
					CountRuleText = "报警中：请先排查并清除报警，再开始检测。";
				}
				return false;
			}
			await WriteCoilWithLockAsync(PlcStartCommandCoilAddress, value: true);
			await Task.Delay(1000);
			await WriteCoilWithLockAsync(PlcStartCommandCoilAddress, value: false);
			return true;
		}
		catch (Exception ex)
		{
			await EnsureStartCommandLowAsync();
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "开始信号发送失败，已发送 M5=0：" + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// 确保开始命令位复位为低电平。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回命令位复位异步任务。</returns>
	/// <remarks>
	/// 由开始脉冲发送前后调用，避免命令位残留高电平。
	/// </remarks>
	private async Task EnsureStartCommandLowAsync()
	{
		try
		{
			await WriteCoilWithLockAsync(PlcStartCommandCoilAddress, value: false);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "写入 M5=0 失败：" + ex2.Message);
		}
	}

	private async Task<(bool Success, bool Value, string Error)> TryReadCoilStateWithLockAsync(ushort address, CancellationToken token = default(CancellationToken))
	{
		if (CommunicationManager.PlcPolling.TryGetCoil(address, CoilCacheMaxAge, out PlcPollingService.CoilSnapshot cached))
		{
			if (cached.Success)
			{
				return (true, cached.Value, string.Empty);
			}

			// Cache has fresh negative state, return directly and avoid extra serial pressure.
			return (false, false, string.IsNullOrWhiteSpace(cached.Error) ? "PLC polling failed." : cached.Error);
		}

		await _plcLock.WaitAsync(token);
		try
		{
			var read = await CommunicationManager.Plc.TryReadCoilsAsync(address, 1);
			if (!read.Success)
			{
				return (false, false, read.Error);
			}

			bool state = read.Values.Length != 0 && read.Values[0];
			return (true, state, string.Empty);
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 在PLC锁保护下读取线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">PLC点位地址。</param>
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回线圈状态值。</returns>
	/// <remarks>
	/// 由首页多个PLC状态读取流程复用。
	/// </remarks>
	private async Task<bool> ReadCoilStateWithLockAsync(ushort address, CancellationToken token = default(CancellationToken))
	{
		var read = await TryReadCoilStateWithLockAsync(address, token);
		if (!read.Success)
		{
			throw new InvalidOperationException(read.Error);
		}

		return read.Value;
	}

	/// <summary>
	/// 在PLC锁保护下写入线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">PLC点位地址。</param>
	/// <param name="value">待写入的线圈值。</param>
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回线圈写入异步任务。</returns>
	/// <remarks>
	/// 由开始/停止/急停/初始化信号下发流程复用。
	/// </remarks>
	private async Task WriteCoilWithLockAsync(ushort address, bool value, CancellationToken token = default(CancellationToken))
	{
		await _plcLock.WaitAsync(token);
		try
		{
			var write = await CommunicationManager.Plc.TryWriteSingleCoilAsync(address, value);
			if (!write.Success)
			{
				throw new InvalidOperationException(write.Error);
			}
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 在UI线程执行指定委托。
	/// </summary>
	/// By:ChengLei
	/// <param name="action">需要执行的业务委托。</param>
	/// <remarks>
	/// 由后台线程回调更新界面属性时调用。
	/// </remarks>
	private static void RunOnUiThread(Action action)
	{
		Application current = Application.Current;
		Dispatcher? dispatcher = current?.Dispatcher;
		if (dispatcher == null || dispatcher.CheckAccess())
		{
			action();
		}
		else
		{
			dispatcher.BeginInvoke((Delegate)action, Array.Empty<object>());
		}
	}

	/// <summary>
	/// 判断当前线程是否为UI线程。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前是否为UI线程。</returns>
	/// <remarks>
	/// 由 RunOnUiThread 进行线程判断时调用。
	/// </remarks>
	private static bool IsOnUiThread()
	{
		Application current = Application.Current;
		Dispatcher? dispatcher = current?.Dispatcher;
		return dispatcher == null || dispatcher.CheckAccess();
	}

	/// <summary>
	/// 启动采血管数量同步任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由检测流程启动时调用。
	/// </remarks>
	private void StartTubeCountSync()
	{
		StopTubeCountSync();
		_tubeCountSyncCts = new CancellationTokenSource();
		_tubeCountSyncTask = Task.Run(() => SyncTubeCountLoopAsync(_tubeCountSyncCts.Token));
	}

	/// <summary>
	/// 停止采血管数量同步任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由停止和释放流程调用。
	/// </remarks>
	private void StopTubeCountSync()
	{
		_tubeCountSyncCts?.Cancel();
		_tubeCountSyncCts?.Dispose();
		_tubeCountSyncCts = null;
		_tubeCountSyncTask = null;
	}

	/// <summary>
	/// 循环将首页数量设置同步到PLC。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回数量同步异步任务。</returns>
	/// <remarks>
	/// 由 StartTubeCountSync 启动并循环调用 SendTubeCountToPlcAsync。
	/// </remarks>
	private async Task SyncTubeCountLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested && _isDetectionStarted)
		{
			try
			{
				await SendTubeCountToPlcAsync(token);
				await Task.Delay(1000, token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex2)
			{
				AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Detection, "采血管数量上报给PLC失败：" + ex2.Message);
				await Task.Delay(1000, token);
			}
		}
	}

	/// <summary>
	/// 向PLC下发当前采血管数量寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回数量下发异步任务。</returns>
	/// <remarks>
	/// 由 SyncTubeCountLoopAsync 周期调用。
	/// </remarks>
	private async Task SendTubeCountToPlcAsync(CancellationToken token)
	{
		ushort tubeCount = (ushort)Math.Clamp(_selectedTubeCount, 0, 65535);
		await _plcLock.WaitAsync(token);
		try
		{
			var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(PlcTubeCountRegisterAddress, tubeCount);
			if (!write.Success)
			{
				throw new InvalidOperationException(write.Error);
			}
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 发送初始化启动信号并校验写入结果。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化命令是否发送成功。</returns>
	/// <remarks>
	/// 由 InitializeSystemAsync 调用。
	/// </remarks>
	private async Task<bool> SendInitSignalToPlcAsync()
	{
		try
		{
			await SendInitParametersToPlcWithVerifyAsync();
			await WriteCoilWithLockAsync(PlcInitCommandCoilAddress, value: true);
			return true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化命令发送失败：" + ex2.Message);
			return false;
		}
	}

	/// <summary>
	/// 下发初始化参数并回读校验写入一致性。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回参数下发与校验异步任务。</returns>
	/// <remarks>
	/// 由 InitializeSystemAsync 在发送初始化信号前调用。
	/// </remarks>
	private async Task SendInitParametersToPlcWithVerifyAsync()
	{
		ProcessParameterConfig config = _processParameterConfigService.Load() ?? new ProcessParameterConfig();
		(ushort Address, int Value, string Name)[] items = new (ushort, int, string)[17]
		{
			(PlcInitZDropNeedleRiseSlowSpeedRegisterAddress, config.ZDropNeedleRiseSlowSpeed, "Z轴_丢枪头_上升慢速速度"),
			(PlcInitPipetteAspirateDelayRegisterAddress, config.PipetteAspirateDelay100ms, "移液枪吸液延时时间"),
			(PlcInitPipetteDispenseDelayRegisterAddress, config.PipetteDispenseDelay100ms, "移液枪打液延时时间"),
			(PlcInitTubeShakeHomeDelayRegisterAddress, config.TubeShakeHomeDelay100ms, "采血管摇晃原位延时时间"),
			(PlcInitTubeShakeWorkDelayRegisterAddress, config.TubeShakeWorkDelay100ms, "采血管摇晃工位延时时间"),
			(PlcInitTubeShakeTargetCountRegisterAddress, config.TubeShakeTargetCount, "采血管摇晃目标次数"),
			(PlcInitHeadspaceShakeHomeDelayRegisterAddress, config.HeadspaceShakeHomeDelay100ms, "顶空瓶摇晃原位延时时间"),
			(PlcInitHeadspaceShakeWorkDelayRegisterAddress, config.HeadspaceShakeWorkDelay100ms, "顶空瓶摇晃工位延时时间"),
			(PlcInitHeadspaceShakeTargetCountRegisterAddress, config.HeadspaceShakeTargetCount, "顶空瓶摇晃目标次数"),
			(PlcInitButanolAspirateDelayRegisterAddress, config.ButanolAspirateDelay100ms, "叔丁醇吸液延时时间"),
			(PlcInitButanolDispenseDelayRegisterAddress, config.ButanolDispenseDelay100ms, "叔丁醇打液延时时间"),
			(PlcInitSampleBottlePressureTimeRegisterAddress, config.SampleBottlePressureTime100ms, "样品瓶加压时间"),
			(PlcInitQuantitativeLoopBalanceTimeRegisterAddress, config.QuantitativeLoopBalanceTime100ms, "定量环平衡时间"),
			(PlcInitInjectionTimeRegisterAddress, config.InjectionTime100ms, "进样时间"),
			(PlcInitSampleBottlePressurePositionRegisterAddress, config.SampleBottlePressurePosition, "样品瓶加压位置"),
			(PlcInitQuantitativeLoopBalancePositionRegisterAddress, config.QuantitativeLoopBalancePosition, "定量环平衡位置"),
			(PlcInitInjectionPositionRegisterAddress, config.InjectionPosition, "进样位置")
		};
		await _plcLock.WaitAsync();
		try
		{
			foreach ((ushort Address, int Value, string Name) item in items)
			{
				ushort expected = (ushort)Math.Clamp(item.Value, 0, 65535);
				var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(item.Address, expected);
				if (!write.Success)
				{
					throw new InvalidOperationException($"D{item.Address} {item.Name} 写入失败：{write.Error}");
				}
				var read = await CommunicationManager.Plc.TryReadHoldingRegistersAsync(item.Address, 1);
				if (!read.Success)
				{
					throw new InvalidOperationException($"D{item.Address} {item.Name} 回读失败：{read.Error}");
				}
				if (read.Values.Length == 0)
				{
					throw new InvalidOperationException($"D{item.Address} {item.Name} 回读失败：返回长度为0");
				}
				ushort actual = read.Values[0];
				if (actual != expected)
				{
					throw new InvalidOperationException($"D{item.Address} {item.Name} 校验失败：期望={expected}，实际={actual}");
				}
			}
		}
		finally
		{
			_plcLock.Release();
		}
		AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化参数写入并校验成功（D6000、D6020、D6021、D6022、D6023、D6024、D6026、D6027、D6028、D6030、D6031、D6040、D6041、D6042、D6302、D6304、D6306）。");
	}

	/// <summary>
	/// 发送停止信号到PLC。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回停止信号发送异步任务。</returns>
	/// <remarks>
	/// 由 StopDetection 调用。
	/// </remarks>
	private async Task SendStopSignalToPlcAsync()
	{
		try
		{
			await WriteCoilWithLockAsync(PlcStopCommandCoilAddress, value: true);
			await Task.Delay(100);
			await WriteCoilWithLockAsync(PlcStopCommandCoilAddress, value: false);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "向PLC发送停止信号失败：" + ex2.Message);
		}
	}

	/// <summary>
	/// 发送急停信号到PLC。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回急停信号发送异步任务。</returns>
	/// <remarks>
	/// 由 EmergencyStop 调用。
	/// </remarks>
	private async Task SendEmergencyStopSignalToPlcAsync()
	{
		try
		{
			await WriteCoilWithLockAsync(PlcEmergencyStopCoilAddress, value: true);
			await Task.Delay(100);
			await WriteCoilWithLockAsync(PlcEmergencyStopCoilAddress, value: false);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "急停信号发送失败：" + ex2.Message);
		}
	}

	/// <summary>
	/// 应用采血管数量并联动顶空瓶数量规则。
	/// </summary>
	/// By:ChengLei
	/// <param name="requestedTubeCount">用户请求的采血管数量。</param>
	/// <param name="writeLog">是否记录模式切换日志。</param>
	/// <remarks>
	/// 由数量选择动作调用，更新业务计数及页面文案。
	/// </remarks>
	private void ApplyCount(int requestedTubeCount, bool writeLog)
	{
		int num = Math.Clamp(requestedTubeCount, 0, 50);
		int num2 = Math.Min(100, num * 2);
		_selectedTubeCount = num;
		_selectedHeadspaceCount = num2;
		TubeCount = num.ToString();
		HeadspaceCount = num2.ToString();
		UpdateRackVisuals();
		if (writeLog)
		{
			AddLog(HomeLogLevel.Info, HomeLogSource.Process, _isDetectionStarted ? HomeLogKind.Detection : HomeLogKind.Operation, $"采血管数量设为{num}，自动映射顶空瓶数量{num2}。", num);
		}
	}

	/// <summary>
	/// 导出当前首页可见日志到指定目录。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由导出日志按钮调用。
	/// </remarks>
	private void ExportLogs()
	{
		List<LogCsvRecord> records = VisibleLogs.Select((HomeLogItemViewModel x) => new LogCsvRecord
		{
			Timestamp = x.Timestamp,
			TubeIndex = x.TubeIndex,
			Message = x.Message,
			LevelText = x.LevelText,
			SourceText = x.SourceText,
			KindText = x.KindText
		}).ToList();
		IReadOnlyList<string> readOnlyList = _logTool.ExportCsvByTube(records, _logSessionId, _selectedTubeCount, DateTime.Now);
		if (readOnlyList.Count == 0)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "没有可导出的日志。");
			return;
		}
		LastExportPath = ExportDirectory;
		string text = ((readOnlyList.Count == 1) ? readOnlyList[0] : $"共导出 {readOnlyList.Count} 个文件，示例：{readOnlyList[0]}");
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出完成：" + text);
	}

	/// <summary>
	/// 按旧版格式导出日志文件。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由兼容导出流程调用。
	/// </remarks>
	private void ExportLogsLegacy()
	{
		List<LogCsvRecord> records = VisibleLogs.Select((HomeLogItemViewModel x) => new LogCsvRecord
		{
			Timestamp = x.Timestamp,
			TubeIndex = x.TubeIndex,
			Message = x.Message,
			LevelText = x.LevelText,
			SourceText = x.SourceText,
			KindText = x.KindText
		}).ToList();
		IReadOnlyList<string> readOnlyList = _logTool.ExportCsvByTube(records, _logSessionId, _selectedTubeCount, DateTime.Now);
		if (readOnlyList.Count == 0)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "没有可导出的日志。");
			return;
		}
		LastExportPath = ((readOnlyList.Count == 1) ? readOnlyList[0] : $"共导出 {readOnlyList.Count} 个文件，示例：{readOnlyList[0]}");
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出完成：" + LastExportPath);
	}

	/// <summary>
	/// 追加首页日志并执行筛选、计数和落盘。
	/// </summary>
	/// By:ChengLei
	/// <param name="level">日志级别。</param>
	/// <param name="source">日志来源分类。</param>
	/// <param name="kind">日志业务类别。</param>
	/// <param name="message">日志消息文本。</param>
	/// <param name="tubeIndex">关联采血管序号（为空表示系统级日志）。</param>
	/// <param name="persistToFile">是否将日志写入本地文件。</param>
	/// <remarks>
	/// 由首页各业务动作和外部日志事件统一调用。
	/// </remarks>
	private void AddLog(HomeLogLevel level, HomeLogSource source, HomeLogKind kind, string message, int? tubeIndex = null, bool persistToFile = true)
	{
		if (!IsOnUiThread())
		{
			RunOnUiThread(delegate
			{
				AddLog(level, source, kind, message, tubeIndex, persistToFile);
			});
			return;
		}
		DateTime now = DateTime.Now;
		int num = tubeIndex.GetValueOrDefault();
		if (num < 0)
		{
			num = 0;
		}
		HomeLogItemViewModel homeLogItemViewModel = new HomeLogItemViewModel
		{
			Timestamp = now,
			Time = now.ToString("yyyy-MM-dd HH:mm:ss"),
			Message = message,
			Level = level,
			Source = source,
			Kind = kind,
			TubeIndex = num
		};
		_allLogs.Insert(0, homeLogItemViewModel);
		if (_allLogs.Count > 2000)
		{
			_allLogs.RemoveAt(_allLogs.Count - 1);
		}
		if (persistToFile)
		{
			_logTool.WriteLog(homeLogItemViewModel.SourceText, homeLogItemViewModel.KindText, homeLogItemViewModel.LevelText, "采血管:" + homeLogItemViewModel.TubeText + " " + homeLogItemViewModel.Message, _logSessionId, _selectedTubeCount, homeLogItemViewModel.TubeIndex, homeLogItemViewModel.Timestamp);
		}
		RecalculateCounters();
		RefreshVisibleLogs();
	}

	/// <summary>
	/// 接收通信日志并映射到首页日志流。
	/// </summary>
	/// By:ChengLei
	/// <param name="log">外部日志消息对象。</param>
	/// <remarks>
	/// 由 CommunicationManager 日志事件触发。
	/// </remarks>
	private void OnCommunicationLogReceived(CommunicationManager.LogMessage log)
	{
		HomeLogLevel level = log.Level switch
		{
			CommunicationManager.LogLevel.Error => HomeLogLevel.Error,
			CommunicationManager.LogLevel.Warning => HomeLogLevel.Warning,
			_ => HomeLogLevel.Info
		};
		string sourceText = string.IsNullOrWhiteSpace(log.Source) ? "通信" : log.Source;
		AddLog(level, HomeLogSource.Hardware, HomeLogKind.Operation, $"[{sourceText}] {log.Message}");
	}

	/// <summary>
	/// 接收流程日志并映射到首页日志流。
	/// </summary>
	/// By:ChengLei
	/// <param name="log">外部日志消息对象。</param>
	/// <remarks>
	/// 由 WorkflowEngine.OnLogGenerated 事件触发。
	/// </remarks>
	private void OnWorkflowLogGenerated(WorkflowEngine.WorkflowLogMessage log)
	{
		string? scanCode = string.IsNullOrWhiteSpace(log.ScanCode) ? ExtractScanCodeFromWorkflowMessage(log.Message) : log.ScanCode;
		if (!string.IsNullOrWhiteSpace(scanCode))
		{
			RunOnUiThread(delegate
			{
				ScanCode = scanCode;
			});
		}
		TryUpdateSampleVolumeFromWorkflowWeight(log);
		HomeLogLevel level = ParseHomeLogLevel(log.LevelText);
		HomeLogKind kind = ParseHomeLogKind(log.LogKind);
		int? tubeIndex = (log.TubeIndex > 0) ? log.TubeIndex : null;
		AddLog(level, HomeLogSource.Process, kind, "流程：" + log.Message, tubeIndex);
	}

	/// <summary>
	/// 根据流程称重事件刷新采血管体积显示。
	/// </summary>
	/// By:ChengLei
	/// <param name="log">流程日志事件对象。</param>
	/// <remarks>
	/// 由 OnWorkflowLogGenerated 调用，仅处理采血管放置和采血管吸液后的称重事件。
	/// </remarks>
	private void TryUpdateSampleVolumeFromWorkflowWeight(WorkflowEngine.WorkflowLogMessage log)
	{
		if (!log.MeasuredWeight.HasValue || !IsTubeWeightStep(log.WeightStepKey))
		{
			return;
		}

		if (!TryGetMicroliterPerWeight(out double microliterPerWeight))
		{
			if (!_sampleVolumeCoefficientWarningLogged)
			{
				_sampleVolumeCoefficientWarningLogged = true;
				AddLog(HomeLogLevel.Warning, HomeLogSource.Process, HomeLogKind.Operation, "未找到有效重量转微升系数，无法刷新采血管体积显示。请先在重量到Z标定页面完成微升系数标定并保存。");
			}
			return;
		}

		_sampleVolumeCoefficientWarningLogged = false;
		double microliter = Math.Max(0.0, log.MeasuredWeight.Value * microliterPerWeight);
		RunOnUiThread(delegate
		{
			SampleVolume = microliter.ToString("F1");
		});
	}

	/// <summary>
	/// 判断流程称重步骤是否属于采血管体积更新来源。
	/// </summary>
	/// By:ChengLei
	/// <param name="weightStepKey">流程称重步骤标识。</param>
	/// <returns>返回是否为采血管称重步骤。</returns>
	/// <remarks>
	/// 由 TryUpdateSampleVolumeFromWorkflowWeight 调用。
	/// </remarks>
	private static bool IsTubeWeightStep(string? weightStepKey)
	{
		return string.Equals(weightStepKey, "tube_place_weight", StringComparison.Ordinal)
			|| string.Equals(weightStepKey, "tube_after_aspirate_weight", StringComparison.Ordinal);
	}

	/// <summary>
	/// 读取重量到微升系数配置并校验有效性。
	/// </summary>
	/// By:ChengLei
	/// <param name="microliterPerWeight">输出重量到微升系数（ul/g）。</param>
	/// <returns>返回系数是否有效。</returns>
	/// <remarks>
	/// 由 TryUpdateSampleVolumeFromWorkflowWeight 调用，优先读取实时保存的标定配置。
	/// </remarks>
	private bool TryGetMicroliterPerWeight(out double microliterPerWeight)
	{
		microliterPerWeight = 0.0;
		try
		{
			WeightToZCalibrationConfig config = _weightToZConfigService.Load() ?? new WeightToZCalibrationConfig();
			if (!config.HasMicroliterCoefficient || Math.Abs(config.MicroliterPerWeight) <= 1E-07)
			{
				return false;
			}

			microliterPerWeight = config.MicroliterPerWeight;
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 从流程日志文本提取条码内容。
	/// </summary>
	/// By:ChengLei
	/// <param name="message">日志消息文本。</param>
	/// <returns>返回提取到的条码文本，未命中时返回空。</returns>
	/// <remarks>
	/// 由 OnWorkflowLogGenerated 在扫码日志场景调用。
	/// </remarks>
	private static string? ExtractScanCodeFromWorkflowMessage(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return null;
		}
		const string prefix = "扫码成功：";
		int num = message.IndexOf(prefix, StringComparison.Ordinal);
		if (num < 0)
		{
			return null;
		}
		int num2 = num + prefix.Length;
		int num3 = message.IndexOf('，', num2);
		string text = ((num3 > num2) ? message.Substring(num2, num3 - num2) : message.Substring(num2)).Trim();
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}

	/// <summary>
	/// 将日志级别文本解析为首页级别枚举。
	/// </summary>
	/// By:ChengLei
	/// <param name="levelText">待解析的日志级别文本。</param>
	/// <returns>返回解析后的首页日志级别。</returns>
	/// <remarks>
	/// 由 OnCommunicationLogReceived 与 OnWorkflowLogGenerated 调用。
	/// </remarks>
	private static HomeLogLevel ParseHomeLogLevel(string levelText)
	{
		if (!string.IsNullOrWhiteSpace(levelText))
		{
			if (levelText.Contains("错误") || levelText.Contains("閿欒"))
			{
				return HomeLogLevel.Error;
			}
			if (levelText.Contains("警告") || levelText.Contains("璀﹀憡"))
			{
				return HomeLogLevel.Warning;
			}
		}
		return HomeLogLevel.Info;
	}

	/// <summary>
	/// 将日志类别文本解析为首页类别枚举。
	/// </summary>
	/// By:ChengLei
	/// <param name="kindText">待解析的日志类别文本。</param>
	/// <returns>返回解析后的首页日志类别。</returns>
	/// <remarks>
	/// 由日志映射流程调用。
	/// </remarks>
	private static HomeLogKind ParseHomeLogKind(string kindText)
	{
		if (!string.IsNullOrWhiteSpace(kindText) && (kindText.Contains("检测") || kindText.Contains("妫€娴")))
		{
			return HomeLogKind.Detection;
		}
		return HomeLogKind.Operation;
	}

	/// <summary>
	/// 重新统计首页日志级别计数。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 AddLog 与筛选刷新流程调用。
	/// </remarks>
	private void RecalculateCounters()
	{
		InfoCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Info);
		WarningCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Warning);
		ErrorCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Error);
	}

	/// <summary>
	/// 按筛选条件刷新首页可见日志集合。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由日志筛选条件变化时调用。
	/// </remarks>
	private void RefreshVisibleLogs()
	{
		List<HomeLogItemViewModel> list = _allLogs.Where(IsVisible).ToList();
		VisibleLogs.Clear();
		foreach (HomeLogItemViewModel item in list)
		{
			VisibleLogs.Add(item);
		}
	}

	/// <summary>
	/// 判断日志是否满足当前筛选条件。
	/// </summary>
	/// By:ChengLei
	/// <param name="log">外部日志消息对象。</param>
	/// <returns>返回日志是否应显示。</returns>
	/// <remarks>
	/// 由 RefreshVisibleLogs 判定每条日志是否显示。
	/// </remarks>
	private bool IsVisible(HomeLogItemViewModel log)
	{
		HomeLogSource source = log.Source;
		if (1 == 0)
		{
		}
		bool flag = source switch
		{
			HomeLogSource.System => ShowSystemLogs, 
			HomeLogSource.Process => ShowProcessLogs, 
			HomeLogSource.Debug => ShowDebugLogs, 
			HomeLogSource.Hardware => ShowHardwareLogs, 
			_ => true, 
		};
		if (1 == 0)
		{
		}
		bool flag2 = flag;
		HomeLogKind kind = log.Kind;
		if (1 == 0)
		{
		}
		flag = kind switch
		{
			HomeLogKind.Operation => ShowOperationLogs, 
			HomeLogKind.Detection => ShowDetectionLogs, 
			_ => true, 
		};
		if (1 == 0)
		{
		}
		bool flag3 = flag;
		HomeLogLevel level = log.Level;
		if (1 == 0)
		{
		}
		flag = level switch
		{
			HomeLogLevel.Info => ShowInfoLogs, 
			HomeLogLevel.Warning => ShowWarningLogs, 
			HomeLogLevel.Error => ShowErrorLogs, 
			_ => true, 
		};
		if (1 == 0)
		{
		}
		bool flag4 = flag;
		return flag2 && flag3 && flag4;
	}

	/// <summary>
	/// 刷新采血管与顶空瓶料架可视状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由数量和状态变化时调用。
	/// </remarks>
	private void UpdateRackVisuals()
	{
		foreach (RackSlotItemViewModel tubeRackSlot in TubeRackSlots)
		{
			if (tubeRackSlot.Number > _selectedTubeCount)
			{
				tubeRackSlot.Fill = IdleSlotFill;
				tubeRackSlot.Foreground = IdleSlotText;
				continue;
			}

			if (_tubeCompletedSlots.Contains(tubeRackSlot.Number))
			{
				tubeRackSlot.Fill = CompletedSlotFill;
				tubeRackSlot.Foreground = ActiveSlotText;
				continue;
			}

			if (_tubeRunningSlots.Contains(tubeRackSlot.Number))
			{
				tubeRackSlot.Fill = RunningSlotFill;
				tubeRackSlot.Foreground = ActiveSlotText;
				continue;
			}

			tubeRackSlot.Fill = ActiveSlotFill;
			tubeRackSlot.Foreground = ActiveSlotText;
		}
		foreach (RackSlotItemViewModel headspaceRackSlot in HeadspaceRackSlots)
		{
			if (headspaceRackSlot.Number > _selectedHeadspaceCount)
			{
				headspaceRackSlot.Fill = IdleSlotFill;
				headspaceRackSlot.Foreground = IdleSlotText;
				continue;
			}

			if (_headspaceCompletedSlots.Contains(headspaceRackSlot.Number))
			{
				headspaceRackSlot.Fill = CompletedSlotFill;
				headspaceRackSlot.Foreground = ActiveSlotText;
				continue;
			}

			if (_headspaceRunningSlots.Contains(headspaceRackSlot.Number))
			{
				headspaceRackSlot.Fill = RunningSlotFill;
				headspaceRackSlot.Foreground = ActiveSlotText;
				continue;
			}

			headspaceRackSlot.Fill = ActiveSlotFill;
			headspaceRackSlot.Foreground = ActiveSlotText;
		}
	}

	/// <summary>
	/// 构建采血管料架槽位集合。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程初始化料架数据时调用。
	/// </remarks>
	private void BuildTubeRackSlots()
	{
		TubeRackSlots.Clear();
		for (int i = 1; i <= 50; i++)
		{
			TubeRackSlots.Add(new RackSlotItemViewModel
			{
				Number = i,
				Fill = IdleSlotFill,
				Foreground = IdleSlotText
			});
		}
	}

	/// <summary>
	/// 构建顶空瓶料架槽位集合。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程初始化料架数据时调用。
	/// </remarks>
	private void BuildHeadspaceRackSlots()
	{
		HeadspaceRackSlots.Clear();
		for (int i = 1; i <= 100; i++)
		{
			HeadspaceRackSlots.Add(new RackSlotItemViewModel
			{
				Number = i,
				Fill = IdleSlotFill,
				Foreground = IdleSlotText
			});
		}
	}

	/// <summary>
	/// 构建针头状态槽位集合。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程初始化针头状态时调用。
	/// </remarks>
	private void BuildNeedleHeadSlots()
	{
		NeedleHeadSlots.Clear();
		for (int i = 1; i <= 80; i++)
		{
			bool flag = i <= 30;
			NeedleHeadSlots.Add(new RackSlotItemViewModel
			{
				Number = i,
				Fill = (flag ? NeedleUsedFill : NeedleIdleFill),
				Foreground = (flag ? ActiveSlotText : IdleSlotText)
			});
		}
	}

	/// <summary>
	/// 构建首页条件项列表。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程初始化条件列表时调用。
	/// </remarks>
	private void BuildConditions()
	{
		Conditions.Clear();
		Conditions.Add(new ConditionItemViewModel("加热箱温度", "0.0", "°C"));
		Conditions.Add(new ConditionItemViewModel("定量环温度", "0.0", "°C"));
		Conditions.Add(new ConditionItemViewModel("传输线温度", "0.0", "°C"));
		Conditions.Add(new ConditionItemViewModel("样品瓶平衡", "0", "s"));
		Conditions.Add(new ConditionItemViewModel("样品瓶加压", "0", "s"));
		Conditions.Add(new ConditionItemViewModel("定量环平衡", "0", "s"));
		Conditions.Add(new ConditionItemViewModel("进样时间", "0", "s"));
	}

	/// <summary>
	/// 生成首页默认提示日志。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程初始化首页日志时调用。
	/// </remarks>
	private void BuildDefaultLogs()
	{
		_allLogs.Clear();
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "系统启动完成，等待任务。", null, persistToFile: false);
		AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Operation, "请点击采血管架设定检测总数。", null, persistToFile: false);
	}

	/// <summary>
	/// 释放首页资源并注销事件及后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由页面销毁流程调用。
	/// </remarks>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		OperationModeService.ModeChanged -= OnOperationModeChanged;
		CommunicationManager.OnLogReceived -= OnCommunicationLogReceived;
		_workflowEngine.OnLogGenerated -= OnWorkflowLogGenerated;
		StopTubeCountSync();
		StopAlarmMonitor();
		StopProcessModeMonitor();
		StopRackProcessMonitor();
		UnregisterCorePlcPollingPoints();
	}

	/// <summary>
	/// 将十六进制颜色文本转换为画刷对象。
	/// </summary>
	/// By:ChengLei
	/// <param name="hex">十六进制颜色文本。</param>
	/// <returns>返回对应颜色画刷。</returns>
	/// <remarks>
	/// 由静态颜色字段初始化时调用。
	/// </remarks>
	private static Brush BrushFromHex(string hex)
	{
		object? brushObject = new BrushConverter().ConvertFromString(hex);
		if (brushObject is Brush brush)
		{
			return brush;
		}
		throw new InvalidOperationException("Invalid brush color: " + hex);
	}
}

public class RackSlotItemViewModel : BaseViewModel
{
	private Brush _fill = Brushes.WhiteSmoke;

	private Brush _foreground = Brushes.Black;

	public int Number { get; set; }

	public Brush Fill
	{
		get
		{
			return _fill;
		}
		set
		{
			if (_fill != value)
			{
				_fill = value;
				OnPropertyChanged("Fill");
			}
		}
	}

	public Brush Foreground
	{
		get
		{
			return _foreground;
		}
		set
		{
			if (_foreground != value)
			{
				_foreground = value;
				OnPropertyChanged("Foreground");
			}
		}
	}
}

public class ConditionItemViewModel : BaseViewModel
{
	private string _value;

	public string Name { get; }

	public string Unit { get; }

	public string Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (_value != value)
			{
				_value = value;
				OnPropertyChanged("Value");
			}
		}
	}

	public ConditionItemViewModel(string name, string value, string unit)
	{
		Name = name;
		_value = value;
		Unit = unit;
	}
}

public class HomeLogItemViewModel
{
	public DateTime Timestamp { get; set; } = DateTime.Now;

	public string Time { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;

	public HomeLogLevel Level { get; set; }

	public HomeLogSource Source { get; set; }

	public HomeLogKind Kind { get; set; }

	public int TubeIndex { get; set; }

	public string LevelText => Level switch
	{
		HomeLogLevel.Warning => "警告",
		HomeLogLevel.Error => "错误",
		_ => "信息"
	};

	public string SourceText => Source switch
	{
		HomeLogSource.System => "系统日志",
		HomeLogSource.Process => "进程日志",
		HomeLogSource.Debug => "调试日志",
		HomeLogSource.Hardware => "硬件日志",
		_ => "未知来源"
	};

	public string KindText => Kind switch
	{
		HomeLogKind.Detection => "检测日志",
		_ => "普通操作日志"
	};

	public string TubeText => TubeIndex > 0 ? TubeIndex.ToString() : "运行";
}

public enum HomeLogLevel
{
	Info,
	Warning,
	Error
}

public enum HomeLogSource
{
	System,
	Process,
	Debug,
	Hardware
}

public enum HomeLogKind
{
	Operation,
	Detection
}

