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

	private readonly SemaphoreSlim _tcpReceiveLock = new SemaphoreSlim(1, 1);

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

	public int CurrentStep { get; private set; }

	public bool IsRunning => _isRunning;

	public event Action<WorkflowLogMessage>? OnLogGenerated;

	public void Start()
	{
		if (!_isRunning)
		{
			_plc = CommunicationManager.Plc;
			ReloadRuntimeConfig(force: true);
			PersistRuntimeConfig();
			_lastCoilState.Clear();
			_lastShakeValue.Clear();
			_cts = new CancellationTokenSource();
			_workerTask = Task.Run(() => MonitorEventsLoopAsync(_cts.Token));
			_isRunning = true;
			WriteWorkflowLog("流程状态机已启动（并发事件驱动，OK位读取确认）。");
		}
	}

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

	private async Task PollRisingEdgeAndDispatchAsync(CancellationToken token)
	{
		await DetectRisingAndHandleAsync(_signals.AllowScanCoil, "scan", HandleScanFlowAsync, token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1PlaceWeightCoil, "hs1_place_weight", (CancellationToken t) => HandleWeightFlowAsync(10, 11, _signals.AllowHs1PlaceWeightCoil, _signals.Hs1PlaceWeightOkCoil, _signals.Hs1PlaceWeightRegister, "顶空1放置", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2PlaceWeightCoil, "hs2_place_weight", (CancellationToken t) => HandleWeightFlowAsync(12, 13, _signals.AllowHs2PlaceWeightCoil, _signals.Hs2PlaceWeightOkCoil, _signals.Hs2PlaceWeightRegister, "顶空2放置", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubePlaceWeightCoil, "tube_place_weight", (CancellationToken t) => HandleWeightFlowAsync(14, 16, _signals.AllowTubePlaceWeightCoil, _signals.TubePlaceWeightOkCoil, _signals.TubePlaceWeightRegister, "采血管放置", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowTubeAfterAspirateWeightCoil, "tube_after_aspirate_weight", (CancellationToken t) => HandleWeightFlowAsync(17, 19, _signals.AllowTubeAfterAspirateWeightCoil, _signals.TubeAfterAspirateWeightOkCoil, _signals.TubeAfterAspirateWeightRegister, "采血管吸液后", needWeightToZ: true, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterBloodWeightCoil, "hs1_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(20, 21, _signals.AllowHs1AfterBloodWeightCoil, _signals.Hs1AfterBloodWeightOkCoil, _signals.Hs1AfterBloodWeightRegister, "顶空1加血液后", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterBloodWeightCoil, "hs2_after_blood_weight", (CancellationToken t) => HandleWeightFlowAsync(22, 23, _signals.AllowHs2AfterBloodWeightCoil, _signals.Hs2AfterBloodWeightOkCoil, _signals.Hs2AfterBloodWeightRegister, "顶空2加血液后", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs1AfterButanolWeightCoil, "hs1_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(24, 25, _signals.AllowHs1AfterButanolWeightCoil, _signals.Hs1AfterButanolWeightOkCoil, _signals.Hs1AfterButanolWeightRegister, "顶空1加叔丁醇后", needWeightToZ: false, t), token);
		await DetectRisingAndHandleAsync(_signals.AllowHs2AfterButanolWeightCoil, "hs2_after_butanol_weight", (CancellationToken t) => HandleWeightFlowAsync(26, 27, _signals.AllowHs2AfterButanolWeightCoil, _signals.Hs2AfterButanolWeightOkCoil, _signals.Hs2AfterButanolWeightRegister, "顶空2加叔丁醇后", needWeightToZ: false, t), token);
	}

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
			WriteWorkflowLog(handlerKey + "处理失败：" + ex3.Message, "错误", "检测日志");
			if (IsWeightToZConfigFatal(ex3))
			{
				WriteWorkflowLog("检测到重量->Z标定系数无效，流程已自动停止。", "错误", "检测日志");
				Stop();
			}
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task PollShakeProgressAsync(CancellationToken token)
	{
		await TryLogShakeProgressAsync(_signals.AllowShakeTubeCoil, _signals.ShakeTubeTimeRegister, "步骤7 采血管摇匀", token);
		await TryLogShakeProgressAsync(_signals.AllowShakeHs1Coil, _signals.ShakeHs1TimeRegister, "步骤8 顶空1摇匀", token);
		await TryLogShakeProgressAsync(_signals.AllowShakeHs2Coil, _signals.ShakeHs2TimeRegister, "步骤9 顶空2摇匀", token);
	}

	private async Task TryLogShakeProgressAsync(ushort allowCoil, ushort timeRegister, string label, CancellationToken token)
	{
		if (await ReadCoilAsync(allowCoil, token))
		{
			ushort value = await ReadRegisterAsync(timeRegister, token);
			if (!_lastShakeValue.TryGetValue(timeRegister, out var last) || last != value)
			{
				_lastShakeValue[timeRegister] = value;
				WriteWorkflowLog($"{label} 当前时长={value}s", "信息", "检测日志");
			}
		}
	}

	private async Task HandleScanFlowAsync(CancellationToken token)
	{
		await _scanLock.WaitAsync(token);
		try
		{
			CurrentStep = 3;
			string code = await ReadScanCodeAsync(token);
			if (string.IsNullOrWhiteSpace(code))
			{
				code = $"UNKNOWN_{DateTime.Now:HHmmss}";
			}
			WriteWorkflowLog($"步骤3 扫码成功：{code}，映射 {code}A/{code}B", "信息", "检测日志");
			CurrentStep = 4;
			await WaitForCoilTrueAsync(_signals.ScanOkCoil, "扫码OK", token);
			WriteWorkflowLog($"步骤4 扫码OK=1：M{_signals.ScanOkCoil}", "信息", "检测日志");
			CurrentStep = 5;
			await ZeroBalanceAsync(token);
			WriteWorkflowLog("步骤5 天平清零已执行。", "信息", "检测日志");
			CurrentStep = 6;
			await WriteRegisterAsync(_signals.ShakeDurationRegister, ClampToUshort(_processParameters.ShakeDurationSeconds), token);
			WriteWorkflowLog($"步骤6 摇匀时长已下发：D{_signals.ShakeDurationRegister}={_processParameters.ShakeDurationSeconds}s", "信息", "检测日志");
		}
		finally
		{
			_scanLock.Release();
		}
	}

	private async Task HandleWeightFlowAsync(int stepReadWeight, int stepWaitOk, ushort allowCoil, ushort okCoil, ushort weightRegister, string stepLabel, bool needWeightToZ, CancellationToken token)
	{
		await _weightLock.WaitAsync(token);
		try
		{
			CurrentStep = stepReadWeight;
			await WaitForCoilTrueAsync(allowCoil, stepLabel + "允称重", token);
			double weight = await ReadWeightAsync(token);
			if (needWeightToZ)
			{
				int zRaw = ComputeZRawFromWeight(weight);
				WriteWorkflowLog($"步骤{stepReadWeight} {stepLabel}称重={weight:F3}，换算Z={zRaw}（吸液步骤，下发Z坐标）", "信息", "检测日志");
				int stepWeightToZ = (CurrentStep = ((stepReadWeight == 14) ? 15 : 18));
				await WriteInt32AtAddressAsync(_signals.ZAbsolutePositionLowRegister, zRaw, token);
				WriteWorkflowLog($"步骤{stepWeightToZ} 重量->Z下发：D{_signals.ZAbsolutePositionLowRegister}/D{_signals.ZAbsolutePositionLowRegister + 1}={zRaw}", "信息", "检测日志");
				await Task.Delay(100, token);
			}
			else
			{
				WriteWorkflowLog($"步骤{stepReadWeight} {stepLabel}称重={weight:F3}，仅记录显示（不下发PLC，D{weightRegister}仅作配置占位）", "信息", "检测日志");
			}
			CurrentStep = stepWaitOk;
			await WaitForCoilTrueAsync(okCoil, stepLabel + "OK", token);
			WriteWorkflowLog($"步骤{stepWaitOk} {stepLabel}OK=1：M{okCoil}", "信息", "检测日志");
		}
		finally
		{
			_weightLock.Release();
		}
	}

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

	private async Task ZeroBalanceAsync(CancellationToken token)
	{
		int port = CommunicationManager.GetPort("天平");
		EnsureTcpPortConnected(port, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await CommunicationManager.TcpServer.SendToPort(port, CommunicationManager.Balance.GetZeroCommand());
			await Task.Delay(200, token);
		}
		finally
		{
			_tcpReceiveLock.Release();
		}
	}

	private async Task<double> ReadWeightAsync(CancellationToken token)
	{
		int port = CommunicationManager.GetPort("天平");
		EnsureTcpPortConnected(port, "天平");
		await _tcpReceiveLock.WaitAsync(token);
		try
		{
			await CommunicationManager.TcpServer.SendToPort(port, CommunicationManager.Balance.GetAllCommand());
			byte[] response = await ReceiveOnceWithTimeoutAsync(port, TimeSpan.FromSeconds(5.0), token);
			return CommunicationManager.Balance.ReadWeight(response);
		}
		finally
		{
			_tcpReceiveLock.Release();
		}
	}

	private async Task<byte[]> ReceiveOnceWithTimeoutAsync(int port, TimeSpan timeout, CancellationToken token)
	{
		Task<byte[]> receiveTask = CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port, token);
		Task delayTask = Task.Delay(timeout, token);
		if (await Task.WhenAny(receiveTask, delayTask) == receiveTask)
		{
			return await receiveTask;
		}
		throw new TimeoutException($"TCP接收超时（{timeout.TotalSeconds:F0}s）。");
	}

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

	private void PersistRuntimeConfig()
	{
		_workflowSignalConfigService.Save(_signals);
		_processParameterConfigService.Save(_processParameters);
		_weightToZConfigService.Save(_weightToZ);
	}

	private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
	{
		lowWord = (ushort)(value & 0xFFFF);
		highWord = (ushort)((value >>> 16) & 0xFFFF);
	}

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

	private void EnsurePlcReady()
	{
		if (_plc == null)
		{
			throw new InvalidOperationException("PLC未初始化。");
		}
	}

	private static void EnsureTcpPortConnected(int port, string deviceName)
	{
		if (!CommunicationManager.TcpServer.GetConnectedPorts().Contains(port))
		{
			throw new InvalidOperationException($"{deviceName} TCP客户端未连接（端口 {port}）。");
		}
	}

	private void WriteWorkflowLog(string message, string levelText = "信息", string logKind = "普通操作日志")
	{
		DateTime now = DateTime.Now;
		string batchNo = now.ToString("yyyyMMdd");
		_logTool.WriteLog("WorkflowEngine", logKind, levelText, message, batchNo);
		try
		{
			this.OnLogGenerated?.Invoke(new WorkflowLogMessage
			{
				Timestamp = now,
				Message = message,
				LevelText = levelText,
				LogKind = logKind
			});
		}
		catch
		{
		}
		Console.WriteLine(message);
	}
}
