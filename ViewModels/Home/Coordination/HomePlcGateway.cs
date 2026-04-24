using System;
using System.Threading;
using System.Threading.Tasks;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页 PLC 操作网关。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 集中封装首页使用的 PLC 地址、读写锁、线圈脉冲、寄存器写入和初始化参数校验。
/// </remarks>
internal sealed class HomePlcGateway
{
	private const ushort TubeCountRegisterAddress = 230;
	private const ushort InitZDropNeedleRiseSlowSpeedRegisterAddress = 6000;
	private const ushort InitPipetteAspirateDelayRegisterAddress = 6020;
	private const ushort InitPipetteDispenseDelayRegisterAddress = 6021;
	private const ushort InitTubeShakeHomeDelayRegisterAddress = 6022;
	private const ushort InitTubeShakeWorkDelayRegisterAddress = 6023;
	private const ushort InitTubeShakeTargetCountRegisterAddress = 6024;
	private const ushort InitHeadspaceShakeHomeDelayRegisterAddress = 6026;
	private const ushort InitHeadspaceShakeWorkDelayRegisterAddress = 6027;
	private const ushort InitHeadspaceShakeTargetCountRegisterAddress = 6028;
	private const ushort InitButanolAspirateDelayRegisterAddress = 6030;
	private const ushort InitButanolDispenseDelayRegisterAddress = 6031;
	private const ushort InitSampleBottlePressureTimeRegisterAddress = 6040;
	private const ushort InitQuantitativeLoopBalanceTimeRegisterAddress = 6041;
	private const ushort InitInjectionTimeRegisterAddress = 6042;
	private const ushort InitSampleBottlePressurePositionRegisterAddress = 6302;
	private const ushort InitQuantitativeLoopBalancePositionRegisterAddress = 6304;
	private const ushort InitInjectionPositionRegisterAddress = 6306;
	private const ushort InitCommandCoilAddress = 13;
	private const ushort InitDoneCoilAddress = 14;
	private const ushort AutoModeCoilAddress = 10;
	private const ushort StartCommandCoilAddress = 5;
	private const ushort StopCommandCoilAddress = 900;
	private const ushort EmergencyStopCoilAddress = 3;
	private const ushort AlarmSummaryCoilAddress = 2;
	private const ushort StandbyModeCoilAddress = 490;
	private const ushort PressureModeCoilAddress = 491;
	private const ushort ExhaustModeCoilAddress = 492;
	private const ushort InjectionModeCoilAddress = 493;
	private const ushort RackProcessStartRegisterAddress = 233;
	private const ushort RackProcessRegisterCount = 22;
	private readonly SemaphoreSlim _plcLock;
	private readonly TimeSpan _coilCacheMaxAge;

	/// <summary>
	/// 初始化首页 PLC 操作网关。
	/// </summary>
	/// By:ChengLei
	/// <param name="plcLock">PLC 串行访问锁。</param>
	/// <param name="coilCacheMaxAge">线圈缓存最大可接受时间。</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建，复用 CommunicationManager 的 PLC 访问锁。
	/// </remarks>
	public HomePlcGateway(SemaphoreSlim plcLock, TimeSpan coilCacheMaxAge)
	{
		_plcLock = plcLock ?? throw new ArgumentNullException(nameof(plcLock));
		_coilCacheMaxAge = coilCacheMaxAge;
	}

	/// <summary>
	/// 注册首页核心 PLC 轮询点位。
	/// </summary>
	/// By:ChengLei
	/// <param name="alarmPollInterval">报警位轮询周期。</param>
	/// <param name="processModePollInterval">工艺模式位轮询周期。</param>
	/// <remarks>
	/// 由 HomeViewModel 构造流程调用，用于给首页常用点位提供缓存轮询数据。
	/// </remarks>
	public void RegisterCorePollingPoints(TimeSpan alarmPollInterval, TimeSpan processModePollInterval)
	{
		CommunicationManager.PlcPolling.RegisterCoil(AlarmSummaryCoilAddress, alarmPollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(StandbyModeCoilAddress, processModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(PressureModeCoilAddress, processModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(ExhaustModeCoilAddress, processModePollInterval);
		CommunicationManager.PlcPolling.RegisterCoil(InjectionModeCoilAddress, processModePollInterval);
		CommunicationManager.PlcPolling.Start();
	}

	/// <summary>
	/// 注销首页核心 PLC 轮询点位。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 HomeViewModel 释放流程调用，防止页面关闭后仍占用轮询资源。
	/// </remarks>
	public void UnregisterCorePollingPoints()
	{
		CommunicationManager.PlcPolling.UnregisterCoil(AlarmSummaryCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(StandbyModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(PressureModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(ExhaustModeCoilAddress);
		CommunicationManager.PlcPolling.UnregisterCoil(InjectionModeCoilAddress);
	}

	/// <summary>
	/// 写入自动模式线圈。
	/// </summary>
	/// By:ChengLei
	/// <param name="autoMode">是否切换为自动模式。</param>
	/// <returns>返回写入异步任务。</returns>
	/// <remarks>
	/// 由首页模式切换命令调用。
	/// </remarks>
	public Task WriteAutoModeAsync(bool autoMode)
	{
		return WriteCoilAsync(AutoModeCoilAddress, autoMode);
	}

	/// <summary>
	/// 直接读取自动模式线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌。</param>
	/// <returns>返回读取结果。</returns>
	/// <remarks>
	/// 不使用轮询缓存，避免档位刚写入后读到旧值，供档位同步监控循环使用。
	/// </remarks>
	public Task<(bool Success, bool Value, string Error)> TryReadAutoModeDirectAsync(CancellationToken token = default)
	{
		return TryReadCoilDirectAsync(AutoModeCoilAddress, token);
	}

	/// <summary>
	/// 直接读取初始化完成线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化完成位当前状态。</returns>
	/// <remarks>
	/// 由初始化等待流程调用，不使用缓存，确保初始化结果实时。
	/// </remarks>
	public async Task<bool> ReadInitDoneDirectAsync()
	{
		return await ReadCoilDirectAsync(InitDoneCoilAddress).ConfigureAwait(false);
	}

	/// <summary>
	/// 读取报警汇总线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌。</param>
	/// <returns>返回读取结果。</returns>
	/// <remarks>
	/// 由报警监控循环调用，优先使用轮询缓存。
	/// </remarks>
	public Task<(bool Success, bool Value, string Error)> TryReadAlarmSummaryAsync(CancellationToken token)
	{
		return TryReadCoilAsync(AlarmSummaryCoilAddress, token);
	}

	/// <summary>
	/// 读取工艺模式线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌。</param>
	/// <returns>返回待机、压力、排气和进样线圈状态。</returns>
	/// <remarks>
	/// 由工艺模式监控循环调用，保持原有顺序失败短路语义。
	/// </remarks>
	public async Task<(
		(bool Success, bool Value, string Error) Standby,
		(bool Success, bool Value, string Error) Pressure,
		(bool Success, bool Value, string Error) Exhaust,
		(bool Success, bool Value, string Error) Injection)> ReadProcessModeCoilsAsync(CancellationToken token)
	{
		(bool Success, bool Value, string Error) standbyRead = await TryReadCoilAsync(StandbyModeCoilAddress, token).ConfigureAwait(false);
		(bool Success, bool Value, string Error) pressureRead = standbyRead.Success
			? await TryReadCoilAsync(PressureModeCoilAddress, token).ConfigureAwait(false)
			: (Success: false, Value: false, Error: standbyRead.Error);
		(bool Success, bool Value, string Error) exhaustRead = standbyRead.Success && pressureRead.Success
			? await TryReadCoilAsync(ExhaustModeCoilAddress, token).ConfigureAwait(false)
			: (Success: false, Value: false, Error: standbyRead.Success ? pressureRead.Error : standbyRead.Error);
		(bool Success, bool Value, string Error) injectionRead = standbyRead.Success && pressureRead.Success && exhaustRead.Success
			? await TryReadCoilAsync(InjectionModeCoilAddress, token).ConfigureAwait(false)
			: (Success: false, Value: false, Error: !standbyRead.Success ? standbyRead.Error : !pressureRead.Success ? pressureRead.Error : exhaustRead.Error);

		return (standbyRead, pressureRead, exhaustRead, injectionRead);
	}

	/// <summary>
	/// 读取启动前置条件线圈。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回报警、自动模式和初始化完成状态。</returns>
	/// <remarks>
	/// 由开始检测前置校验调用。
	/// </remarks>
	public async Task<(bool Alarm, bool AutoMode, bool InitDone)> ReadStartPreconditionsAsync()
	{
		bool alarm = await ReadCoilAsync(AlarmSummaryCoilAddress).ConfigureAwait(false);
		bool autoMode = await ReadCoilAsync(AutoModeCoilAddress).ConfigureAwait(false);
		bool initDone = await ReadCoilAsync(InitDoneCoilAddress).ConfigureAwait(false);
		return (alarm, autoMode, initDone);
	}

	/// <summary>
	/// 读取料架工序寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="token">取消令牌。</param>
	/// <returns>返回 D233~D254 读取结果。</returns>
	/// <remarks>
	/// 由料架工序监控循环调用。
	/// </remarks>
	public Task<(bool Success, ushort[] Values, string Error)> ReadRackProcessRegistersAsync(CancellationToken token)
	{
		return ReadHoldingRegistersAsync(RackProcessStartRegisterAddress, RackProcessRegisterCount, token);
	}

	/// <summary>
	/// 下发采血管数量到 PLC。
	/// </summary>
	/// By:ChengLei
	/// <param name="selectedTubeCount">当前采血管数量。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回写入异步任务。</returns>
	/// <remarks>
	/// 由首页数量同步循环周期调用。
	/// </remarks>
	public async Task SendTubeCountAsync(int selectedTubeCount, CancellationToken token)
	{
		ushort tubeCount = (ushort)Math.Clamp(selectedTubeCount, 0, 65535);
		await _plcLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(TubeCountRegisterAddress, tubeCount).ConfigureAwait(false);
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
	/// 下发初始化参数并校验回读值。
	/// </summary>
	/// By:ChengLei
	/// <param name="config">流程参数配置。</param>
	/// <returns>返回参数写入异步任务。</returns>
	/// <remarks>
	/// 由初始化命令发送前调用，写入后逐项回读校验。
	/// </remarks>
	public async Task SendInitParametersWithVerifyAsync(ProcessParameterConfig config)
	{
		(ushort Address, int Value, string Name)[] items = BuildInitParameterItems(config);
		await _plcLock.WaitAsync().ConfigureAwait(false);
		try
		{
			foreach ((ushort Address, int Value, string Name) item in items)
			{
				ushort expected = (ushort)Math.Clamp(item.Value, 0, 65535);
				var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(item.Address, expected).ConfigureAwait(false);
				if (!write.Success)
				{
					throw new InvalidOperationException($"D{item.Address} {item.Name} 写入失败：{write.Error}");
				}

				var read = await CommunicationManager.Plc.TryReadHoldingRegistersAsync(item.Address, 1).ConfigureAwait(false);
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
	}

	/// <summary>
	/// 写入初始化命令线圈高电平。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回写入异步任务。</returns>
	/// <remarks>
	/// 由首页初始化流程在参数下发成功后调用。
	/// </remarks>
	public Task SendInitCommandAsync()
	{
		return WriteCoilAsync(InitCommandCoilAddress, true);
	}

	/// <summary>
	/// 发送开始命令脉冲。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回脉冲发送任务。</returns>
	/// <remarks>
	/// 由开始检测前置条件满足后调用。
	/// </remarks>
	public async Task PulseStartCommandAsync()
	{
		await WriteCoilAsync(StartCommandCoilAddress, true).ConfigureAwait(false);
		await Task.Delay(1000).ConfigureAwait(false);
		await WriteCoilAsync(StartCommandCoilAddress, false).ConfigureAwait(false);
	}

	/// <summary>
	/// 确保开始命令线圈为低电平。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回写入异步任务。</returns>
	/// <remarks>
	/// 由开始检测失败和前置条件不满足时调用。
	/// </remarks>
	public Task EnsureStartCommandLowAsync()
	{
		return WriteCoilAsync(StartCommandCoilAddress, false);
	}

	/// <summary>
	/// 发送停止命令脉冲。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回脉冲发送任务。</returns>
	/// <remarks>
	/// 由停止检测和报警自动停机流程调用。
	/// </remarks>
	public Task PulseStopCommandAsync()
	{
		return PulseCoilAsync(StopCommandCoilAddress, TimeSpan.FromMilliseconds(100));
	}

	/// <summary>
	/// 发送急停命令脉冲。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回脉冲发送任务。</returns>
	/// <remarks>
	/// 由急停流程调用。
	/// </remarks>
	public Task PulseEmergencyStopCommandAsync()
	{
		return PulseCoilAsync(EmergencyStopCoilAddress, TimeSpan.FromMilliseconds(100));
	}

	/// <summary>
	/// 读取线圈状态并返回错误信息。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回读取结果。</returns>
	/// <remarks>
	/// 优先使用轮询缓存；缓存中存在新鲜失败状态时直接返回失败，避免额外串口压力。
	/// </remarks>
	private async Task<(bool Success, bool Value, string Error)> TryReadCoilAsync(ushort address, CancellationToken token = default)
	{
		if (CommunicationManager.PlcPolling.TryGetCoil(address, _coilCacheMaxAge, out PlcPollingService.CoilSnapshot cached))
		{
			if (cached.Success)
			{
				return (true, cached.Value, string.Empty);
			}

			return (false, false, string.IsNullOrWhiteSpace(cached.Error) ? "PLC polling failed." : cached.Error);
		}

		await _plcLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			var read = await CommunicationManager.Plc.TryReadCoilsAsync(address, 1).ConfigureAwait(false);
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
	/// 读取线圈状态，失败时抛出异常。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回线圈状态。</returns>
	/// <remarks>
	/// 由需要异常语义的业务流程调用。
	/// </remarks>
	private async Task<bool> ReadCoilAsync(ushort address, CancellationToken token = default)
	{
		var read = await TryReadCoilAsync(address, token).ConfigureAwait(false);
		if (!read.Success)
		{
			throw new InvalidOperationException(read.Error);
		}

		return read.Value;
	}

	/// <summary>
	/// 直接读取线圈状态并返回错误信息。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回读取结果。</returns>
	/// <remarks>
	/// 不使用轮询缓存，适用于档位同步等不能容忍缓存滞后的读取场景。
	/// </remarks>
	private async Task<(bool Success, bool Value, string Error)> TryReadCoilDirectAsync(ushort address, CancellationToken token = default)
	{
		await _plcLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			var read = await CommunicationManager.Plc.TryReadCoilsAsync(address, 1).ConfigureAwait(false);
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
	/// 直接读取线圈状态，失败时抛出异常。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回线圈状态。</returns>
	/// <remarks>
	/// 不使用轮询缓存，适用于初始化完成位和档位同步等需要实时读取的场景。
	/// </remarks>
	private async Task<bool> ReadCoilDirectAsync(ushort address, CancellationToken token = default)
	{
		var read = await TryReadCoilDirectAsync(address, token).ConfigureAwait(false);
		if (!read.Success)
		{
			throw new InvalidOperationException(read.Error);
		}

		return read.Value;
	}

	/// <summary>
	/// 写入线圈状态。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="value">写入值。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回写入任务。</returns>
	/// <remarks>
	/// 所有线圈写入统一经过 PLC 访问锁串行化。
	/// </remarks>
	private async Task WriteCoilAsync(ushort address, bool value, CancellationToken token = default)
	{
		await _plcLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			var write = await CommunicationManager.Plc.TryWriteSingleCoilAsync(address, value).ConfigureAwait(false);
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
	/// 发送线圈脉冲。
	/// </summary>
	/// By:ChengLei
	/// <param name="address">线圈地址。</param>
	/// <param name="pulseWidth">脉冲宽度。</param>
	/// <returns>返回脉冲发送任务。</returns>
	/// <remarks>
	/// 由停止和急停命令复用。
	/// </remarks>
	private async Task PulseCoilAsync(ushort address, TimeSpan pulseWidth)
	{
		await WriteCoilAsync(address, true).ConfigureAwait(false);
		await Task.Delay(pulseWidth).ConfigureAwait(false);
		await WriteCoilAsync(address, false).ConfigureAwait(false);
	}

	/// <summary>
	/// 读取保持寄存器。
	/// </summary>
	/// By:ChengLei
	/// <param name="startAddress">起始寄存器地址。</param>
	/// <param name="length">读取长度。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回读取结果。</returns>
	/// <remarks>
	/// 所有寄存器读取统一经过 PLC 访问锁串行化。
	/// </remarks>
	private async Task<(bool Success, ushort[] Values, string Error)> ReadHoldingRegistersAsync(ushort startAddress, ushort length, CancellationToken token)
	{
		await _plcLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			return await CommunicationManager.Plc.TryReadHoldingRegistersAsync(startAddress, length).ConfigureAwait(false);
		}
		finally
		{
			_plcLock.Release();
		}
	}

	/// <summary>
	/// 构建初始化参数写入项。
	/// </summary>
	/// By:ChengLei
	/// <param name="config">流程参数配置。</param>
	/// <returns>返回寄存器地址、值和名称集合。</returns>
	/// <remarks>
	/// 由初始化参数写入流程调用，集中维护 D6000/D6020 等地址映射。
	/// </remarks>
	private static (ushort Address, int Value, string Name)[] BuildInitParameterItems(ProcessParameterConfig config)
	{
		return new (ushort Address, int Value, string Name)[17]
		{
			(InitZDropNeedleRiseSlowSpeedRegisterAddress, config.ZDropNeedleRiseSlowSpeed, "Z轴_丢枪头_上升慢速速度"),
			(InitPipetteAspirateDelayRegisterAddress, config.PipetteAspirateDelay100ms, "移液枪吸液延时时间"),
			(InitPipetteDispenseDelayRegisterAddress, config.PipetteDispenseDelay100ms, "移液枪打液延时时间"),
			(InitTubeShakeHomeDelayRegisterAddress, config.TubeShakeHomeDelay100ms, "采血管摇晃原位延时时间"),
			(InitTubeShakeWorkDelayRegisterAddress, config.TubeShakeWorkDelay100ms, "采血管摇晃工位延时时间"),
			(InitTubeShakeTargetCountRegisterAddress, config.TubeShakeTargetCount, "采血管摇晃目标次数"),
			(InitHeadspaceShakeHomeDelayRegisterAddress, config.HeadspaceShakeHomeDelay100ms, "顶空瓶摇晃原位延时时间"),
			(InitHeadspaceShakeWorkDelayRegisterAddress, config.HeadspaceShakeWorkDelay100ms, "顶空瓶摇晃工位延时时间"),
			(InitHeadspaceShakeTargetCountRegisterAddress, config.HeadspaceShakeTargetCount, "顶空瓶摇晃目标次数"),
			(InitButanolAspirateDelayRegisterAddress, config.ButanolAspirateDelay100ms, "叔丁醇吸液延时时间"),
			(InitButanolDispenseDelayRegisterAddress, config.ButanolDispenseDelay100ms, "叔丁醇打液延时时间"),
			(InitSampleBottlePressureTimeRegisterAddress, config.SampleBottlePressureTime100ms, "样品瓶加压时间"),
			(InitQuantitativeLoopBalanceTimeRegisterAddress, config.QuantitativeLoopBalanceTime100ms, "定量环平衡时间"),
			(InitInjectionTimeRegisterAddress, config.InjectionTime100ms, "进样时间"),
			(InitSampleBottlePressurePositionRegisterAddress, config.SampleBottlePressurePosition, "样品瓶加压位置"),
			(InitQuantitativeLoopBalancePositionRegisterAddress, config.QuantitativeLoopBalancePosition, "定量环平衡位置"),
			(InitInjectionPositionRegisterAddress, config.InjectionPosition, "进样位置")
		};
	}
}
