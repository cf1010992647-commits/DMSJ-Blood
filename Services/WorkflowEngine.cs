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
	public sealed class WorkflowLogMessage
	{
		public DateTime Timestamp { get; init; } = DateTime.Now;

		public string Message { get; init; } = string.Empty;

		public string LevelText { get; init; } = "信息";

		public string LogKind { get; init; } = "普通操作日志";

		public int TubeIndex { get; init; }

		public string ScanCode { get; init; } = string.Empty;

		public string WeightStepKey { get; init; } = string.Empty;

		public double? MeasuredWeight { get; init; }
	}

	private const string WorkflowSignalConfigFileName = "WorkflowSignalConfig.json";

	private const string ParameterConfigFileName = "ProcessParameterConfig.json";

	private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";

	private readonly ScannerProtocolService _scanner = new ScannerProtocolService();

	private readonly LogTool _logTool = LogTool.Shared;

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

	private readonly Dictionary<ushort, ushort> _lastShakeValue = new Dictionary<ushort, ushort>();

	private Lx5vPlc? _plc;

	private CancellationTokenSource? _cts;

	private Task? _workerTask;

	private volatile bool _isRunning;

	private DateTime _lastConfigReload = DateTime.MinValue;

	private WorkflowSignalConfig _signals = new WorkflowSignalConfig();

	private ProcessParameterConfig _processParameters = new ProcessParameterConfig();

	private WeightToZCalibrationConfig _weightToZ = new WeightToZCalibrationConfig();

	private int _tubeSequence;

	private int _currentTubeIndex;

	private string _currentScanCode = string.Empty;

	public int CurrentStep { get; private set; }

	public bool IsRunning => _isRunning;

	public event Action<WorkflowLogMessage>? OnLogGenerated;

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
			ReloadRuntimeConfig(force: true);
			PersistRuntimeConfig();
				_lastCoilState.Clear();
				_lastShakeValue.Clear();
				_tubeSequence = 0;
				_currentTubeIndex = 0;
				_currentScanCode = string.Empty;
				_cts = new CancellationTokenSource();
			_workerTask = Task.Run(() => MonitorEventsLoopAsync(_cts.Token));
			_isRunning = true;
			WriteWorkflowLog("流程状态机已启动（并发事件驱动，OK位读取确认）。");
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
		if (!_isRunning)
		{
			return;
		}
		try
		{
			_cts?.Cancel();
		}
		catch
		{
		}
		finally
		{
			_isRunning = false;
		}
		WriteWorkflowLog("流程状态机已停止。");
	}

	/// <summary>
	/// 执行流程主监控循环，轮询事件并调度处理器。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回流程监控异步任务。</returns>
	/// <remarks>
	/// 由 Start 创建的后台任务调用；循环内依次调用 PollRisingEdgeAndDispatchAsync 和 PollShakeProgressAsync。
	/// </remarks>
	private async Task MonitorEventsLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				EnsurePlcReady();
				ReloadRuntimeConfig(force: false);
				await PollRisingEdgeAndDispatchAsync(token);
				await PollShakeProgressAsync(token);
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
		await DetectRisingAndHandleAsync(_signals.AllowHs1PlaceWeightCoil, "hs1_place_weight", (CancellationToken t) => HandleWeightFlowAsync(10, 11, _signals.AllowHs1PlaceWeightCoil, _signals.Hs1PlaceWeightOkCoil, _signals.Hs1PlaceWeightRegister, "顶空1放置", "hs1_place_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2PlaceWeightCoil, "hs2_place_weight", (CancellationToken t) => HandleWeightFlowAsync(12, 13, _signals.AllowHs2PlaceWeightCoil, _signals.Hs2PlaceWeightOkCoil, _signals.Hs2PlaceWeightRegister, "顶空2放置", "hs2_place_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubePlaceWeightCoil, "tube_place_weight", (CancellationToken t) => HandleWeightFlowAsync(14, 16, _signals.AllowTubePlaceWeightCoil, _signals.TubePlaceWeightOkCoil, _signals.TubePlaceWeightRegister, "采血管放置", "tube_place_weight", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubeAfterAspirateWeightCoil, "tube_after_aspirate_weight", (CancellationToken t) => HandleWeightFlowAsync(17, 19, _signals.AllowTubeAfterAspirateWeightCoil, _signals.TubeAfterAspirateWeightOkCoil, _signals.TubeAfterAspirateWeightRegister, "采血管吸液后", "tube_after_aspirate_weight", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterBloodWeightCoil, "hs1_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(20, 21, _signals.AllowHs1AfterBloodWeightCoil, _signals.Hs1AfterBloodWeightOkCoil, _signals.Hs1AfterBloodWeightRegister, "顶空1加血液后", "hs1_after_blood_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterBloodWeightCoil, "hs2_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(22, 23, _signals.AllowHs2AfterBloodWeightCoil, _signals.Hs2AfterBloodWeightOkCoil, _signals.Hs2AfterBloodWeightRegister, "顶空2加血液后", "hs2_after_blood_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterButanolWeightCoil, "hs1_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(24, 25, _signals.AllowHs1AfterButanolWeightCoil, _signals.Hs1AfterButanolWeightOkCoil, _signals.Hs1AfterButanolWeightRegister, "顶空1加叔丁醇后", "hs1_after_butanol_weight", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterButanolWeightCoil, "hs2_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(26, 27, _signals.AllowHs2AfterButanolWeightCoil, _signals.Hs2AfterButanolWeightOkCoil, _signals.Hs2AfterButanolWeightRegister, "顶空2加叔丁醇后", "hs2_after_butanol_weight", needWeightToZ: false, t), token);
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
	/// 轮询摇匀进度并输出阶段日志。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回摇匀进度轮询异步任务。</returns>
	/// <remarks>
	/// 由 MonitorEventsLoopAsync 在每个轮询周期调用。
	/// </remarks>
	private async Task PollShakeProgressAsync(CancellationToken token)
	{
		await TryLogShakeProgressAsync(_signals.AllowShakeTubeCoil, _signals.ShakeTubeTimeRegister, "步骤7 采血管摇匀", token);
		await TryLogShakeProgressAsync(_signals.AllowShakeHs1Coil, _signals.ShakeHs1TimeRegister, "步骤8 顶空1摇匀", token);
		await TryLogShakeProgressAsync(_signals.AllowShakeHs2Coil, _signals.ShakeHs2TimeRegister, "步骤9 顶空2摇匀", token);
	}

	/// <summary>
	/// 按允许位读取摇匀时长并在变化时写日志。
	/// </summary>
	/// By:ChengLei
	/// <param name="allowCoil">允许运行的触发线圈地址。</param>
	/// <param name="timeRegister">摇匀时长寄存器地址。</param>
	/// <param name="label">日志显示标签。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回摇匀进度记录异步任务。</returns>
	/// <remarks>
	/// 由 PollShakeProgressAsync 分别针对采血管、顶空1、顶空2调用。
	/// </remarks>
	private async Task TryLogShakeProgressAsync(ushort allowCoil, ushort timeRegister, string label, CancellationToken token)
	{
		if (await ReadCoilAsync(allowCoil, token))
		{
			ushort value = await ReadRegisterAsync(timeRegister, token);
			if (!_lastShakeValue.TryGetValue(timeRegister, out var last) || last != value)
			{
				_lastShakeValue[timeRegister] = value;
				WriteWorkflowLog($"{label} 当前时长={value}s", "信息", "检测日志", GetCurrentTubeIndex());
			}
		}
	}

	/// <summary>
	/// 处理扫码流程：扫码、等待扫码OK、清零天平、下发摇匀时长。
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
			WriteWorkflowLog($"步骤3 扫码成功：{code}，映射 {code}A/{code}B", "信息", "检测日志", tubeIndex, _currentScanCode);
			CurrentStep = 4;
			await WaitForCoilTrueAsync(_signals.ScanOkCoil, "扫码OK", token);
			WriteWorkflowLog($"步骤4 扫码OK=1：M{_signals.ScanOkCoil}", "信息", "检测日志", tubeIndex);
			CurrentStep = 5;
			await ZeroBalanceAsync(token);
			WriteWorkflowLog("步骤5 天平清零已执行。", "信息", "检测日志", tubeIndex);
			CurrentStep = 6;
			await WriteRegisterAsync(_signals.ShakeDurationRegister, ClampToUshort(_processParameters.ShakeDurationSeconds), token);
			WriteWorkflowLog($"步骤6 摇匀时长已下发：D{_signals.ShakeDurationRegister}={_processParameters.ShakeDurationSeconds}s", "信息", "检测日志", tubeIndex);
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
	private async Task HandleWeightFlowAsync(int stepReadWeight, int stepWaitOk, ushort allowCoil, ushort okCoil, ushort weightRegister, string stepLabel, string weightStepKey, bool needWeightToZ, CancellationToken token)
	{
		await _weightLock.WaitAsync(token);
		try
		{
			int tubeIndex = GetCurrentTubeIndex();
			CurrentStep = stepReadWeight;
			await WaitForCoilTrueAsync(allowCoil, stepLabel + "允称重", token);
			double weight = await ReadWeightAsync(token);
			if (needWeightToZ)
			{
				int zRaw = ComputeZRawFromWeight(weight);
				WriteWorkflowLog($"步骤{stepReadWeight} {stepLabel}称重={weight:F3}，换算Z={zRaw}（吸液步骤，下发Z坐标）", "信息", "检测日志", tubeIndex, scanCode: null, weightStepKey: weightStepKey, measuredWeight: weight);
				int stepWeightToZ = (CurrentStep = ((stepReadWeight == 14) ? 15 : 18));
				await WriteInt32AtAddressAsync(_signals.ZAbsolutePositionLowRegister, zRaw, token);
				WriteWorkflowLog($"步骤{stepWeightToZ} 重量->Z下发：D{_signals.ZAbsolutePositionLowRegister}/D{_signals.ZAbsolutePositionLowRegister + 1}={zRaw}", "信息", "检测日志", tubeIndex);
				await Task.Delay(100, token);
			}
			else
			{
				WriteWorkflowLog($"步骤{stepReadWeight} {stepLabel}称重={weight:F3}，仅记录显示（不下发PLC，D{weightRegister}仅作配置占位）", "信息", "检测日志", tubeIndex, scanCode: null, weightStepKey: weightStepKey, measuredWeight: weight);
			}
			CurrentStep = stepWaitOk;
			await WaitForCoilTrueAsync(okCoil, stepLabel + "OK", token);
			WriteWorkflowLog($"步骤{stepWaitOk} {stepLabel}OK=1：M{okCoil}", "信息", "检测日志", tubeIndex);
		}
		finally
		{
			_weightLock.Release();
		}
	}

	/// <summary>
	/// 从扫码枪端口读取并解析条码。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回解析后的条码字符串。</returns>
	/// <remarks>
	/// 由 HandleScanFlowAsync 在扫码步骤调用。
	/// </remarks>
	private async Task<string> ReadScanCodeAsync(CancellationToken token)
	{
		int port = CommunicationManager.GetPort("扫码枪");
		EnsureTcpPortConnected(port, "扫码枪");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			byte[] response = await ReceiveOnceWithTimeoutAsync(port, TimeSpan.FromSeconds(8.0), token);
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
		int port = CommunicationManager.GetPort("天平");
		EnsureTcpPortConnected(port, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await CommunicationManager.TcpServer.SendToPort(port, CommunicationManager.Balance.GetZeroCommand());
			try
			{
				_ = await ReceiveOnceWithTimeoutAsync(port, TimeSpan.FromMilliseconds(800.0), token);
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
		int port = CommunicationManager.GetPort("天平");
		EnsureTcpPortConnected(port, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await DrainStaleTcpFramesAsync(port, token);
			await CommunicationManager.TcpServer.SendToPort(port, CommunicationManager.Balance.GetAllCommand());
			byte[] response = await ReceiveValidBalanceAllResponseAsync(port, TimeSpan.FromSeconds(5.0), token);
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
	/// <param name="port">目标TCP端口。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回缓存清理异步任务。</returns>
	/// <remarks>
	/// 由 ReadWeightAsync 在发送读重量命令前调用。
	/// </remarks>
	private async Task DrainStaleTcpFramesAsync(int port, CancellationToken token)
	{
		for (int i = 0; i < 4; i++)
		{
			try
			{
				_ = await ReceiveOnceWithTimeoutAsync(port, TimeSpan.FromMilliseconds(60.0), token);
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
	/// <param name="port">目标TCP端口。</param>
	/// <param name="timeout">超时时间。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回有效天平回包字节数组。</returns>
	/// <remarks>
	/// 由 ReadWeightAsync 调用，用于过滤无效回包。
	/// </remarks>
	private async Task<byte[]> ReceiveValidBalanceAllResponseAsync(int port, TimeSpan timeout, CancellationToken token)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (true)
		{
			TimeSpan remain = deadline - DateTime.UtcNow;
			if (remain <= TimeSpan.Zero)
			{
				throw new TimeoutException($"等待天平重量数据超时（{timeout.TotalSeconds:F0}s）。");
			}

			byte[] response = await ReceiveOnceWithTimeoutAsync(port, remain, token);
			if (IsBalanceAllResponse(response))
			{
				return response;
			}

			WriteWorkflowLog($"天平回包无效，已忽略（len={response.Length}）。", "警告", "检测日志", GetCurrentTubeIndex());
		}
	}

	/// <summary>
	/// 判断回包是否满足天平全量读协议格式。
	/// </summary>
	/// By:ChengLei
	/// <param name="response">待校验的回包字节数组。</param>
	/// <returns>返回是否为有效天平全量回包。</returns>
	/// <remarks>
	/// 由 ReceiveValidBalanceAllResponseAsync 校验每次收到的回包。
	/// </remarks>
	private static bool IsBalanceAllResponse(byte[] response)
	{
		return response.Length >= 13 && response[0] == 1 && response[1] == 3 && response[2] >= 8;
	}

	/// <summary>
	/// 在指定超时时间内从端口接收单帧数据。
	/// </summary>
	/// By:ChengLei
	/// <param name="port">目标TCP端口。</param>
	/// <param name="timeout">超时时间。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回单帧接收结果字节数组。</returns>
	/// <remarks>
	/// 由扫码、天平清零、重量读取流程复用调用。
	/// </remarks>
	private async Task<byte[]> ReceiveOnceWithTimeoutAsync(int port, TimeSpan timeout, CancellationToken token)
	{
		using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
		timeoutCts.CancelAfter(timeout);
		try
		{
			return await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port, timeoutCts.Token);
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
	/// 写入单个PLC保持寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">PLC地址。</param>
	/// <param name="value">待写入或待转换的数值。</param>
	/// <param name="token">取消令牌，用于外部终止当前异步流程。</param>
	/// <returns>返回寄存器写入异步任务。</returns>
	/// <remarks>
	/// 由扫码流程下发摇匀时长时调用。
	/// </remarks>
	private async Task WriteRegisterAsync(ushort address, ushort value, CancellationToken token)
	{
		EnsurePlcReady();
		await _plcLock.WaitAsync(token);
		try
		{
			(bool Success, string Error) write = await _plc!.TryWriteSingleRegisterAsync(address, value);
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
	/// 按刷新窗口重载运行时配置。
	/// </summary>
	/// By:ChengLei
	/// <param name="force">是否强制重载配置。</param>
	/// <remarks>
	/// 由 Start 强制调用一次，随后由 MonitorEventsLoopAsync 周期调用。
	/// </remarks>
	private void ReloadRuntimeConfig(bool force)
	{
		if (force || !((DateTime.Now - _lastConfigReload).TotalSeconds < 2.0))
		{
			_signals = _workflowSignalConfigService.Load() ?? new WorkflowSignalConfig();
			_processParameters = _processParameterConfigService.Load() ?? new ProcessParameterConfig();
			_weightToZ = _weightToZConfigService.Load() ?? new WeightToZCalibrationConfig();
			_lastConfigReload = DateTime.Now;
		}
	}

	/// <summary>
	/// 持久化当前运行时配置到本地文件。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 Start 启动时调用，用于确保配置文件存在并落盘。
	/// </remarks>
	private void PersistRuntimeConfig()
	{
		_workflowSignalConfigService.Save(_signals);
		_processParameterConfigService.Save(_processParameters);
		_weightToZConfigService.Save(_weightToZ);
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
	/// 将数值裁剪并四舍五入为ushort。
	/// </summary>
	/// By:ChengLei
	/// <param name="value">待写入或待转换的数值。</param>
	/// <returns>返回裁剪后的ushort值。</returns>
	/// <remarks>
	/// 由 HandleScanFlowAsync 下发摇匀时长前调用。
	/// </remarks>
	private static ushort ClampToUshort(double value)
	{
		if (value <= 0.0)
		{
			return 0;
		}
		if (value >= 65535.0)
		{
			return ushort.MaxValue;
		}
		return (ushort)Math.Round(value, MidpointRounding.AwayFromZero);
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
	/// 校验指定设备端口是否存在TCP连接。
	/// </summary>
	/// By:ChengLei
	/// <param name="port">目标TCP端口。</param>
	/// <param name="deviceName">设备名称，用于异常提示。</param>
	/// <remarks>
	/// 由扫码和天平通信方法调用，统一校验连接状态。
	/// </remarks>
	private static void EnsureTcpPortConnected(int port, string deviceName)
	{
		if (!CommunicationManager.TcpServer.GetConnectedPorts().Contains(port))
		{
			throw new InvalidOperationException($"{deviceName} TCP客户端未连接（端口 {port}）。");
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
	/// <remarks>
	/// 由流程各步骤调用，并通过 OnLogGenerated 推送到首页日志。
	/// </remarks>
	private void WriteWorkflowLog(string message, string levelText = "信息", string logKind = "普通操作日志", int? tubeIndex = null, string? scanCode = null, string? weightStepKey = null, double? measuredWeight = null)
	{
		DateTime now = DateTime.Now;
		string batchNo = now.ToString("yyyyMMdd");
		int num = NormalizeTubeIndex(tubeIndex);
		string text = string.IsNullOrWhiteSpace(scanCode) ? _currentScanCode : scanCode.Trim();
		_logTool.WriteLog("WorkflowEngine", logKind, levelText, message, batchNo, 0, num, now);
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
				MeasuredWeight = measuredWeight
			});
		}
		catch
		{
		}
		Console.WriteLine(message);
	}
}
