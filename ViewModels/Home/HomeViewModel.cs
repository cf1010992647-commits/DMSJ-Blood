using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

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
	private const int MaxTubeCount = 50;

	private const int MaxHeadspaceCount = 100;

	private const int MaxNeedleHeadCount = 50;

	private const string ProcessParameterConfigFileName = "ProcessParameterConfig.json";

	private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";

	private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(10);

	private static readonly TimeSpan InitPollInterval = TimeSpan.FromMilliseconds(100);

	private static readonly TimeSpan AlarmPollInterval = TimeSpan.FromMilliseconds(200);

	private static readonly TimeSpan ProcessModePollInterval = TimeSpan.FromMilliseconds(300);

	private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(900);

	private static readonly TimeSpan RackProcessPollInterval = TimeSpan.FromMilliseconds(300);

	private static readonly TimeSpan TemperaturePollInterval = TimeSpan.FromSeconds(5);

	private static readonly TimeSpan TemperatureWriteRefreshInterval = TimeSpan.FromSeconds(30);

	private static readonly TimeSpan BackgroundStopTimeout = TimeSpan.FromSeconds(2);

	private readonly ConfigService<ProcessParameterConfig> _processParameterConfigService = new ConfigService<ProcessParameterConfig>(ProcessParameterConfigFileName);

	private readonly ConfigService<WeightToZCalibrationConfig> _weightToZConfigService = new ConfigService<WeightToZCalibrationConfig>(WeightToZConfigFileName);

	private readonly WorkflowEngine _workflowEngine;

	private readonly IUiDispatcher _uiDispatcher;

	private readonly HomeDetectionStateCoordinator _detectionState;

	private readonly HomeDetectionCommandCoordinator _detectionCommands;

	private readonly HomeLogOutputCoordinator _homeLogOutput;

	private readonly HomeInteractionCoordinator _interactionCoordinator;

	private readonly HomeBackgroundTaskCoordinator _backgroundTasks;

	private readonly HomeLogIngressCoordinator _logIngress;

	private readonly HomeTubeDetailPresenter _tubeDetailPresenter;

	private readonly HomeConditionCoordinator _conditionCoordinator;

	private readonly TemperatureService _temperatureService = new TemperatureService();

	private readonly HomeSampleVolumeConverter _sampleVolumeConverter;

	private readonly HomeLogController _homeLogController;

	private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;

	private readonly HomePlcGateway _plcGateway;

	private readonly HomePlcCommandCoordinator _plcCommands;

	private readonly HomeBackgroundTaskSlot _tubeCountSyncTaskSlot = new HomeBackgroundTaskSlot("采血管数量同步");

	private readonly HomeBackgroundTaskSlot _alarmMonitorTaskSlot = new HomeBackgroundTaskSlot("报警监控");

	private readonly HomeBackgroundTaskSlot _operationModeMonitorTaskSlot = new HomeBackgroundTaskSlot("档位监控");

	private readonly HomeBackgroundTaskSlot _processModeMonitorTaskSlot = new HomeBackgroundTaskSlot("工艺模式监控");

	private readonly HomeBackgroundTaskSlot _rackProcessMonitorTaskSlot = new HomeBackgroundTaskSlot("料架工序监控");

	private readonly HomeBackgroundTaskSlot _temperatureMonitorTaskSlot = new HomeBackgroundTaskSlot("温控监控");

	private readonly ConcurrentQueue<TubeProcessEvent> _tubeProcessEvents = new ConcurrentQueue<TubeProcessEvent>();

	private readonly SemaphoreSlim _tubeProcessEventSignal = new SemaphoreSlim(0);

	private readonly HomeTubeProcessState _tubeProcessState;
	private readonly HomeRackProcessState _rackProcessState;

	private volatile bool _isAlarmActive;

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

	private string _lastExportPath = string.Empty;

	private string _exportDirectory = string.Empty;

	private string _currentBatchNo = string.Empty;

	private int _selectedTubeCount;

	private int _selectedHeadspaceCount;

	private int _selectedDetailTubeIndex;

	private readonly HomeBackgroundTaskSlot _tubeProcessEventTaskSlot = new HomeBackgroundTaskSlot("采血管事件处理");

	private bool _showSystemLogs = true;

	private bool _showProcessLogs = true;

	private bool _showDebugLogs = true;

	private bool _showHardwareLogs = true;

	private bool _showOperationLogs = true;

	private bool _showDetectionLogs = true;

	private bool _showInfoLogs = true;

	private bool _showWarningLogs = true;

	private bool _showErrorLogs = true;

	private OperationMode _operationMode = OperationModeService.CurrentMode;

	private HomeProcessModeState _currentProcessMode = HomeProcessModeState.Standby;

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
			return _detectionState.CountRuleText;
		}
		private set
		{
			if (_detectionState.SetCountRuleText(value))
			{
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

	public bool IsTubeSelectionEnabled => _detectionState.IsTubeSelectionEnabled;

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

	public bool CanSwitchToAutoMode => CommunicationManager.Is485Open && IsManualMode;

	public bool CanSwitchToManualMode => CommunicationManager.Is485Open && IsAutoMode;

	public string ModeDisplayText => IsAutoMode ? "当前档位：自动" : "当前档位：手动";

	public bool IsStandbyProcessMode => _currentProcessMode == HomeProcessModeState.Standby;

	public bool IsPressureProcessMode => _currentProcessMode == HomeProcessModeState.Pressure;

	public bool IsExhaustProcessMode => _currentProcessMode == HomeProcessModeState.Exhaust;

	public bool IsInjectionProcessMode => _currentProcessMode == HomeProcessModeState.Injection;

	public string ProcessModeDisplayText => _currentProcessMode switch
	{
		HomeProcessModeState.Pressure => "当前模式：压力模式",
		HomeProcessModeState.Exhaust => "当前模式：排气模式",
		HomeProcessModeState.Injection => "当前模式：注入模式",
		_ => "当前模式：待机模式"
	};

	public ObservableCollection<RackSlotItemViewModel> TubeRackSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<RackSlotItemViewModel> HeadspaceRackSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<RackSlotItemViewModel> NeedleHeadSlots { get; } = new ObservableCollection<RackSlotItemViewModel>();

	public ObservableCollection<ConditionItemViewModel> Conditions { get; } = new ObservableCollection<ConditionItemViewModel>();

	public ObservableCollection<HomeLogItemViewModel> VisibleLogs => _homeLogController.VisibleLogs;

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
		: this(new WorkflowEngine(), new WpfUiDispatcher())
	{
	}

	/// <summary>
	/// 初始化首页视图模型并注入核心依赖。
	/// </summary>
	/// By:ChengLei
	/// <param name="workflowEngine">流程引擎实例。</param>
	/// <param name="uiDispatcher">UI线程调度器。</param>
	/// <remarks>
	/// 由测试或未来组合根调用，默认构造函数仍使用生产依赖。
	/// </remarks>
	public HomeViewModel(WorkflowEngine workflowEngine, IUiDispatcher uiDispatcher)
	{
		_workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
		_uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
		_detectionState = new HomeDetectionStateCoordinator();
		_detectionCommands = new HomeDetectionCommandCoordinator();
		_homeLogOutput = new HomeLogOutputCoordinator();
		_interactionCoordinator = new HomeInteractionCoordinator();
		_backgroundTasks = new HomeBackgroundTaskCoordinator(BackgroundStopTimeout, (level, source, kind, message) => AddLog(level, source, kind, message));
		_logIngress = new HomeLogIngressCoordinator();
		_tubeDetailPresenter = new HomeTubeDetailPresenter();
		_conditionCoordinator = new HomeConditionCoordinator(_processParameterConfigService);
		_sampleVolumeConverter = new HomeSampleVolumeConverter(_weightToZConfigService);
		_homeLogController = new HomeLogController(
			() => _homeLogOutput.LogTool,
			() => _homeLogOutput.GetCurrentBatchNoForLogging(_currentBatchNo));
		_plcGateway = new HomePlcGateway(_plcLock, CoilCacheMaxAge);
		_plcCommands = new HomePlcCommandCoordinator(_plcGateway, InitTimeout, InitPollInterval);
		_tubeProcessState = new HomeTubeProcessState(_sampleVolumeConverter.BuildSampleVolumeFromWeight);
		_rackProcessState = new HomeRackProcessState(MaxTubeCount, MaxHeadspaceCount, MaxNeedleHeadCount);
		InitCommand = new AsyncRelayCommand(
			InitializeSystemAsync,
			() => !_detectionState.IsInitializing,
			ex => AddLog(HomeLogLevel.Error, HomeLogSource.System, HomeLogKind.Operation, "初始化失败：" + ex.Message));
		StartCommand = new AsyncRelayCommand(
			StartDetectionAsync,
			() => !_detectionState.IsDetectionStarted && !_detectionState.IsStartCommandProcessing,
			ex => AddLog(HomeLogLevel.Error, HomeLogSource.Process, HomeLogKind.Detection, "检测启动失败：" + ex.Message));
		StopCommand = new AsyncRelayCommand(
			StopDetectionAsync,
			() => _detectionState.IsDetectionStarted,
			ex => AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "停止检测失败：" + ex.Message));
		SaveCommand = new RelayCommand(delegate
		{
			RunAction("参数保存", HomeLogSource.Process, HomeLogKind.Operation);
		});
		LightCommand = new RelayCommand(delegate
		{
			RunAction("照明切换", HomeLogSource.Hardware, HomeLogKind.Operation);
		});
		DemoCommand = new AsyncRelayCommand(
			EmergencyStopAsync,
			onError: ex => AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "急停失败：" + ex.Message));
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
		SwitchToAutoModeCommand = new AsyncRelayCommand(
			() => SwitchOperationModeAsync(OperationMode.Auto),
			() => CanSwitchToAutoMode,
			ex => AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "档位切换失败：" + ex.Message));
		SwitchToManualModeCommand = new AsyncRelayCommand(
			() => SwitchOperationModeAsync(OperationMode.Manual),
			() => CanSwitchToManualMode,
			ex => AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "档位切换失败：" + ex.Message));
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
		CommunicationManager.OnStateChanged += OnCommunicationStateChanged;
		CommunicationManager.OnLogReceived += OnCommunicationLogReceived;
		_workflowEngine.OnLogGenerated += OnWorkflowLogGenerated;
		StartTubeProcessEventLoop();
		StartAlarmMonitor();
		StartOperationModeMonitor();
		StartProcessModeMonitor();
		StartRackProcessMonitor();
		StartTemperatureMonitor();
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
		_plcGateway.RegisterCorePollingPoints(AlarmPollInterval, ProcessModePollInterval);
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
		_plcGateway.UnregisterCorePollingPoints();
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
		if (!CommunicationManager.Is485Open)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC未连接，禁止切换自动/手动档位。");
			RefreshModeSwitchStates();
			return;
		}

		bool autoModeBit = mode == OperationMode.Auto;
		try
		{
			await _plcGateway.WriteAutoModeAsync(autoModeBit);
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
			RefreshModeSwitchStates();
			if (writeLog)
			{
				AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, (mode == OperationMode.Auto) ? "档位切换为自动（M10=1）。" : "档位切换为手动（M10=0）。");
			}
		}
		else
		{
			RefreshModeSwitchStates();
		}
	}

	/// <summary>
	/// 响应通信状态变化并刷新档位切换按钮状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 CommunicationManager.OnStateChanged 触发，确保 PLC 断开时自动/手动按钮立即禁用。
	/// </remarks>
	private void OnCommunicationStateChanged()
	{
		RunOnUiThread(RefreshModeSwitchStates);
	}

	/// <summary>
	/// 刷新自动和手动档位切换按钮状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 高亮仍由当前档位控制，可点击状态则由 PLC 在线状态和当前模式共同决定。
	/// </remarks>
	private void RefreshModeSwitchStates()
	{
		OnPropertyChanged("CanSwitchToAutoMode");
		OnPropertyChanged("CanSwitchToManualMode");
		CommandManager.InvalidateRequerySuggested();
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
		ApplyLogOutputState(_homeLogOutput.Initialize(), writeLog: false);
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
		HomeExportDirectorySelectionResult result = _interactionCoordinator.SelectExportDirectory(new HomeExportDirectorySelectionContext
		{
			CurrentExportDirectory = ExportDirectory,
			DefaultExportDirectory = _homeLogOutput.DefaultProjectLogsDirectory,
			ApplyDirectory = path => _homeLogOutput.ApplyExportDirectory(path, saveToConfig: true)
		});
		if (!string.IsNullOrWhiteSpace(result.Error))
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "选择导出目录失败：" + result.Error);
			return;
		}

		if (result.Applied && result.State.HasValue)
		{
			ApplyLogOutputState(result.State.Value, writeLog: true);
		}
	}

	/// <summary>
	/// 应用日志输出状态并同步首页绑定属性与流程日志输出。
	/// </summary>
	/// By:ChengLei
	/// <param name="state">日志输出状态。</param>
	/// <param name="writeLog">是否记录目录变更日志。</param>
	/// <remarks>
	/// 由导出目录初始化与目录选择流程调用。
	/// </remarks>
	private void ApplyLogOutputState(HomeLogOutputState state, bool writeLog)
	{
		ExportDirectory = state.ExportDirectory;
		LastExportPath = state.ExportDirectory;
		_workflowEngine.ConfigureLogOutput(state.LogTool, () => _homeLogOutput.GetCurrentBatchNoForLogging(_currentBatchNo));
		if (writeLog)
		{
			AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出目录已设置：" + state.ExportDirectory);
		}
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
		await _detectionCommands.InitializeAsync(new HomeInitializeCommandContext
		{
			DetectionState = _detectionState,
			LoadProcessParameterConfig = () => _processParameterConfigService.Load() ?? new ProcessParameterConfig(),
			ApplyConditions = config => RunOnUiThread(() => HomeConditionPresenter.Apply(Conditions, config)),
			InitializePlcAsync = (config, onReadError) => _plcCommands.InitializeAsync(config, onReadError),
			AddLog = (level, source, kind, message) => AddLog(level, source, kind, message),
			InvalidateCommands = CommandManager.InvalidateRequerySuggested
		});
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
		OnPropertyChanged("CountRuleText");
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
		await _detectionCommands.StartAsync(new HomeStartCommandContext
		{
			DetectionState = _detectionState,
			SelectedTubeCount = _selectedTubeCount,
			SelectedHeadspaceCount = _selectedHeadspaceCount,
			IsPlcConnected = () => CommunicationManager.Is485Open,
			ResetStartCommandLowAsync = ResetStartCommandLowAsync,
			TryStartAsync = _plcCommands.TryStartAsync,
			SetAlarmActive = value => _isAlarmActive = value,
			ClearTubeProcessRuntimeState = ClearTubeProcessRuntimeState,
			AllocateNextBatchNo = _homeLogOutput.AllocateNextBatchNo,
			SetCurrentBatchNo = batchNo => _currentBatchNo = batchNo,
			RefreshDetectionState = RefreshDetectionCommandStates,
			StartWorkflow = _workflowEngine.Start,
			StartTubeCountSync = StartTubeCountSync,
			AddLog = (level, source, kind, message) => AddLog(level, source, kind, message),
			InvalidateCommands = CommandManager.InvalidateRequerySuggested
		});
	}

	/// <summary>
	/// 执行检测停止流程并下发停止信号。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回检测停止任务。</returns>
	/// <remarks>
	/// 由首页停止按钮或自动停机流程调用。
	/// </remarks>
	private async Task StopDetectionAsync()
	{
		await _detectionCommands.StopAsync(new HomeStopCommandContext
		{
			DetectionState = _detectionState,
			RefreshDetectionState = RefreshDetectionCommandStates,
			StopTubeCountSyncAsync = StopTubeCountSyncAsync,
			ClearTubeProcessRuntimeState = ClearTubeProcessRuntimeState,
			ClearRackProcessStates = ClearRackProcessStates,
			StopWorkflowAsync = () => _workflowEngine.StopAsync(),
			SendStopAsync = () => ExecutePlcCommandAsync(_plcCommands.SendStopAsync, "向PLC发送停止信号失败："),
			AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
		});
	}

	/// <summary>
	/// 执行急停流程并记录关键日志。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回急停处理任务。</returns>
	/// <remarks>
	/// 由急停按钮调用，优先保障设备安全停机。
	/// </remarks>
	private async Task EmergencyStopAsync()
	{
		await _detectionCommands.EmergencyStopAsync(new HomeEmergencyStopCommandContext
		{
			DetectionState = _detectionState,
			RefreshDetectionState = RefreshDetectionCommandStates,
			StopTubeCountSyncAsync = StopTubeCountSyncAsync,
			ClearTubeProcessRuntimeState = ClearTubeProcessRuntimeState,
			ClearRackProcessStates = ClearRackProcessStates,
			StopWorkflowAsync = () => _workflowEngine.StopAsync(),
			SendEmergencyStopAsync = () => ExecutePlcCommandAsync(_plcCommands.SendEmergencyStopAsync, "急停信号发送失败："),
			SendStopAsync = () => ExecutePlcCommandAsync(_plcCommands.SendStopAsync, "向PLC发送停止信号失败："),
			AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
		});
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
		_interactionCoordinator.HandleTubeSlotClick(new HomeTubeSlotClickContext
		{
			Slot = slot,
			IsDetectionStarted = _detectionState.IsDetectionStarted,
			SetSelectedDetailTubeIndex = value => _selectedDetailTubeIndex = value,
			ApplyCount = ApplyCount,
			RefreshSelectedTubeDetails = RefreshSelectedTubeDetails
		});
	}

	/// <summary>
	/// 启动采血管事件串行处理后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由构造流程调用，用于统一串行处理流程日志、寄存器事件和单管详情刷新。
	/// </remarks>
	private void StartTubeProcessEventLoop()
	{
		_backgroundTasks.Restart(_tubeProcessEventTaskSlot, TubeProcessEventLoopAsync);
	}

	/// <summary>
	/// 停止采血管事件串行处理后台任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由停止检测和释放流程调用，防止后台消费者泄漏。
	/// </remarks>
	private async Task StopTubeProcessEventLoopAsync()
	{
		await _backgroundTasks.StopAsync(_tubeProcessEventTaskSlot).ConfigureAwait(false);
	}

	/// <summary>
	/// 往采血管事件队列追加一条待处理事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeEvent">待处理事件。</param>
	/// <remarks>
	/// 由流程日志与料架工序监控统一调用，后续由单线程消费者串行处理。
	/// </remarks>
	private void EnqueueTubeProcessEvent(TubeProcessEvent tubeEvent)
	{
		if (tubeEvent == null || tubeEvent.TubeIndex <= 0)
		{
			return;
		}

		_tubeProcessEvents.Enqueue(tubeEvent);
		_tubeProcessEventSignal.Release();
	}

	/// <summary>
	/// 串行消费采血管事件并更新上下文。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于停止后台消费者。</param>
	/// <returns>返回后台循环任务。</returns>
	/// <remarks>
	/// 由 StartTubeProcessEventLoop 启动，保证同一批次内所有归属更新按事件顺序执行。
	/// </remarks>
	private async Task TubeProcessEventLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				await _tubeProcessEventSignal.WaitAsync(token);
				while (_tubeProcessEvents.TryDequeue(out TubeProcessEvent? tubeEvent))
				{
					ProcessTubeProcessEvent(tubeEvent);
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	/// <summary>
	/// 处理单条采血管事件并同步上下文、日志和 CSV。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeEvent">待处理事件。</param>
	/// <remarks>
	/// 由 TubeProcessEventLoopAsync 串行调用，确保上下文归属不受并发干扰。
	/// </remarks>
	private void ProcessTubeProcessEvent(TubeProcessEvent tubeEvent)
	{
		HomeTubeProcessResult result = _tubeProcessState.ApplyEvent(tubeEvent, _selectedDetailTubeIndex);
		_selectedDetailTubeIndex = result.SelectedDetailTubeIndex;
		_homeLogOutput.AppendTubeTraceRecord(_currentBatchNo, tubeEvent.Timestamp, tubeEvent.BatchNo, result.Context, result.HeadspaceCode, tubeEvent.ProcessName, tubeEvent.EventName, tubeEvent.PlcValue, tubeEvent.DurationSeconds, tubeEvent.Note);
		AddLog(tubeEvent.HomeLogLevel, tubeEvent.HomeLogSource, tubeEvent.HomeLogKind, tubeEvent.HomeLogMessage, tubeEvent.TubeIndex, tubeEvent.PersistHomeLogToFile);
		RefreshSelectedTubeDetails();
	}

	/// <summary>
	/// 根据当前选中的采血管上下文刷新首页详情显示。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由采血管选择和事件串行处理流程调用，首页详情始终只显示选中采血管的数据。
	/// </remarks>
	private void RefreshSelectedTubeDetails()
	{
		if (!IsOnUiThread())
		{
			RunOnUiThread(RefreshSelectedTubeDetails);
			return;
		}

		TubeContext? context = null;
		if (_selectedDetailTubeIndex > 0)
		{
			_tubeProcessState.TryGetContext(_selectedDetailTubeIndex, out context);
		}

		_tubeDetailPresenter.Apply(
			new HomeTubeDetailApplyContext
			{
				SetScanCode = value => ScanCode = value,
				SetSampleVolume = value => SampleVolume = value,
				SetHeadspaceASampleWeight = value => HeadspaceASampleWeight = value,
				SetHeadspaceAButanolWeight = value => HeadspaceAButanolWeight = value,
				SetHeadspaceBSampleWeight = value => HeadspaceBSampleWeight = value,
				SetHeadspaceBButanolWeight = value => HeadspaceBButanolWeight = value
			},
			context);
	}

	/// <summary>
	/// 清理当前批次的采血管上下文和事件队列状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始新批次 停止检测 和 急停流程调用，避免旧批次上下文串入新批次。
	/// </remarks>
	private void ClearTubeProcessRuntimeState()
	{
		_tubeProcessState.Clear();
		_selectedDetailTubeIndex = 0;
		while (_tubeProcessEvents.TryDequeue(out _))
		{
		}

		RefreshSelectedTubeDetails();
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
		_backgroundTasks.Restart(_alarmMonitorTaskSlot, token => HomeMonitorLoops.RunAlarmMonitorAsync(
			new HomeAlarmMonitorContext
			{
				PollInterval = AlarmPollInterval,
				IsPlcConnected = () => CommunicationManager.Is485Open,
				IsDetectionStarted = () => _detectionState.IsDetectionStarted,
				ReadAlarmAsync = async token =>
				{
					var read = await _plcGateway.TryReadAlarmSummaryAsync(token).ConfigureAwait(false);
					return new HomePlcBoolReadResult(read.Success, read.Value, read.Error);
				},
				SetAlarmActive = value => _isAlarmActive = value,
				RunOnUiThread = RunOnUiThread,
				SetCountRuleText = text => CountRuleText = text,
				AutoStopDetectionAsync = AutoStopDetectionByAlarmAsync,
				AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
			},
			token));
	}

	/// <summary>
	/// 停止报警监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由停止/释放流程调用。
	/// </remarks>
	private async Task StopAlarmMonitorAsync()
	{
		await _backgroundTasks.StopAsync(_alarmMonitorTaskSlot).ConfigureAwait(false);
	}

	/// <summary>
	/// 启动自动手动档位同步后台任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 软件打开后持续直读 PLC 的 M10 位，保证首页档位显示和设置页权限与设备真实状态一致。
	/// </remarks>
	private void StartOperationModeMonitor()
	{
		_backgroundTasks.Restart(_operationModeMonitorTaskSlot, token => HomeMonitorLoops.RunOperationModeMonitorAsync(
			new HomeOperationModeMonitorContext
			{
				PollInterval = ProcessModePollInterval,
				IsPlcConnected = () => CommunicationManager.Is485Open,
				ReadAutoModeAsync = async token =>
				{
					var read = await _plcGateway.TryReadAutoModeDirectAsync(token).ConfigureAwait(false);
					return new HomePlcBoolReadResult(read.Success, read.Value, read.Error);
				},
				RunOnUiThread = RunOnUiThread,
				SetOperationMode = mode => OperationModeService.CurrentMode = mode,
				AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
			},
			token));
	}

	/// <summary>
	/// 停止自动手动档位同步后台任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由首页释放流程调用，避免页面关闭后仍持续读取 PLC 档位。
	/// </remarks>
	private async Task StopOperationModeMonitorAsync()
	{
		await _backgroundTasks.StopAsync(_operationModeMonitorTaskSlot).ConfigureAwait(false);
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
		_backgroundTasks.Restart(_processModeMonitorTaskSlot, token => HomeMonitorLoops.RunProcessModeMonitorAsync(
			new HomeProcessModeMonitorContext
			{
				PollInterval = ProcessModePollInterval,
				IsPlcConnected = () => CommunicationManager.Is485Open,
				ReadProcessModeAsync = async token =>
				{
					var modeReads = await _plcGateway.ReadProcessModeCoilsAsync(token).ConfigureAwait(false);
					return new HomeProcessModeReadResult(
						new HomePlcBoolReadResult(modeReads.Standby.Success, modeReads.Standby.Value, modeReads.Standby.Error),
						new HomePlcBoolReadResult(modeReads.Pressure.Success, modeReads.Pressure.Value, modeReads.Pressure.Error),
						new HomePlcBoolReadResult(modeReads.Exhaust.Success, modeReads.Exhaust.Value, modeReads.Exhaust.Error),
						new HomePlcBoolReadResult(modeReads.Injection.Success, modeReads.Injection.Value, modeReads.Injection.Error));
				},
				RunOnUiThread = RunOnUiThread,
				SetProcessMode = SetCurrentProcessMode,
				AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
			},
			token));
	}

	/// <summary>
	/// 停止工艺模式监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由停止/释放流程调用。
	/// </remarks>
	private async Task StopProcessModeMonitorAsync()
	{
		await _backgroundTasks.StopAsync(_processModeMonitorTaskSlot).ConfigureAwait(false);
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
		_backgroundTasks.Restart(_rackProcessMonitorTaskSlot, token => HomeMonitorLoops.RunRackProcessMonitorAsync(
			new HomeRackProcessMonitorContext
			{
				PollInterval = RackProcessPollInterval,
				IsPlcConnected = () => CommunicationManager.Is485Open,
				IsDetectionStarted = () => _detectionState.IsDetectionStarted,
				ReadRegistersAsync = async token =>
				{
					var read = await _plcGateway.ReadRackProcessRegistersAsync(token).ConfigureAwait(false);
					return new HomePlcRegisterReadResult(read.Success, read.Values, read.Error);
				},
				RunOnUiThread = RunOnUiThread,
				ClearStates = ClearRackProcessStates,
				ApplyRegisters = registers => ApplyRackProcessRegisters(registers),
				AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
			},
			token));
	}

	/// <summary>
	/// 启动温控后台监控任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 软件打开后即持续运行 使用同一条温控 TCP 通道并按协议站号轮询四路温控。
	/// </remarks>
	private void StartTemperatureMonitor()
	{
		_backgroundTasks.Restart(_temperatureMonitorTaskSlot, token => HomeMonitorLoops.RunTemperatureMonitorAsync(
			new HomeTemperatureMonitorContext
			{
				PollInterval = TemperaturePollInterval,
				WriteRefreshInterval = TemperatureWriteRefreshInterval,
				TemperatureTolerance = 0.2d,
				IsTcpRunning = () => CommunicationManager.IsTcpRunning,
				LoadTargets = BuildTemperatureMonitorTargets,
				ResolveDeviceKey = () => CommunicationManager.GetDeviceKey("温控"),
				IsDeviceConnected = deviceKey => CommunicationManager.TcpServer.IsDeviceConnected(deviceKey),
				ReadTemperatureAsync = (station, token) => _temperatureService.ReadCurrentTemperatureAsync(station, token: token),
				WriteTargetTemperatureAsync = (station, targetTemperature, token) => _temperatureService.SetTargetTemperatureAsync(station, targetTemperature, token: token),
				AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
			},
			token));
	}

	/// <summary>
	/// 停止温控后台监控任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由首页释放流程调用 避免页面关闭后仍继续轮询温控。
	/// </remarks>
	private async Task StopTemperatureMonitorAsync()
	{
		await _backgroundTasks.StopAsync(_temperatureMonitorTaskSlot).ConfigureAwait(false);
	}

	/// <summary>
	/// 构建温控后台监控目标集合。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回按站号组织的温控目标集合。</returns>
	/// <remarks>
	/// 当前四路温控共用同一端口 具体站号由参数配置页和配置文件决定。
	/// </remarks>
	private IReadOnlyList<HomeTemperatureMonitorTarget> BuildTemperatureMonitorTargets()
	{
		ProcessParameterConfig config = _conditionCoordinator.LoadSafely();
		return new[]
		{
			new HomeTemperatureMonitorTarget("加热箱温控", config.HeatingBoxTemperatureStation, config.HeatingBoxTemperature),
			new HomeTemperatureMonitorTarget("定量环温控", config.QuantitativeLoopTemperatureStation, config.QuantitativeLoopTemperature),
			new HomeTemperatureMonitorTarget("传输线温控", config.TransferLineTemperatureStation, config.TransferLineTemperature),
			new HomeTemperatureMonitorTarget("预留温控", config.ReservedTemperatureStation, config.ReservedTemperature)
		};
	}

	/// <summary>
	/// 停止料架工序状态监控后台任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由 Dispose 调用，避免页面关闭后仍持续读取PLC寄存器。
	/// </remarks>
	private async Task StopRackProcessMonitorAsync()
	{
		await _backgroundTasks.StopAsync(_rackProcessMonitorTaskSlot).ConfigureAwait(false);
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
		HomeRackProcessResult result = _rackProcessState.ApplyRegisters(registers, _detectionState.IsDetectionStarted, _homeLogOutput.GetCurrentBatchNoForLogging(_currentBatchNo));
		foreach (TubeProcessEvent tubeEvent in result.Events)
		{
			EnqueueTubeProcessEvent(tubeEvent);
		}

		if (result.Changed)
		{
			UpdateRackVisuals();
		}
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
		if (!_rackProcessState.Clear())
		{
			return;
		}

		UpdateRackVisuals();
	}

	/// <summary>
	/// 设置当前工艺模式并刷新显示文本。
	/// </summary>
	/// By:ChengLei
	/// <param name="mode">运行模式枚举值。</param>
	/// <remarks>
	/// 由工艺模式监控流程调用。
	/// </remarks>
	private void SetCurrentProcessMode(HomeProcessModeState mode)
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
	private static string GetProcessModeText(HomeProcessModeState mode)
	{
		return mode switch
		{
			HomeProcessModeState.Standby => "待机",
			HomeProcessModeState.Pressure => "加压",
			HomeProcessModeState.Exhaust => "排气",
			HomeProcessModeState.Injection => "进样",
			_ => "未知"
		};
	}

	/// <summary>
	/// 执行首页PLC命令并在失败时写入统一日志。
	/// </summary>
	/// By:ChengLei
	/// <param name="commandFactory">返回PLC命令结果的异步委托。</param>
	/// <param name="failureMessagePrefix">失败日志前缀文本。</param>
	/// <returns>返回命令执行等待任务。</returns>
	/// <remarks>
	/// 由停止 急停 和命令位复位等复用 避免首页重复编写相同异常日志逻辑。
	/// </remarks>
	private async Task ExecutePlcCommandAsync(Func<Task<HomeCommandResult>> commandFactory, string failureMessagePrefix)
	{
		HomeCommandResult result = await commandFactory().ConfigureAwait(true);
		if (!result.Success)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, failureMessagePrefix + result.Error);
		}
	}

	/// <summary>
	/// 复位开始命令位到低电平并在失败时写日志。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回复位等待任务。</returns>
	/// <remarks>
	/// 由开始前置条件不满足和开始异常流程复用。
	/// </remarks>
	private Task ResetStartCommandLowAsync()
	{
		return ExecutePlcCommandAsync(_plcCommands.EnsureStartCommandLowAsync, "写入 M5=0 失败：");
	}

	/// <summary>
	/// 在报警触发时自动停止检测流程。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回自动停机任务。</returns>
	/// <remarks>
	/// 由 AlarmMonitorLoopAsync 在报警触发后调用。
	/// </remarks>
	private async Task AutoStopDetectionByAlarmAsync()
	{
		await _detectionCommands.AutoStopByAlarmAsync(new HomeAutoStopCommandContext
		{
			DetectionState = _detectionState,
			RefreshDetectionState = RefreshDetectionCommandStates,
			StopTubeCountSyncAsync = StopTubeCountSyncAsync,
			ClearRackProcessStates = ClearRackProcessStates,
			StopWorkflowAsync = () => _workflowEngine.StopAsync(),
			SendStopAsync = () => ExecutePlcCommandAsync(_plcCommands.SendStopAsync, "向PLC发送停止信号失败："),
			AddLog = (level, source, kind, message) => AddLog(level, source, kind, message)
		});
	}

	/// <summary>
	/// 在UI线程执行指定委托。
	/// </summary>
	/// By:ChengLei
	/// <param name="action">需要执行的业务委托。</param>
	/// <remarks>
	/// 由后台线程回调更新界面属性时调用。
	/// </remarks>
	private void RunOnUiThread(Action action)
	{
		_uiDispatcher.BeginInvoke(action);
	}

	/// <summary>
	/// 判断当前线程是否为UI线程。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前是否为UI线程。</returns>
	/// <remarks>
	/// 由 RunOnUiThread 进行线程判断时调用。
	/// </remarks>
	private bool IsOnUiThread()
	{
		return _uiDispatcher.CheckAccess();
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
		_backgroundTasks.Restart(_tubeCountSyncTaskSlot, SyncTubeCountLoopAsync);
	}

	/// <summary>
	/// 停止采血管数量同步任务。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步停止任务。</returns>
	/// <remarks>
	/// 由停止和释放流程调用。
	/// </remarks>
	private async Task StopTubeCountSyncAsync()
	{
		await _backgroundTasks.StopAsync(_tubeCountSyncTaskSlot).ConfigureAwait(false);
	}

	/// <summary>
	/// 循环将首页数量设置同步到PLC。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断后台循环或等待。</param>
	/// <returns>返回数量同步异步任务。</returns>
	/// <remarks>
	/// 由 StartTubeCountSync 启动并循环调用首页 PLC 网关写入采血管数量。
	/// </remarks>
	private async Task SyncTubeCountLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested && _detectionState.IsDetectionStarted)
		{
			try
			{
				await _plcGateway.SendTubeCountAsync(_selectedTubeCount, token);
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
			AddLog(HomeLogLevel.Info, HomeLogSource.Process, _detectionState.ResolveCountChangeKind(), $"采血管数量设为{num}，自动映射顶空瓶数量{num2}。", num);
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
		HomeLogExportResult result = _interactionCoordinator.BuildLogExportResult(_homeLogController.ExportVisibleLogs(), ExportDirectory, legacyMode: false);
		if (!result.HasExportedFiles)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "没有可导出的日志。");
			return;
		}
		LastExportPath = result.LastExportPath;
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出完成：" + result.SuccessMessage);
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
		HomeLogExportResult result = _interactionCoordinator.BuildLogExportResult(_homeLogController.ExportVisibleLogs(), ExportDirectory, legacyMode: true);
		if (!result.HasExportedFiles)
		{
			AddLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, "没有可导出的日志。");
			return;
		}
		LastExportPath = result.LastExportPath;
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "日志导出完成：" + result.SuccessMessage);
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
		_logIngress.Add(
			new HomeLogWriteContext
			{
				IsOnUiThread = IsOnUiThread,
				RunOnUiThread = RunOnUiThread,
				Controller = _homeLogController,
				CreateFilterState = CreateLogFilterState,
				ApplyLogCounters = ApplyLogCounters
			},
			level,
			source,
			kind,
			message,
			tubeIndex,
			persistToFile);
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
		_logIngress.HandleCommunicationLog(
			(level, source, kind, message, tubeIndex, persistToFile) => AddLog(level, source, kind, message, tubeIndex, persistToFile),
			log);
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
		_logIngress.HandleWorkflowLog(
			new HomeWorkflowLogIngressContext
			{
				GetCurrentBatchNoForLogging = () => _homeLogOutput.GetCurrentBatchNoForLogging(_currentBatchNo),
				EnqueueTubeProcessEvent = EnqueueTubeProcessEvent,
				AddLog = (level, source, kind, message, tubeIndex, persistToFile) => AddLog(level, source, kind, message, tubeIndex, persistToFile)
			},
			log);
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
		ApplyLogCounters(_homeLogController.Refresh(CreateLogFilterState()));
	}

	/// <summary>
	/// 创建当前首页日志筛选状态快照。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前日志筛选状态。</returns>
	/// <remarks>
	/// 由日志控制器刷新可见集合时调用，避免控制器直接读取页面属性。
	/// </remarks>
	private HomeLogFilterState CreateLogFilterState()
	{
		return new HomeLogFilterState
		{
			ShowSystemLogs = ShowSystemLogs,
			ShowProcessLogs = ShowProcessLogs,
			ShowDebugLogs = ShowDebugLogs,
			ShowHardwareLogs = ShowHardwareLogs,
			ShowOperationLogs = ShowOperationLogs,
			ShowDetectionLogs = ShowDetectionLogs,
			ShowInfoLogs = ShowInfoLogs,
			ShowWarningLogs = ShowWarningLogs,
			ShowErrorLogs = ShowErrorLogs
		};
	}

	/// <summary>
	/// 应用日志计数到首页绑定属性。
	/// </summary>
	/// By:ChengLei
	/// <param name="counters">日志计数快照。</param>
	/// <remarks>
	/// 由日志追加和筛选刷新流程调用。
	/// </remarks>
	private void ApplyLogCounters(HomeLogCounters counters)
	{
		InfoCount = counters.InfoCount;
		WarningCount = counters.WarningCount;
		ErrorCount = counters.ErrorCount;
	}

	/// <summary>
	/// 刷新采血管、顶空瓶与移液枪头可视状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由数量和状态变化时调用。
	/// </remarks>
	private void UpdateRackVisuals()
	{
		HomeRackVisualPresenter.UpdateRackVisuals(
			TubeRackSlots,
			HeadspaceRackSlots,
			NeedleHeadSlots,
			_selectedTubeCount,
			_selectedHeadspaceCount,
			_rackProcessState.TubeRunningSlots,
			_rackProcessState.TubeCompletedSlots,
			_rackProcessState.HeadspaceRunningSlots,
			_rackProcessState.HeadspaceCompletedSlots,
			_rackProcessState.UsedNeedleHeadCount);
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
		HomeRackVisualPresenter.BuildSampleSlots(TubeRackSlots, MaxTubeCount);
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
		HomeRackVisualPresenter.BuildSampleSlots(HeadspaceRackSlots, MaxHeadspaceCount);
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
		HomeRackVisualPresenter.BuildNeedleHeadSlots(NeedleHeadSlots, MaxNeedleHeadCount);
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
		_conditionCoordinator.ApplyTo(Conditions);
	}

	/// <summary>
	/// 从最新参数配置刷新首页条件项显示值。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页重新显示和初始化参数下发前调用，确保页面展示与配置文件保持一致。
	/// </remarks>
	public void RefreshConditionsFromConfig()
	{
		_conditionCoordinator.ApplyTo(Conditions);
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
		_homeLogController.Clear();
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
		DisposeAsync().GetAwaiter().GetResult();
	}

	/// <summary>
	/// 异步释放首页资源并等待后台任务退出。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回异步释放任务。</returns>
	/// <remarks>
	/// 由应用退出流程调用，按首页后台任务、流程引擎、事件订阅顺序清理。
	/// </remarks>
	public async Task DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		OperationModeService.ModeChanged -= OnOperationModeChanged;
		CommunicationManager.OnStateChanged -= OnCommunicationStateChanged;
		CommunicationManager.OnLogReceived -= OnCommunicationLogReceived;
		_workflowEngine.OnLogGenerated -= OnWorkflowLogGenerated;
		await StopTubeCountSyncAsync().ConfigureAwait(false);
		await StopTubeProcessEventLoopAsync().ConfigureAwait(false);
		await StopAlarmMonitorAsync().ConfigureAwait(false);
		await StopOperationModeMonitorAsync().ConfigureAwait(false);
		await StopProcessModeMonitorAsync().ConfigureAwait(false);
		await StopRackProcessMonitorAsync().ConfigureAwait(false);
		await StopTemperatureMonitorAsync().ConfigureAwait(false);
		await _workflowEngine.StopAsync().ConfigureAwait(false);
		UnregisterCorePlcPollingPoints();
	}

}

