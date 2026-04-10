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

	// DMSJ：以下为采血管总是PLC地址(占位地址)，待电气确认后替换为正式地址。
	private const ushort PlcTubeCountRegisterAddress = 9000;

	// DMSJ：初始化按钮地址由电气确认为 M13（线圈地址 13）。
	private const ushort PlcInitCommandCoilAddress = 13;

	// DMSJ：初始化完成状态位地址由电气确认为 M14（线圈地址 14）。
	private const ushort PlcInitDoneCoilAddress = 14;

	// DMSJ：自动挡标记位地址（M10），M10=1 表示自动挡。
	private const ushort PlcAutoModeCoilAddress = 10;

	// DMSJ：开始命令位地址（M5），按脉冲方式发送。
	private const ushort PlcStartCommandCoilAddress = 5;

	private const ushort PlcStopCommandCoilAddress = 900;

	// DMSJ：急停位地址临时占位，由电气确认为 M3。
	private const ushort PlcEmergencyStopCoilAddress = 3;

	// DMSJ：报警汇总位地址（M2）。
	private const ushort PlcAlarmSummaryCoilAddress = 2;

	private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(10);

	private static readonly TimeSpan InitPollInterval = TimeSpan.FromMilliseconds(100);

	private static readonly TimeSpan AlarmPollInterval = TimeSpan.FromMilliseconds(200);

	private static readonly TimeSpan ProcessModePollInterval = TimeSpan.FromMilliseconds(300);

	private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(900);

	// DMSJ：以下四个模式点位暂未定版，当前为占位地址，后续按PLC点表替换。
	private const ushort PlcStandbyModeCoilAddress = 101;

	private const ushort PlcPressureModeCoilAddress = 102;

	private const ushort PlcExhaustModeCoilAddress = 103;

	private const ushort PlcInjectionModeCoilAddress = 104;

	private static readonly Brush ActiveSlotFill = BrushFromHex("#005ECC");

	private static readonly Brush ActiveSlotText = Brushes.White;

	private static readonly Brush IdleSlotFill = Brushes.WhiteSmoke;

	private static readonly Brush IdleSlotText = BrushFromHex("#0F172A");

	private static readonly Brush NeedleUsedFill = BrushFromHex("#3C4A54");

	private static readonly Brush NeedleIdleFill = Brushes.WhiteSmoke;

	private const string ExportPathConfigFileName = "HomeExportPathConfig.json";

	private readonly ConfigService<HomeExportPathConfig> _exportPathConfigService = new ConfigService<HomeExportPathConfig>("HomeExportPathConfig.json");

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

	private bool _isInitializing;

	private bool _isStartCommandProcessing;

	private volatile bool _isAlarmActive;

	private bool _isDetectionStarted;

	private int _infoCount;

	private int _warningCount;

	private int _errorCount;

	private string _sampleName = string.Empty;

	private string _sampleVolume = "0";

	private string _butanolName = string.Empty;

	private string _butanolVolume = "0";

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
	}

	private void RegisterCorePlcPollingPoints()
	{
		CommunicationManager.PlcPolling.RegisterCoil(PlcAlarmSummaryCoilAddress, AlarmPollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcStandbyModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcPressureModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcExhaustModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PlcInjectionModeCoilAddress, ProcessModePollInterval);
		CommunicationManager.PlcPolling.Start();
	}

	private void UnregisterCorePlcPollingPoints()
	{
		CommunicationManager.PlcPolling.UnregisterCoil(PlcAlarmSummaryCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcStandbyModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcPressureModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcExhaustModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PlcInjectionModeCoilAddress);
	}

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

	private void OnOperationModeChanged(OperationMode mode)
	{
		RunOnUiThread(delegate
		{
			ApplyOperationMode(mode, writeLog: false);
		});
	}

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

	private void InitializeExportDirectory()
	{
		HomeExportPathConfig homeExportPathConfig = _exportPathConfigService.Load() ?? new HomeExportPathConfig();
		string defaultProjectLogsDirectory = GetDefaultProjectLogsDirectory();
		string directoryPath = (string.IsNullOrWhiteSpace(homeExportPathConfig.ExportDirectory) ? defaultProjectLogsDirectory : homeExportPathConfig.ExportDirectory);
		ApplyExportDirectory(directoryPath, saveToConfig: true, writeLog: false);
		LastExportPath = ExportDirectory;
	}

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

	private void StartDetection()
	{
		_ = StartDetectionAsync();
	}

	private void RefreshDetectionCommandStates()
	{
		OnPropertyChanged("IsTubeSelectionEnabled");
		CommandManager.InvalidateRequerySuggested();
	}

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
		_workflowEngine.Stop();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "检测已停止：可重新选择采血管数量。";
		AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Detection, "检测已停止。");
	}

	private void EmergencyStop()
	{
		_isDetectionStarted = false;
		RefreshDetectionCommandStates();
		StopTubeCountSync();
		_workflowEngine.Stop();
		_ = SendEmergencyStopSignalToPlcAsync();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "急停已触发：请排查后复位。";
		AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "急停触发，已停止当前动作。");
	}

	private void RunAction(string action, HomeLogSource source, HomeLogKind kind)
	{
		AddLog(HomeLogLevel.Info, source, kind, action);
	}

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

	private void StartAlarmMonitor()
	{
		StopAlarmMonitor();
		_alarmMonitorCts = new CancellationTokenSource();
		_alarmMonitorTask = Task.Run(() => AlarmMonitorLoopAsync(_alarmMonitorCts.Token));
	}

	private void StopAlarmMonitor()
	{
		_alarmMonitorCts?.Cancel();
		_alarmMonitorCts?.Dispose();
		_alarmMonitorCts = null;
		_alarmMonitorTask = null;
	}

	private void StartProcessModeMonitor()
	{
		StopProcessModeMonitor();
		_processModeMonitorCts = new CancellationTokenSource();
		_processModeMonitorTask = Task.Run(() => ProcessModeMonitorLoopAsync(_processModeMonitorCts.Token));
	}

	private void StopProcessModeMonitor()
	{
		_processModeMonitorCts?.Cancel();
		_processModeMonitorCts?.Dispose();
		_processModeMonitorCts = null;
		_processModeMonitorTask = null;
	}

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

	private void AutoStopDetectionByAlarm()
	{
		_isDetectionStarted = false;
		RefreshDetectionCommandStates();
		StopTubeCountSync();
		_workflowEngine.Stop();
		_ = SendStopSignalToPlcAsync();
		CountRuleText = "报警触发：检测已自动停止，请排查后复位。";
		AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Detection, "检测过程中报警汇总(M2=1)，已自动停止检测。");
	}

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

	private async Task<bool> ReadCoilStateWithLockAsync(ushort address, CancellationToken token = default(CancellationToken))
	{
		var read = await TryReadCoilStateWithLockAsync(address, token);
		if (!read.Success)
		{
			throw new InvalidOperationException(read.Error);
		}

		return read.Value;
	}

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

	private static bool IsOnUiThread()
	{
		Application current = Application.Current;
		Dispatcher? dispatcher = current?.Dispatcher;
		return dispatcher == null || dispatcher.CheckAccess();
	}

	private void StartTubeCountSync()
	{
		StopTubeCountSync();
		_tubeCountSyncCts = new CancellationTokenSource();
		_tubeCountSyncTask = Task.Run(() => SyncTubeCountLoopAsync(_tubeCountSyncCts.Token));
	}

	private void StopTubeCountSync()
	{
		_tubeCountSyncCts?.Cancel();
		_tubeCountSyncCts?.Dispose();
		_tubeCountSyncCts = null;
		_tubeCountSyncTask = null;
	}

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

	private async Task<bool> SendInitSignalToPlcAsync()
	{
		try
		{
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

	private void OnWorkflowLogGenerated(WorkflowEngine.WorkflowLogMessage log)
	{
		HomeLogLevel level = ParseHomeLogLevel(log.LevelText);
		HomeLogKind kind = ParseHomeLogKind(log.LogKind);
		AddLog(level, HomeLogSource.Process, kind, "流程：" + log.Message);
	}

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

	private static HomeLogKind ParseHomeLogKind(string kindText)
	{
		if (!string.IsNullOrWhiteSpace(kindText) && (kindText.Contains("检测") || kindText.Contains("妫€娴")))
		{
			return HomeLogKind.Detection;
		}
		return HomeLogKind.Operation;
	}

	private void RecalculateCounters()
	{
		InfoCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Info);
		WarningCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Warning);
		ErrorCount = _allLogs.Count((HomeLogItemViewModel x) => x.Level == HomeLogLevel.Error);
	}

	private void RefreshVisibleLogs()
	{
		List<HomeLogItemViewModel> list = _allLogs.Where(IsVisible).ToList();
		VisibleLogs.Clear();
		foreach (HomeLogItemViewModel item in list)
		{
			VisibleLogs.Add(item);
		}
	}

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

	private void UpdateRackVisuals()
	{
		foreach (RackSlotItemViewModel tubeRackSlot in TubeRackSlots)
		{
			bool flag = tubeRackSlot.Number <= _selectedTubeCount;
			tubeRackSlot.Fill = (flag ? ActiveSlotFill : IdleSlotFill);
			tubeRackSlot.Foreground = (flag ? ActiveSlotText : IdleSlotText);
		}
		foreach (RackSlotItemViewModel headspaceRackSlot in HeadspaceRackSlots)
		{
			bool flag2 = headspaceRackSlot.Number <= _selectedHeadspaceCount;
			headspaceRackSlot.Fill = (flag2 ? ActiveSlotFill : IdleSlotFill);
			headspaceRackSlot.Foreground = (flag2 ? ActiveSlotText : IdleSlotText);
		}
	}

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

	private void BuildDefaultLogs()
	{
		_allLogs.Clear();
		AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "系统启动完成，等待任务。", null, persistToFile: false);
		AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Operation, "请点击采血管架设定检测总数。", null, persistToFile: false);
	}

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
		UnregisterCorePlcPollingPoints();
	}

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

	public string TubeText => TubeIndex > 0 ? TubeIndex.ToString() : "-";
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

