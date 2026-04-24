using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页监控循环工具
internal static class HomeMonitorLoops
{
	/// <summary>
	/// 运行料架工序监控循环并在读取成功时回调应用寄存器状态
	/// </summary>
	/// By:ChengLei
	/// <param name="context">料架工序监控上下文</param>
	/// <param name="token">取消令牌</param>
	/// <returns>返回监控循环任务</returns>
	/// <remarks>
	/// 保留原有离线清空 读取失败仅首错记日志 恢复后补一条恢复日志的行为
	/// </remarks>
	public static async Task RunRackProcessMonitorAsync(HomeRackProcessMonitorContext context, CancellationToken token)
	{
		bool readFaultLogged = false;
		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!context.IsPlcConnected())
				{
					context.RunOnUiThread(context.ClearStates);
					readFaultLogged = false;
					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				if (!context.IsDetectionStarted())
				{
					context.RunOnUiThread(context.ClearStates);
					readFaultLogged = false;
					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				HomePlcRegisterReadResult read = await context.ReadRegistersAsync(token).ConfigureAwait(false);
				if (!read.Success)
				{
					if (!readFaultLogged)
					{
						context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取失败：" + read.Error);
						readFaultLogged = true;
					}

					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				context.RunOnUiThread(() => context.ApplyRegisters(read.Values));
				if (readFaultLogged)
				{
					context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取已恢复。");
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
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "料架工序状态读取失败：" + ex.Message);
					readFaultLogged = true;
				}
			}

			await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// 运行自动手动档位监控循环并持续同步软件档位状态
	/// </summary>
	/// By:ChengLei
	/// <param name="context">档位监控上下文</param>
	/// <param name="token">取消令牌</param>
	/// <returns>返回档位监控循环任务</returns>
	/// <remarks>
	/// 直接读取 PLC 的 M10 位 避免仅依赖软件内存态导致设置页和首页档位显示失真
	/// </remarks>
	public static async Task RunOperationModeMonitorAsync(HomeOperationModeMonitorContext context, CancellationToken token)
	{
		bool readFaultLogged = false;
		bool hasLastMode = false;
		OperationMode lastMode = OperationMode.Auto;

		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!context.IsPlcConnected())
				{
					readFaultLogged = false;
					hasLastMode = false;
					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				HomePlcBoolReadResult autoModeRead = await context.ReadAutoModeAsync(token).ConfigureAwait(false);
				if (!autoModeRead.Success)
				{
					if (!readFaultLogged)
					{
						context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "档位同步失败：" + autoModeRead.Error);
						readFaultLogged = true;
					}

					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				OperationMode mode = autoModeRead.Value ? OperationMode.Auto : OperationMode.Manual;
				if (!hasLastMode || lastMode != mode)
				{
					context.RunOnUiThread(() => context.SetOperationMode(mode));
					lastMode = mode;
					hasLastMode = true;
				}

				if (readFaultLogged)
				{
					context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "档位同步已恢复。");
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
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "档位同步失败：" + ex.Message);
					readFaultLogged = true;
				}
			}

			try
			{
				await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	/// <summary>
	/// 运行工艺模式监控循环并在模式变化时回调页面状态更新
	/// </summary>
	/// By:ChengLei
	/// <param name="context">工艺模式监控上下文</param>
	/// <param name="token">取消令牌</param>
	/// <returns>返回监控循环任务</returns>
	/// <remarks>
	/// 保留原有流程模式监听的失败短路与恢复日志语义
	/// </remarks>
	public static async Task RunProcessModeMonitorAsync(HomeProcessModeMonitorContext context, CancellationToken token)
	{
		bool readFaultLogged = false;
		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!context.IsPlcConnected())
				{
					readFaultLogged = false;
					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				HomeProcessModeReadResult modeReads = await context.ReadProcessModeAsync(token).ConfigureAwait(false);
				if (!(modeReads.Standby.Success && modeReads.Pressure.Success && modeReads.Exhaust.Success && modeReads.Injection.Success))
				{
					if (!readFaultLogged)
					{
						string readError = !modeReads.Standby.Success
							? modeReads.Standby.Error
							: !modeReads.Pressure.Success
								? modeReads.Pressure.Error
								: !modeReads.Exhaust.Success
									? modeReads.Exhaust.Error
									: modeReads.Injection.Error;
						context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听失败：" + readError);
						readFaultLogged = true;
					}

					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				HomeProcessModeState mode = ResolveProcessModeState(
					modeReads.Standby.Value,
					modeReads.Pressure.Value,
					modeReads.Exhaust.Value,
					modeReads.Injection.Value);
				context.RunOnUiThread(() => context.SetProcessMode(mode));
				if (readFaultLogged)
				{
					context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听已恢复。");
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
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "流程模式监听失败：" + ex.Message);
					readFaultLogged = true;
				}
			}

			await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// 运行报警监控循环并驱动首页报警联动行为
	/// </summary>
	/// By:ChengLei
	/// <param name="context">报警监控上下文</param>
	/// <param name="token">取消令牌</param>
	/// <returns>返回监控循环任务</returns>
	/// <remarks>
	/// 保留原有连接变化日志 报警触发联动停机 与通讯故障恢复日志语义
	/// </remarks>
	public static async Task RunAlarmMonitorAsync(HomeAlarmMonitorContext context, CancellationToken token)
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
				bool isConnected = context.IsPlcConnected();
				if (!hasConnectionState)
				{
					hasConnectionState = true;
					lastConnectionState = isConnected;
				}
				else if (lastConnectionState != isConnected)
				{
					lastConnectionState = isConnected;
					context.AddLog(
						isConnected ? HomeLogLevel.Info : HomeLogLevel.Error,
						HomeLogSource.Hardware,
						HomeLogKind.Operation,
						isConnected ? "PLC连接已恢复（RS485在线）。" : "PLC连接已断开（RS485离线）。");
				}

				if (!isConnected)
				{
					context.SetAlarmActive(false);
					hasLastState = false;
					readErrorLogged = false;
					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				HomePlcBoolReadResult alarmRead = await context.ReadAlarmAsync(token).ConfigureAwait(false);
				if (!alarmRead.Success)
				{
					if (!commFaultActive)
					{
						context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯中断：" + alarmRead.Error);
						commFaultActive = true;
					}

					if (!readErrorLogged)
					{
						context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总监听失败：" + alarmRead.Error);
						readErrorLogged = true;
					}

					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				bool currentState = alarmRead.Value;
				if (commFaultActive)
				{
					context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯已恢复。");
					commFaultActive = false;
				}

				context.SetAlarmActive(currentState);
				if (!hasLastState)
				{
					hasLastState = true;
					lastState = currentState;
					if (currentState)
					{
						context.RunOnUiThread(() =>
						{
							context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总触发(M2=1)。");
							context.SetCountRuleText("报警中：请先排查并清除报警，再开始检测。");
							if (context.IsDetectionStarted())
							{
								_ = context.AutoStopDetectionAsync();
							}
						});
					}
				}
				else if (!lastState && currentState)
				{
					context.RunOnUiThread(() =>
					{
						context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总触发(M2=1)。");
						context.SetCountRuleText("报警中：请先排查并清除报警，再开始检测。");
						if (context.IsDetectionStarted())
						{
							_ = context.AutoStopDetectionAsync();
						}
					});
				}
				else if (lastState && !currentState)
				{
					context.RunOnUiThread(() =>
					{
						context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总解除(M2=0)。");
						if (!context.IsDetectionStarted())
						{
							context.SetCountRuleText("报警已解除：可重新开始检测。");
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
			catch (Exception ex)
			{
				if (!commFaultActive)
				{
					context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC通讯中断：" + ex.Message);
					commFaultActive = true;
				}

				if (!readErrorLogged)
				{
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "报警汇总监听失败：" + ex.Message);
					readErrorLogged = true;
				}
			}

			await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// 运行温控后台监控循环并持续保障实验环境温度。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">温控监控上下文。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回温控监控循环任务。</returns>
	/// <remarks>
	/// 软件打开后即持续运行 不阻断开始检测 仅负责温度读取 偏差纠偏和异常日志。
	/// </remarks>
	public static async Task RunTemperatureMonitorAsync(HomeTemperatureMonitorContext context, CancellationToken token)
	{
		Dictionary<string, HomeTemperatureMonitorChannelState> states = new Dictionary<string, HomeTemperatureMonitorChannelState>(StringComparer.OrdinalIgnoreCase);
		bool tcpFaultLogged = false;

		while (!token.IsCancellationRequested)
		{
			try
			{
				if (!context.IsTcpRunning())
				{
					if (!tcpFaultLogged)
					{
						context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "TCP服务未启动，温控监控等待恢复。");
						tcpFaultLogged = true;
					}

					await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
					continue;
				}

				if (tcpFaultLogged)
				{
					context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "TCP服务已恢复，温控监控继续运行。");
					tcpFaultLogged = false;
				}

				foreach (HomeTemperatureMonitorTarget target in context.LoadTargets())
				{
					token.ThrowIfCancellationRequested();
					HomeTemperatureMonitorChannelState state = GetOrCreateChannelState(states, target.Station);
					await MonitorTemperatureChannelAsync(context, target, state, token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "温控监控异常：" + ex.Message);
			}

			await Task.Delay(context.PollInterval, token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// 根据模式线圈组合解析当前工艺模式
	/// </summary>
	/// By:ChengLei
	/// <param name="standby">待机模式信号</param>
	/// <param name="pressure">增压模式信号</param>
	/// <param name="exhaust">排气模式信号</param>
	/// <param name="injection">进样模式信号</param>
	/// <returns>返回解析后的首页工艺模式状态</returns>
	/// <remarks>
	/// 多个位同时为高电平时保持与原实现一致的优先级 注入高于排气 排气高于压力 压力高于待机
	/// </remarks>
	private static HomeProcessModeState ResolveProcessModeState(bool standby, bool pressure, bool exhaust, bool injection)
	{
		if (injection)
		{
			return HomeProcessModeState.Injection;
		}

		if (exhaust)
		{
			return HomeProcessModeState.Exhaust;
		}

		if (pressure)
		{
			return HomeProcessModeState.Pressure;
		}

		if (standby)
		{
			return HomeProcessModeState.Standby;
		}

		return HomeProcessModeState.Standby;
	}

	/// <summary>
	/// 获取或创建指定温控通道的运行状态对象。
	/// </summary>
	/// By:ChengLei
	/// <param name="states">运行状态字典。</param>
	/// <param name="deviceType">温控设备类型。</param>
	/// <returns>返回对应通道状态对象。</returns>
	/// <remarks>
	/// 由温控监控循环为每一路温控维护独立的日志去重和下发节流状态。
	/// </remarks>
	private static HomeTemperatureMonitorChannelState GetOrCreateChannelState(
		IDictionary<string, HomeTemperatureMonitorChannelState> states,
		string station)
	{
		if (!states.TryGetValue(station, out HomeTemperatureMonitorChannelState? state))
		{
			state = new HomeTemperatureMonitorChannelState();
			states[station] = state;
		}

		return state;
	}

	/// <summary>
	/// 监控单一路温控设备并在温度偏低时自动下发设定值。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">温控监控上下文。</param>
	/// <param name="target">当前监控目标。</param>
	/// <param name="state">通道运行状态。</param>
	/// <param name="token">取消令牌。</param>
	/// <returns>返回单路温控监控任务。</returns>
	/// <remarks>
	/// 读取成功后只在状态变化时记日志 避免后台监控持续刷屏。
	/// </remarks>
	private static async Task MonitorTemperatureChannelAsync(
		HomeTemperatureMonitorContext context,
		HomeTemperatureMonitorTarget target,
		HomeTemperatureMonitorChannelState state,
		CancellationToken token)
	{
		string channelLabel = $"{target.DisplayName}(站号{target.Station})";
		string deviceKey;
		try
		{
			deviceKey = context.ResolveDeviceKey();
			state.MappingFaultLogged = false;
		}
		catch (Exception ex)
		{
			if (!state.MappingFaultLogged)
			{
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}未配置通信映射：{ex.Message}");
				state.MappingFaultLogged = true;
			}

			return;
		}

		if (!context.IsDeviceConnected(deviceKey))
		{
			if (!state.OfflineLogged)
			{
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}未连接，温控监控等待设备上线。");
				state.OfflineLogged = true;
			}

			state.ReadFaultLogged = false;
			state.WriteFaultLogged = false;
			return;
		}

		if (state.OfflineLogged)
		{
			context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}已连接，恢复温控监控。");
			state.OfflineLogged = false;
		}

		double currentTemperature;
		try
		{
			currentTemperature = await context.ReadTemperatureAsync(target.Station, token).ConfigureAwait(false);
			if (state.ReadFaultLogged)
			{
				context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}温度读取已恢复。");
				state.ReadFaultLogged = false;
			}
		}
		catch (Exception ex)
		{
			if (!state.ReadFaultLogged)
			{
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}温度读取失败：{ex.Message}");
				state.ReadFaultLogged = true;
			}

			return;
		}

		bool belowTarget = currentTemperature + context.TemperatureTolerance < target.TargetTemperature;
		if (belowTarget)
		{
			try
			{
				if (!state.BelowTargetLogged)
				{
					context.AddLog(
						HomeLogLevel.Warning,
						HomeLogSource.Hardware,
						HomeLogKind.Operation,
						$"{channelLabel}当前温度 {currentTemperature:F1}℃ 低于设定 {target.TargetTemperature:F1}℃，开始自动纠偏。");
				}

				DateTime utcNow = DateTime.UtcNow;
				if (!state.LastWriteUtc.HasValue || utcNow - state.LastWriteUtc.Value >= context.WriteRefreshInterval)
				{
					await context.WriteTargetTemperatureAsync(target.Station, target.TargetTemperature, token).ConfigureAwait(false);
					state.LastWriteUtc = utcNow;
					state.WriteFaultLogged = false;
				}

				state.BelowTargetLogged = true;
			}
			catch (Exception ex)
			{
				if (!state.WriteFaultLogged)
				{
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}温控设定下发失败：{ex.Message}");
					state.WriteFaultLogged = true;
				}
			}

			return;
		}

		if (state.BelowTargetLogged)
		{
			context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, $"{channelLabel}温度已恢复达标，当前 {currentTemperature:F1}℃。");
		}

		state.BelowTargetLogged = false;
		state.WriteFaultLogged = false;
		state.LastWriteUtc = null;
	}
}

/// <summary>
/// 作用
/// PLC布尔读取结果
internal readonly record struct HomePlcBoolReadResult(bool Success, bool Value, string Error);

/// <summary>
/// 作用
/// PLC寄存器读取结果
internal readonly record struct HomePlcRegisterReadResult(bool Success, ushort[] Values, string Error);

/// <summary>
/// 作用
/// 工艺模式点位读取结果
internal readonly record struct HomeProcessModeReadResult(
	HomePlcBoolReadResult Standby,
	HomePlcBoolReadResult Pressure,
	HomePlcBoolReadResult Exhaust,
	HomePlcBoolReadResult Injection);

/// <summary>
/// 作用
/// 首页料架工序监控上下文
internal sealed class HomeRackProcessMonitorContext
{
	public required TimeSpan PollInterval { get; init; }
	public required Func<bool> IsPlcConnected { get; init; }
	public required Func<bool> IsDetectionStarted { get; init; }
	public required Func<CancellationToken, Task<HomePlcRegisterReadResult>> ReadRegistersAsync { get; init; }
	public required Action<Action> RunOnUiThread { get; init; }
	public required Action ClearStates { get; init; }
	public required Action<IReadOnlyList<ushort>> ApplyRegisters { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页工艺模式监控上下文
internal sealed class HomeProcessModeMonitorContext
{
	public required TimeSpan PollInterval { get; init; }
	public required Func<bool> IsPlcConnected { get; init; }
	public required Func<CancellationToken, Task<HomeProcessModeReadResult>> ReadProcessModeAsync { get; init; }
	public required Action<Action> RunOnUiThread { get; init; }
	public required Action<HomeProcessModeState> SetProcessMode { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页档位监控上下文
internal sealed class HomeOperationModeMonitorContext
{
	public required TimeSpan PollInterval { get; init; }
	public required Func<bool> IsPlcConnected { get; init; }
	public required Func<CancellationToken, Task<HomePlcBoolReadResult>> ReadAutoModeAsync { get; init; }
	public required Action<Action> RunOnUiThread { get; init; }
	public required Action<OperationMode> SetOperationMode { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页报警监控上下文
internal sealed class HomeAlarmMonitorContext
{
	public required TimeSpan PollInterval { get; init; }
	public required Func<bool> IsPlcConnected { get; init; }
	public required Func<bool> IsDetectionStarted { get; init; }
	public required Func<CancellationToken, Task<HomePlcBoolReadResult>> ReadAlarmAsync { get; init; }
	public required Action<bool> SetAlarmActive { get; init; }
	public required Action<Action> RunOnUiThread { get; init; }
	public required Action<string> SetCountRuleText { get; init; }
	public required Func<Task> AutoStopDetectionAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页温控监控目标
internal readonly record struct HomeTemperatureMonitorTarget(string DisplayName, string Station, double TargetTemperature);

/// <summary>
/// 作用
/// 首页温控监控上下文
internal sealed class HomeTemperatureMonitorContext
{
	public required TimeSpan PollInterval { get; init; }
	public required TimeSpan WriteRefreshInterval { get; init; }
	public required double TemperatureTolerance { get; init; }
	public required Func<bool> IsTcpRunning { get; init; }
	public required Func<IReadOnlyList<HomeTemperatureMonitorTarget>> LoadTargets { get; init; }
	public required Func<string> ResolveDeviceKey { get; init; }
	public required Func<string, bool> IsDeviceConnected { get; init; }
	public required Func<string, CancellationToken, Task<double>> ReadTemperatureAsync { get; init; }
	public required Func<string, double, CancellationToken, Task> WriteTargetTemperatureAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 温控监控通道运行状态
internal sealed class HomeTemperatureMonitorChannelState
{
	public bool MappingFaultLogged { get; set; }
	public bool OfflineLogged { get; set; }
	public bool ReadFaultLogged { get; set; }
	public bool WriteFaultLogged { get; set; }
	public bool BelowTargetLogged { get; set; }
	public DateTime? LastWriteUtc { get; set; }
}
