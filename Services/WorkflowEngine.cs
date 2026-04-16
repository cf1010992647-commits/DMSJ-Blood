using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Blood_Alcohol.Communication.Protocols;
using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Logs;
using Blood_Alcohol.Models;

namespace Blood_Alcohol.Services;

public class WorkflowEngine
{
	/// <summary>
	/// 流程日志结构化消息模型
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由流程引擎推送给首页 用于界面展示和管号轨迹 CSV 追加
	/// </remarks>
	public sealed class WorkflowLogMessage
	{
		/// <summary>
		/// 日志时间戳
		/// </summary>
		/// By:ChengLei
		public DateTime Timestamp { get; init; } = DateTime.Now;

		/// <summary>
		/// 日志消息正文
		/// </summary>
		/// By:ChengLei
		public string Message { get; init; } = string.Empty;

		/// <summary>
		/// 日志级别文本
		/// </summary>
		/// By:ChengLei
		public string LevelText { get; init; } = "信息";

		/// <summary>
		/// 日志分类文本
		/// </summary>
		/// By:ChengLei
		public string LogKind { get; init; } = "普通操作日志";

		/// <summary>
		/// 采血管号 0 表示普通流程日志
		/// </summary>
		/// By:ChengLei
		public int TubeIndex { get; init; }

		/// <summary>
		/// 当前关联扫码值
		/// </summary>
		/// By:ChengLei
		public string ScanCode { get; init; } = string.Empty;

		/// <summary>
		/// 称重步骤键
		/// </summary>
		/// By:ChengLei
		public string WeightStepKey { get; init; } = string.Empty;

		/// <summary>
		/// 称重值 克
		/// </summary>
		/// By:ChengLei
		public double? MeasuredWeight { get; init; }

		/// <summary>
		/// 批次号文本
		/// </summary>
		/// By:ChengLei
		public string BatchNo { get; init; } = string.Empty;

		/// <summary>
		/// 顶空瓶标识 A或B
		/// </summary>
		/// By:ChengLei
		public string HeadspaceBottleTag { get; init; } = string.Empty;

		/// <summary>
		/// 工序名称
		/// </summary>
		/// By:ChengLei
		public string ProcessName { get; init; } = string.Empty;

		/// <summary>
		/// 事件名称
		/// </summary>
		/// By:ChengLei
		public string EventName { get; init; } = string.Empty;

		/// <summary>
		/// PLC 值文本
		/// </summary>
		/// By:ChengLei
		public string PlcValue { get; init; } = string.Empty;

		/// <summary>
		/// 持续时长 秒
		/// </summary>
		/// By:ChengLei
		public double? DurationSeconds { get; init; }
	}

	private const string WorkflowSignalConfigFileName = "WorkflowSignalConfig.json";

	private const string ParameterConfigFileName = "ProcessParameterConfig.json";

	private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";

	private readonly ScannerProtocolService _scanner = new ScannerProtocolService();

	private LogTool _logTool = LogTool.Shared;

	private readonly ConfigService<WorkflowSignalConfig> _workflowSignalConfigService = new ConfigService<WorkflowSignalConfig>("WorkflowSignalConfig.json");

	private readonly ConfigService<ProcessParameterConfig> _processParameterConfigService = new ConfigService<ProcessParameterConfig>("ProcessParameterConfig.json");

	private readonly ConfigService<WeightToZCalibrationConfig> _weightToZConfigService = new ConfigService<WeightToZCalibrationConfig>("WeightToZCalibrationConfig.json");

	private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;

	private readonly SemaphoreSlim _tcpReceiveLock = CommunicationManager.TcpReceiveLock;

	private readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim _weightLock = new SemaphoreSlim(1, 1);

	private readonly Dictionary<string, SemaphoreSlim> _handlerLocks = new Dictionary<string, SemaphoreSlim>
	{
		["scan"] = new SemaphoreSlim(1, 1),
		["hs1_place_weight"] = new SemaphoreSlim(1, 1),
		["hs2_place_weight"] = new SemaphoreSlim(1, 1),
		["tube_place_weight"] = new SemaphoreSlim(1, 1),
		["tube_after_aspirate_weight"] = new SemaphoreSlim(1, 1),
		["hs1_after_blood_weight"] = new SemaphoreSlim(1, 1),
		["hs2_after_blood_weight"] = new SemaphoreSlim(1, 1),
		["hs1_after_butanol_weight"] = new SemaphoreSlim(1, 1),
		["hs2_after_butanol_weight"] = new SemaphoreSlim(1, 1)
	};

	private readonly Dictionary<ushort, bool> _lastCoilState = new Dictionary<ushort, bool>();

	private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

	private Lx5vPlc? _plc;

	private CancellationTokenSource? _cts;

	private Task? _workerTask;

	private volatile bool _isRunning;

	private WorkflowSignalConfig _signals = new WorkflowSignalConfig();

	private ProcessParameterConfig _processParameters = new ProcessParameterConfig();

	private WeightToZCalibrationConfig _weightToZ = new WeightToZCalibrationConfig();

	private int _tubeSequence;

	private int _currentTubeIndex;

	private string _currentScanCode = string.Empty;

	private Func<string?> _batchNoProvider = () => string.Empty;

	public int CurrentStep { get; private set; }

	public bool IsRunning => _isRunning;

	public event Action<WorkflowLogMessage>? OnLogGenerated;

	/// <summary>
	/// 配置流程日志输出目标与批次上下文提供器
	/// </summary>
	/// By:ChengLei
	/// <param name="logTool">日志写入工具实例</param>
	/// <param name="batchNoProvider">批次号提供委托</param>
	/// <remarks>
	/// 由首页在日志目录变更或初始化时调用 用于统一流程日志落盘位置
	/// </remarks>
	public void ConfigureLogOutput(LogTool logTool, Func<string?> batchNoProvider)
	{
		_logTool = logTool ?? LogTool.Shared;
		_batchNoProvider = batchNoProvider ?? (() => string.Empty);
	}

	/// <summary>
	/// 启动流程引擎并初始化运行状态。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页开始流程动作调用；启动后会通过 Task.Run 进入 MonitorEventsLoopAsync。
	/// </remarks>
	public void Start()
	{
		if (!_isRunning)
		{
			_plc = CommunicationManager.Plc;
			WorkflowRuntimeSnapshot snapshot = LoadRuntimeSnapshot();
			List<string> configErrors = ValidateRuntimeSnapshot(snapshot);
			if (configErrors.Count > 0)
			{
				foreach (string error in configErrors)
				{
					WriteWorkflowLog("流程配置非法：" + error, "错误", "检测日志");
				}

				throw new InvalidOperationException("流程配置非法，已阻止检测启动。");
			}

			ApplyRuntimeSnapshot(snapshot);
			PersistRuntimeConfig(snapshot);
			_lastCoilState.Clear();
			_tubeSequence = 0;
			_currentTubeIndex = 0;
			_currentScanCode = string.Empty;
			_cts = new CancellationTokenSource();
			_workerTask = Task.Run(() => MonitorEventsLoopAsync(_cts.Token));
			_isRunning = true;
			WriteWorkflowLog($"流程状态机已启动（配置快照：{snapshot.LoadedAt:yyyy-MM-dd HH:mm:ss}，并发事件驱动，OK位读取确认）。");
		}
	}

	/// <summary>
	/// 停止流程引擎并取消后台监控任务。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页停止流程动作调用，也会在致命配置异常时被内部触发。
	/// </remarks>
	public void Stop()
	{
		StopAsync().GetAwaiter().GetResult();
	}

	/// <summary>
	/// 异步停止流程引擎并等待后台监控任务退出。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于中断停机等待。</param>
	/// <returns>返回异步停机任务。</returns>
	/// <remarks>
	/// 由首页释放和应用退出流程调用，取消后台任务后最多等待限定时间。
	/// </remarks>
	public async Task StopAsync(CancellationToken token = default)
	{
		CancellationTokenSource? cts = _cts;
		Task? workerTask = _workerTask;
		if (!_isRunning && cts == null && workerTask == null)
		{
			return;
		}

		try
		{
			cts?.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}

		_isRunning = false;

		if (workerTask != null && !workerTask.IsCompleted && workerTask.Id != Task.CurrentId)
		{
			await WaitWorkerExitAsync(workerTask, token).ConfigureAwait(false);
		}

		if (ReferenceEquals(_cts, cts))
		{
			_cts = null;
		}

		if (ReferenceEquals(_workerTask, workerTask))
		{
			_workerTask = null;
		}

		cts?.Dispose();
		WriteWorkflowLog("流程状态机已停止。");
	}

	/// <summary>
	/// 等待流程后台任务在限定时间内退出。
	/// </summary>
	/// By:ChengLei
	/// <param name="workerTask">需要等待的后台任务。</param>
	/// <param name="token">取消令牌，用于中断停机等待。</param>
	/// <returns>返回等待任务。</returns>
	/// <remarks>
	/// 由 StopAsync 调用，超时只记录日志并继续执行关闭流程。
	/// </remarks>
	private async Task WaitWorkerExitAsync(Task workerTask, CancellationToken token)
	{
		try
		{
			await workerTask.WaitAsync(StopTimeout, token).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			WriteWorkflowLog($"流程状态机后台任务停止超时（{StopTimeout.TotalSeconds:F0}s）。");
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			WriteWorkflowLog("流程状态机停机等待被取消。");
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			WriteWorkflowLog($"流程状态机后台任务停止异常：{ex.Message}");
		}
	}

	/// <summary>
	/// 执行流程主监控循环，轮询事件并调度处理器。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回流程监控异步任务。</returns>
	/// <remarks>
	/// 由 Start 创建的后台任务调用；循环内依次调用 PollRisingEdgeAndDispatchAsync。
	/// </remarks>
	private async Task MonitorEventsLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				EnsurePlcReady();
				await PollRisingEdgeAndDispatchAsync(token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex2)
			{
				WriteWorkflowLog("流程监控异常：" + ex2.Message, "错误", "检测日志");
			}
			await Task.Delay(100, token);
		}
	}

	/// <summary>
	/// 轮询各触发位并按信号类型分发处理流程。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回事件分发异步任务。</returns>
	/// <remarks>
	/// 由 MonitorEventsLoopAsync 在每个轮询周期调用。
	/// </remarks>
	private async Task PollRisingEdgeAndDispatchAsync(CancellationToken token)
	{
		await DetectRisingAndHandleAsync(_signals.AllowScanCoil, "scan", HandleScanFlowAsync, token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1PlaceWeightCoil, "hs1_place_weight", (CancellationToken t) => HandleWeightFlowAsync(10, 11, _signals.AllowHs1PlaceWeightCoil, _signals.Hs1PlaceWeightOkCoil, "顶空1放置", "hs1_place_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2PlaceWeightCoil, "hs2_place_weight", (CancellationToken t) => HandleWeightFlowAsync(12, 13, _signals.AllowHs2PlaceWeightCoil, _signals.Hs2PlaceWeightOkCoil, "顶空2放置", "hs2_place_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubePlaceWeightCoil, "tube_place_weight", (CancellationToken t) => HandleWeightFlowAsync(14, 16, _signals.AllowTubePlaceWeightCoil, _signals.TubePlaceWeightOkCoil, "采血管放置", "tube_place_weight", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubeAfterAspirateWeightCoil, "tube_after_aspirate_weight", (CancellationToken t) => HandleWeightFlowAsync(17, 19, _signals.AllowTubeAfterAspirateWeightCoil, _signals.TubeAfterAspirateWeightOkCoil,  "采血管吸液后", "tube_after_aspirate_weight", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterBloodWeightCoil, "hs1_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(20, 21, _signals.AllowHs1AfterBloodWeightCoil, _signals.Hs1AfterBloodWeightOkCoil,  "顶空1加血液后", "hs1_after_blood_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterBloodWeightCoil, "hs2_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(22, 23, _signals.AllowHs2AfterBloodWeightCoil, _signals.Hs2AfterBloodWeightOkCoil,  "顶空2加血液后", "hs2_after_blood_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterButanolWeightCoil, "hs1_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(24, 25, _signals.AllowHs1AfterButanolWeightCoil, _signals.Hs1AfterButanolWeightOkCoil,  "顶空1加叔丁醇后", "hs1_after_butanol_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterButanolWeightCoil, "hs2_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(26, 27, _signals.AllowHs2AfterButanolWeightCoil, _signals.Hs2AfterButanolWeightOkCoil,  "顶空2加叔丁醇后", "hs2_after_butanol_weight", needWeightToZ: false, t), token);
	}

	/// <summary>
	/// 检测指定线圈上升沿并触发对应处理器。
	/// </summary>
	/// By:ChengLei
	/// <param name="coilAddress">触发检测的M线圈地址。</param>
	/// <param name="handlerKey">处理器键名，用于并发互斥控制。</param>
	/// <param name="handler">检测到上升沿后执行的异步处理委托。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回上升沿检测异步任务。</returns>
	/// <remarks>
	/// 由 PollRisingEdgeAndDispatchAsync 针对每个触发位调用。
	/// </remarks>
	private async Task DetectRisingAndHandleAsync(ushort coilAddress, string handlerKey, Func<CancellationToken, Task> handler, CancellationToken token)
	{
		bool state = await ReadCoilAsync(coilAddress, token);
		bool old;
		bool last = _lastCoilState.TryGetValue(coilAddress, out old) && old;
		_lastCoilState[coilAddress] = state;
		if (state && !last)
		{
			_ = RunHandlerOnceAsync(handlerKey, handler, token);
		}
	}

	/// <summary>
	/// 在互斥门控下执行单类处理器，避免并发重入。
	/// </summary>
	/// By:ChengLei
	/// <param name="handlerKey">处理器键名，用于并发互斥控制。</param>
	/// <param name="handler">检测到上升沿后执行的异步处理委托。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回处理器执行异步任务。</returns>
	/// <remarks>
	/// 由 DetectRisingAndHandleAsync 在检测到上升沿后调用。
	/// </remarks>
	private async Task RunHandlerOnceAsync(string handlerKey, Func<CancellationToken, Task> handler, CancellationToken token)
	{
		SemaphoreSlim gate = _handlerLocks[handlerKey];
		if (!(await gate.WaitAsync(0, token)))
		{
			return;
		}
		try
		{
			await handler(token);
		}
		catch (OperationCanceledException)
		{
		}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				WriteWorkflowLog(handlerKey + "处理失败：" + ex3.Message, "错误", "检测日志", GetCurrentTubeIndex());
				if (IsWeightToZConfigFatal(ex3))
				{
					WriteWorkflowLog("检测到重量->Z标定系数无效，流程已自动停止。", "错误", "检测日志", GetCurrentTubeIndex());
					Stop();
				}
			}
		finally
		{
			gate.Release();
		}
	}

	/// <summary>
	/// 处理扫码流程：扫码、等待扫码OK、清零天平。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回扫码流程异步任务。</returns>
	/// <remarks>
	/// 由 PollRisingEdgeAndDispatchAsync 监听 AllowScanCoil 上升沿后触发。
	/// </remarks>
	private async Task HandleScanFlowAsync(CancellationToken token)
	{
		await _scanLock.WaitAsync(token);
		try
		{
			int tubeIndex = BeginTubeCycle();
			CurrentStep = 3;
			string code = await ReadScanCodeAsync(token);
			if (string.IsNullOrWhiteSpace(code))
			{
				code = $"UNKNOWN_{DateTime.Now:HHmmss}";
			}
			_currentScanCode = code.Trim();
			WriteWorkflowLog($"步骤3 扫码成功：{code}，映射 {code}A/{code}B", "信息", "检测日志", tubeIndex, _currentScanCode, processName: "扫码", eventName: "扫码成功");
			CurrentStep = 4;
			await WaitForCoilTrueAsync(_signals.ScanOkCoil, "扫码OK", token);
			WriteWorkflowLog($"步骤4 扫码OK=1：M{_signals.ScanOkCoil}", "信息", "检测日志", tubeIndex, processName: "扫码", eventName: "扫码确认", plcValue: $"M{_signals.ScanOkCoil}=1");
			CurrentStep = 5;
			await ZeroBalanceAsync(token);
			WriteWorkflowLog("步骤5 天平清零已执行。", "信息", "检测日志", tubeIndex, processName: "天平清零", eventName: "清零完成");
		}
		finally
		{
			_scanLock.Release();
		}
	}

	/// <summary>
	/// 处理称重流程，并按配置决定是否执行重量转Z坐标下发。
	/// </summary>
	/// By:ChengLei
	/// <param name="stepReadWeight">称重读取对应步骤号。</param>
	/// <param name="stepWaitOk">等待OK确认对应步骤号。</param>
	/// <param name="allowCoil">允许运行的触发线圈地址。</param>
	/// <param name="okCoil">步骤完成确认线圈地址。</param>
	/// <param name="weightRegister">对应重量寄存器地址。</param>
	/// <param name="stepLabel">步骤日志名称。</param>
	/// <param name="weightStepKey">称重步骤标识，用于界面侧识别重量来源。</param>
	/// <param name="needWeightToZ">是否执行重量转Z坐标下发。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回称重流程异步任务。</returns>
	/// <remarks>
	/// 由 PollRisingEdgeAndDispatchAsync 针对各类称重触发位调用。
	/// </remarks>
	private async Task HandleWeightFlowAsync(int stepReadWeight, int stepWaitOk, ushort allowCoil, ushort okCoil, string stepLabel, string weightStepKey, bool needWeightToZ, CancellationToken token)
	{
		await _weightLock.WaitAsync(token);
		try
		{
			int tubeIndex = GetCurrentTubeIndex();
			CurrentStep = stepReadWeight;
			await WaitForCoilTrueAsync(allowCoil, stepLabel + "允称重", token);
			double weight = await ReadWeightAsync(token);
			string headspaceBottleTag = ResolveHeadspaceBottleTag(weightStepKey);
			if (needWeightToZ)
			{
				int zRaw = ComputeZRawFromWeight(weight);
				WriteWorkflowLog($"步骤{stepReadWeight} {stepLabel}称重={weight:F3}，换算Z={zRaw}（吸液步骤，下发Z坐标）", "信息", "检测日志", tubeIndex, scanCode: null, weightStepKey: weightStepKey, measuredWeight: weight, headspaceBottleTag: headspaceBottleTag, processName: stepLabel, eventName: "称重完成", plcValue: weight.ToString("F3"));
				int stepWeightToZ = (CurrentStep = ((stepReadWeight == 14) ? 15 : 18));
				await WriteInt32AtAddressAsync(_signals.ZAbsolutePositionLowRegister, zRaw, token);
				WriteWorkflowLog($"步骤{stepWeightToZ} 重量->Z下发：D{_signals.ZAbsolutePositionLowRegister}/D{_signals.ZAbsolutePositionLowRegister + 1}={zRaw}", "信息", "检测日志", tubeIndex, headspaceBottleTag: headspaceBottleTag, processName: stepLabel, eventName: "Z坐标下发", plcValue: zRaw.ToString());
				await Task.Delay(100, token);
			}
			CurrentStep = stepWaitOk;
			await WaitForCoilTrueAsync(okCoil, stepLabel + "OK", token);
			WriteWorkflowLog($"步骤{stepWaitOk} {stepLabel}OK=1：M{okCoil}", "信息", "检测日志", tubeIndex, headspaceBottleTag: headspaceBottleTag, processName: stepLabel, eventName: "步骤确认", plcValue: $"M{okCoil}=1");
		}
		finally
		{
			_weightLock.Release();
		}
	}

	/// <summary>
	/// 从扫码枪设备读取并解析条码。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回解析后的条码字符串。</returns>
	/// <remarks>
	/// 由 HandleScanFlowAsync 在扫码步骤调用。
	/// </remarks>
	private async Task<string> ReadScanCodeAsync(CancellationToken token)
	{
		string deviceKey = CommunicationManager.GetDeviceKey("扫码枪");
		EnsureTcpDeviceConnected(deviceKey, "扫码枪");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			byte[] response = await ReceiveOnceWithTimeoutAsync(deviceKey, TimeSpan.FromSeconds(8.0), token);
			return _scanner.ParseCode(response).Trim();
		}
		finally
		{
			_tcpReceiveLock.Release();
		}
	}

	/// <summary>
	/// 向天平发送清零命令并尝试读取确认回包。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回天平清零异步任务。</returns>
	/// <remarks>
	/// 由 HandleScanFlowAsync 在扫码确认后调用。
	/// </remarks>
	private async Task ZeroBalanceAsync(CancellationToken token)
	{
		string deviceKey = CommunicationManager.GetDeviceKey("天平");
		EnsureTcpDeviceConnected(deviceKey, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, CommunicationManager.Balance.GetZeroCommand());
			try
			{
				_ = await ReceiveOnceWithTimeoutAsync(deviceKey, TimeSpan.FromMilliseconds(800.0), token);
			}
			catch (TimeoutException)
			{
			}
			await Task.Delay(200, token);
		}
		finally
		{
			_tcpReceiveLock.Release();
		}
	}

	/// <summary>
	/// 读取天平重量并转换为业务数值。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回读取到的重量值。</returns>
	/// <remarks>
	/// 由 HandleWeightFlowAsync 在称重步骤调用。
	/// </remarks>
	private async Task<double> ReadWeightAsync(CancellationToken token)
	{
		string deviceKey = CommunicationManager.GetDeviceKey("天平");
		EnsureTcpDeviceConnected(deviceKey, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await DrainStaleTcpFramesAsync(deviceKey, token);
			await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, CommunicationManager.Balance.GetAllCommand());
			byte[] response = await ReceiveValidBalanceAllResponseAsync(deviceKey, TimeSpan.FromSeconds(5.0), token);
			return CommunicationManager.Balance.ReadWeight(response);
		}
		finally
		{
			_tcpReceiveLock.Release();
		}
	}

	/// <summary>
	/// 清理天平端口历史缓存帧，避免旧包干扰。
	/// </summary>
	/// By:ChengLei
	/// <param name="deviceKey">逻辑设备键。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回缓存清理异步任务。</returns>
	/// <remarks>
	/// 由 ReadWeightAsync 在发送读重量命令前调用。
	/// </remarks>
	private async Task DrainStaleTcpFramesAsync(string deviceKey, CancellationToken token)
	{
		for (int i = 0; i < 4; i++)
		{
			try
			{
				_ = await ReceiveOnceWithTimeoutAsync(deviceKey, TimeSpan.FromMilliseconds(60.0), token);
			}
			catch (TimeoutException)
			{
				break;
			}
		}
	}

	/// <summary>
	/// 循环接收直到拿到有效天平全量回包。
	/// </summary>
	/// By:ChengLei
	/// <param name="deviceKey">逻辑设备键。</param>
	/// <param name="timeout">超时时间。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回有效天平回包字节数组。</returns>
	/// <remarks>
	/// 由 ReadWeightAsync 调用，用于过滤无效回包。
	/// </remarks>
	private async Task<byte[]> ReceiveValidBalanceAllResponseAsync(string deviceKey, TimeSpan timeout, CancellationToken token)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (true)
		{
			TimeSpan remain = deadline - DateTime.UtcNow;
			if (remain <= TimeSpan.Zero)
			{
				throw new TimeoutException($"等待天平重量数据超时（{timeout.TotalSeconds:F0}s）。");
			}

			byte[] response = await ReceiveOnceWithTimeoutAsync(deviceKey, remain, token);
			if (CommunicationManager.Balance.TryValidateAllResponse(response, out string errorMessage))
			{
				return response;
			}

			WriteWorkflowLog($"天平回包无效，已忽略（len={response.Length}，reason={errorMessage}）。", "警告", "检测日志", GetCurrentTubeIndex());
		}
	}

	/// <summary>
	/// 在指定超时时间内从设备接收单帧数据。
	/// </summary>
	/// By:ChengLei
	/// <param name="deviceKey">逻辑设备键。</param>
	/// <param name="timeout">超时时间。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回单帧接收结果字节数组。</returns>
	/// <remarks>
	/// 由扫码、天平清零、重量读取流程复用调用。
	/// </remarks>
	private async Task<byte[]> ReceiveOnceWithTimeoutAsync(string deviceKey, TimeSpan timeout, CancellationToken token)
	{
		using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
		timeoutCts.CancelAfter(timeout);
		try
		{
			return await CommunicationManager.TcpServer.ReceiveOnceFromDeviceAsync(deviceKey, timeoutCts.Token);
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested && timeoutCts.IsCancellationRequested)
		{
			throw new TimeoutException($"TCP接收超时（{timeout.TotalSeconds:F0}s）。");
		}
	}

	/// <summary>
	/// 按标定系数把重量换算为PLC可下发的Z原始值。
	/// </summary>
	/// By:ChengLei
	/// <param name="weight">称重值（克）。</param>
	/// <returns>返回可下发PLC的Z轴原始整型值。</returns>
	/// <remarks>
	/// 由 HandleWeightFlowAsync 在 needWeightToZ=true 时调用。
	/// </remarks>
	private int ComputeZRawFromWeight(double weight)
	{
		if (!_weightToZ.HasCoefficient || Math.Abs(_weightToZ.ZPerWeight) <= 1E-07)
		{
			throw new InvalidOperationException("重量->Z系数无效（未标定或为0），已禁止继续流程。");
		}
		double num = weight * _weightToZ.ZPerWeight;
		double num2 = Math.Round(num * (double)(int)_signals.ZAbsolutePositionScale, MidpointRounding.AwayFromZero);
		if (num2 < -2147483648.0 || num2 > 2147483647.0)
		{
			throw new InvalidOperationException($"Z换算越界：weight={weight:F3}, z={num:F3}, raw={num2:F0}");
		}
		return (int)num2;
	}

	/// <summary>
	/// 判断异常是否属于重量转Z配置致命错误。
	/// </summary>
	/// By:ChengLei
	/// <param name="ex">待判定的异常对象。</param>
	/// <returns>返回是否为致命配置异常。</returns>
	/// <remarks>
	/// 由 RunHandlerOnceAsync 捕获异常后调用。
	/// </remarks>
	private static bool IsWeightToZConfigFatal(Exception ex)
	{
		for (Exception? ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
		{
			if (ex2 is InvalidOperationException && ex2.Message.Contains("重量->Z系数无效", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 等待指定线圈变为真，超时则抛出异常。
	/// </summary>
	/// By:ChengLei
	/// <param name="coilAddress">触发检测的M线圈地址。</param>
	/// <param name="signalName">信号名称，用于超时提示。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回等待完成异步任务。</returns>
	/// <remarks>
	/// 由扫码流程和称重流程在等待OK/允许信号时调用。
	/// </remarks>
	private async Task WaitForCoilTrueAsync(ushort coilAddress, string signalName, CancellationToken token)
	{
		int timeoutSeconds = Math.Max(5, _signals.SignalWaitTimeoutSeconds);
		Stopwatch sw = Stopwatch.StartNew();
		while (!token.IsCancellationRequested && !(await ReadCoilAsync(coilAddress, token)))
		{
			if (sw.Elapsed > TimeSpan.FromSeconds(timeoutSeconds))
			{
				throw new TimeoutException($"等待 {signalName}(M{coilAddress}) 超时（>{timeoutSeconds}s）。");
			}
			await Task.Delay(100, token);
		}
	}

	/// <summary>
	/// 读取单个PLC线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">PLC地址。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回线圈状态值。</returns>
	/// <remarks>
	/// 由上升沿检测、等待信号、摇匀进度读取等流程调用。
	/// </remarks>
	private async Task<bool> ReadCoilAsync(ushort address, CancellationToken token)
	{
		EnsurePlcReady();
		await _plcLock.WaitAsync(token);
		try
		{
			(bool Success, bool[] Values, string Error) read = await _plc!.TryReadCoilsAsync(address, 1);
			if (!read.Success)
			{
				throw new InvalidOperationException(read.Error);
			}
			bool[] states = read.Values;
			return states.Length != 0 && states[0];
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 读取单个PLC保持寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">PLC地址。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回寄存器值。</returns>
	/// <remarks>
	/// 由摇匀进度读取与寄存器取值流程调用。
	/// </remarks>
	private async Task<ushort> ReadRegisterAsync(ushort address, CancellationToken token)
	{
		EnsurePlcReady();
		await _plcLock.WaitAsync(token);
		try
		{
			(bool Success, ushort[] Values, string Error) read = await _plc!.TryReadHoldingRegistersAsync(address, 1);
			if (!read.Success)
			{
				throw new InvalidOperationException(read.Error);
			}
			ushort[] regs = read.Values;
			if (regs.Length == 0)
			{
				throw new InvalidOperationException($"读取 D{address} 失败：返回长度为0。");
			}
			return regs[0];
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 把32位值拆分后写入连续两个寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="lowAddress">32位值低16位寄存器地址。</param>
	/// <param name="value">待写入或待转换的数值。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回32位值写入异步任务。</returns>
	/// <remarks>
	/// 由称重流程下发重量转Z结果时调用。
	/// </remarks>
	private async Task WriteInt32AtAddressAsync(ushort lowAddress, int value, CancellationToken token)
	{
		SplitInt32(value, out var lowWord, out var highWord);
		EnsurePlcReady();
		await _plcLock.WaitAsync(token);
		try
		{
			(bool Success, string Error) writeLow = await _plc!.TryWriteSingleRegisterAsync(lowAddress, lowWord);
			if (!writeLow.Success)
			{
				throw new InvalidOperationException(writeLow.Error);
			}
			(bool Success, string Error) writeHigh = await _plc!.TryWriteSingleRegisterAsync((ushort)(lowAddress + 1), highWord);
			if (!writeHigh.Success)
			{
				throw new InvalidOperationException(writeHigh.Error);
			}
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 加载流程运行时配置快照。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前批次使用的运行时配置快照。</returns>
	/// <remarks>
	/// 由 Start 调用一次，运行中不再重载配置，修改会在下一批 Start 生效。
	/// </remarks>
	private WorkflowRuntimeSnapshot LoadRuntimeSnapshot()
	{
		return new WorkflowRuntimeSnapshot(
			_workflowSignalConfigService.Load() ?? new WorkflowSignalConfig(),
			_processParameterConfigService.Load() ?? new ProcessParameterConfig(),
			_weightToZConfigService.Load() ?? new WeightToZCalibrationConfig(),
			DateTime.Now);
	}

	/// <summary>
	/// 校验当前批次运行时配置快照。
	/// </summary>
	/// <param name="snapshot">当前批次使用的运行时配置快照。</param>
	/// <returns>返回配置错误列表，列表为空表示校验通过。</returns>
	private static List<string> ValidateRuntimeSnapshot(WorkflowRuntimeSnapshot snapshot)
	{
		var errors = new List<string>();

		AddPrefixedValidationErrors(errors, "流程信号配置", snapshot.Signals.Validate());
		AddPrefixedValidationErrors(errors, "工艺参数配置", snapshot.Parameters.Validate());
		if (!CommunicationManager.ValidateCurrentSettingsAndLog())
		{
			AddPrefixedValidationErrors(errors, "通信配置", CommunicationManager.ConfigurationErrors);
		}

		return errors;
	}

	/// <summary>
	/// 添加带配置名称前缀的校验错误。
	/// </summary>
	/// <param name="target">目标错误列表。</param>
	/// <param name="prefix">配置名称前缀。</param>
	/// <param name="errors">原始错误列表。</param>
	private static void AddPrefixedValidationErrors(
		List<string> target,
		string prefix,
		IEnumerable<string> errors)
	{
		foreach (string error in errors)
		{
			target.Add($"{prefix}：{error}");
		}
	}

	/// <summary>
	/// 应用流程运行时配置快照。
	/// </summary>
	/// By:ChengLei
	/// <param name="snapshot">当前批次使用的运行时配置快照。</param>
	/// <remarks>
	/// 由 Start 调用，把快照写入字段供本批次后续流程固定使用。
	/// </remarks>
	private void ApplyRuntimeSnapshot(WorkflowRuntimeSnapshot snapshot)
	{
		_signals = snapshot.Signals;
		_processParameters = snapshot.Parameters;
		_weightToZ = snapshot.WeightToZ;
	}

	/// <summary>
	/// 持久化当前运行时配置到本地文件。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 Start 启动时调用，用于确保当前快照对应的配置文件存在并落盘。
	/// </remarks>
	private void PersistRuntimeConfig(WorkflowRuntimeSnapshot snapshot)
	{
		_workflowSignalConfigService.Save(snapshot.Signals);
		_processParameterConfigService.Save(snapshot.Parameters);
		_weightToZConfigService.Save(snapshot.WeightToZ);
	}

	/// <summary>
	/// 将Int32拆分为低16位和高16位。
	/// </summary>
	/// By:ChengLei
	/// <param name="value">待写入或待转换的数值。</param>
	/// <param name="lowWord">输出低16位。</param>
	/// <param name="highWord">输出高16位。</param>
	/// <remarks>
	/// 由 WriteInt32AtAddressAsync 调用。
	/// </remarks>
	private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
	{
		lowWord = (ushort)(value & 0xFFFF);
		highWord = (ushort)((value >>> 16) & 0xFFFF);
	}

	/// <summary>
	/// 校验PLC对象是否已就绪。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 PLC读写方法调用，统一做空引用防护。
	/// </remarks>
	private void EnsurePlcReady()
	{
		if (_plc == null)
		{
			throw new InvalidOperationException("PLC未初始化。");
		}
	}

	/// <summary>
	/// 校验指定逻辑设备是否存在TCP连接。
	/// </summary>
	/// By:ChengLei
	/// <param name="deviceKey">逻辑设备键。</param>
	/// <param name="deviceName">设备名称，用于异常提示。</param>
	/// <remarks>
	/// 由扫码和天平通信方法调用，统一校验连接状态。
	/// </remarks>
	private static void EnsureTcpDeviceConnected(string deviceKey, string deviceName)
	{
		if (!CommunicationManager.TcpServer.IsDeviceConnected(deviceKey))
		{
			throw new InvalidOperationException($"{deviceName} TCP客户端未连接（DeviceKey={deviceKey}）。");
		}
	}

	/// <summary>
	/// 开启新采血管处理序号并重置当前扫码值。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回新分配的采血管序号。</returns>
	/// <remarks>
	/// 由 HandleScanFlowAsync 在新管开始时调用。
	/// </remarks>
	private int BeginTubeCycle()
	{
		int num = Interlocked.Increment(ref _tubeSequence);
		Volatile.Write(ref _currentTubeIndex, num);
		_currentScanCode = string.Empty;
		return num;
	}

	/// <summary>
	/// 获取当前流程采血管序号。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前采血管序号。</returns>
	/// <remarks>
	/// 由称重、摇匀和错误日志路径调用。
	/// </remarks>
	private int GetCurrentTubeIndex()
	{
		int num = Volatile.Read(ref _currentTubeIndex);
		return (num > 0) ? num : 0;
	}

	/// <summary>
	/// 将可空管号归一化为有效非负值。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeIndex">可选采血管序号。</param>
	/// <returns>返回归一化后的采血管序号。</returns>
	/// <remarks>
	/// 由 WriteWorkflowLog 统一规范化可空管号。
	/// </remarks>
	private static int NormalizeTubeIndex(int? tubeIndex)
	{
		int num = tubeIndex.GetValueOrDefault();
		return (num > 0) ? num : 0;
	}

	/// <summary>
	/// 根据流程步骤键解析对应的顶空瓶标识
	/// </summary>
	/// By:ChengLei
	/// <param name="weightStepKey">流程步骤键</param>
	/// <returns>返回 A B 或空字符串</returns>
	/// <remarks>
	/// 由称重与摇匀结构化日志组装流程调用
	/// </remarks>
	private static string ResolveHeadspaceBottleTag(string? weightStepKey)
	{
		if (string.IsNullOrWhiteSpace(weightStepKey))
		{
			return string.Empty;
		}

		if (weightStepKey.Contains("hs1", StringComparison.OrdinalIgnoreCase))
		{
			return "A";
		}

		if (weightStepKey.Contains("hs2", StringComparison.OrdinalIgnoreCase))
		{
			return "B";
		}

		return string.Empty;
	}

	/// <summary>
	/// 解析当前流程应使用的批次号文本
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前批次号 未设置时返回占位批次名</returns>
	/// <remarks>
	/// 由流程日志落盘与事件推送统一获取批次上下文时调用
	/// </remarks>
	private string ResolveBatchNo()
	{
		string? batchNo = _batchNoProvider();
		return string.IsNullOrWhiteSpace(batchNo) ? "批次_未开始" : batchNo.Trim();
	}

	/// <summary>
	/// 写入流程日志并推送到界面订阅事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="message">日志正文。</param>
	/// <param name="levelText">日志级别文本。</param>
	/// <param name="logKind">日志分类文本。</param>
	/// <param name="tubeIndex">可选采血管序号。</param>
	/// <param name="scanCode">可选扫码值，空时使用当前流程扫码值。</param>
	/// <param name="weightStepKey">可选称重步骤标识，空值表示非称重日志。</param>
	/// <param name="measuredWeight">可选称重值（g），空值表示非称重日志。</param>
	/// <param name="headspaceBottleTag">可选顶空瓶标识 A或B 空值表示采血管主线。</param>
	/// <param name="processName">可选工序名称。</param>
	/// <param name="eventName">可选事件名称。</param>
	/// <param name="plcValue">可选 PLC 值或关键值文本。</param>
	/// <param name="durationSeconds">可选持续时长 秒。</param>
	/// <remarks>
	/// 由流程各步骤调用，并通过 OnLogGenerated 推送到首页日志。
	/// </remarks>
	private void WriteWorkflowLog(string message, string levelText = "信息", string logKind = "普通操作日志", int? tubeIndex = null, string? scanCode = null, string? weightStepKey = null, double? measuredWeight = null, string? headspaceBottleTag = null, string? processName = null, string? eventName = null, string? plcValue = null, double? durationSeconds = null)
	{
		DateTime now = DateTime.Now;
		string batchNo = ResolveBatchNo();
		int num = NormalizeTubeIndex(tubeIndex);
		string text = string.IsNullOrWhiteSpace(scanCode) ? string.Empty : scanCode.Trim();
		_logTool.WriteLog("WorkflowEngine", logKind, levelText, message, batchNo, num, now);
		try
		{
			this.OnLogGenerated?.Invoke(new WorkflowLogMessage
			{
				Timestamp = now,
				Message = message,
				LevelText = levelText,
				LogKind = logKind,
				TubeIndex = num,
				ScanCode = text,
				WeightStepKey = weightStepKey ?? string.Empty,
				MeasuredWeight = measuredWeight,
				BatchNo = batchNo,
				HeadspaceBottleTag = headspaceBottleTag ?? string.Empty,
				ProcessName = processName ?? string.Empty,
				EventName = eventName ?? string.Empty,
				PlcValue = plcValue ?? string.Empty,
				DurationSeconds = durationSeconds
			});
		}
		catch
		{
		}
		Console.WriteLine(message);
	}
}
