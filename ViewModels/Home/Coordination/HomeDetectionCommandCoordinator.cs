using System;
using System.Threading.Tasks;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页检测命令协调器
internal sealed class HomeDetectionCommandCoordinator
{
	/// <summary>
	/// 执行首页初始化命令并统一处理状态切换与日志
	/// </summary>
	/// By:ChengLei
	/// <param name="context">初始化命令上下文</param>
	/// <returns>返回初始化执行任务</returns>
	/// <remarks>
	/// 保留原有初始化防重入 参数刷新 PLC 初始化与日志语义
	/// </remarks>
	public async Task InitializeAsync(HomeInitializeCommandContext context)
	{
		if (!context.DetectionState.TryBeginInitialize())
		{
			return;
		}

		try
		{
			context.InvalidateCommands();
			context.AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "初始化中...请等待");
			ProcessParameterConfig config = context.LoadProcessParameterConfig();
			context.ApplyConditions(config);

			HomePlcInitializeResult initResult = await context.InitializePlcAsync(
				config,
				message => context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化状态读取失败：" + message)).ConfigureAwait(true);
			if (!string.IsNullOrWhiteSpace(initResult.CommandError))
			{
				context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化失败：初始化命令发送失败。" + initResult.CommandError);
			}
			else if (initResult.Completed)
			{
				context.AddLog(HomeLogLevel.Info, HomeLogSource.Hardware, HomeLogKind.Operation, "初始化参数写入并校验成功（D6000、D6020、D6021、D6022、D6023、D6024、D6026、D6027、D6028、D6030、D6031、D6040、D6041、D6042、D6302、D6304、D6306）。");
				context.AddLog(HomeLogLevel.Info, HomeLogSource.System, HomeLogKind.Operation, "初始化成功");
			}
			else
			{
				context.AddLog(HomeLogLevel.Error, HomeLogSource.System, HomeLogKind.Operation, "初始化失败：超时（10分钟）");
			}
		}
		finally
		{
			context.DetectionState.FinishInitialize();
			context.InvalidateCommands();
		}
	}

	/// <summary>
	/// 执行首页开始命令并统一处理前置校验与状态切换
	/// </summary>
	/// By:ChengLei
	/// <param name="context">开始命令上下文</param>
	/// <returns>返回开始执行任务</returns>
	/// <remarks>
	/// 保留原有前置校验 开始失败复位 M5 和成功后启动流程与数量同步的语义
	/// </remarks>
	public async Task StartAsync(HomeStartCommandContext context)
	{
		if (!context.DetectionState.TryBeginStartProcessing())
		{
			return;
		}

		context.InvalidateCommands();
		try
		{
			if (context.SelectedTubeCount <= 0)
			{
				await context.ResetStartCommandLowAsync().ConfigureAwait(true);
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Process, HomeLogKind.Operation, "请先点击采血管架选择检测总数。");
				return;
			}

			if (!context.IsPlcConnected())
			{
				await context.ResetStartCommandLowAsync().ConfigureAwait(true);
				context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "PLC未连接，已发送 M5=0，禁止开始检测。");
				return;
			}

			HomeStartAttemptResult startResult = await context.TryStartAsync().ConfigureAwait(true);
			context.SetAlarmActive(startResult.AlarmActive);
			if (!startResult.Success)
			{
				await context.ResetStartCommandLowAsync().ConfigureAwait(true);
				if (!string.IsNullOrWhiteSpace(startResult.Error))
				{
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, "开始信号发送失败，已发送 M5=0：" + startResult.Error);
				}
				else
				{
					context.AddLog(HomeLogLevel.Warning, HomeLogSource.Hardware, HomeLogKind.Operation, $"开始前置条件不满足：M2={(startResult.AlarmActive ? 1 : 0)}，M10={(startResult.AutoModeEnabled ? 1 : 0)}，M14={(startResult.InitDone ? 1 : 0)}，已发送 M5=0。");
					if (startResult.AlarmActive)
					{
						context.DetectionState.MarkAlarmBlocked();
						context.RefreshDetectionState();
					}
				}

				return;
			}

			context.ClearTubeProcessRuntimeState();
			string batchNo = context.AllocateNextBatchNo();
			context.SetCurrentBatchNo(batchNo);
			context.DetectionState.MarkStarted();
			context.RefreshDetectionState();
			context.StartWorkflow();
			context.StartTubeCountSync();
			context.AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Detection, $"开始检测：{batchNo}，采血管{context.SelectedTubeCount}，顶空瓶{context.SelectedHeadspaceCount}。");
		}
		catch (Exception ex)
		{
			context.DetectionState.MarkStartFailed();
			context.RefreshDetectionState();
			await context.ResetStartCommandLowAsync().ConfigureAwait(true);
			context.AddLog(HomeLogLevel.Error, HomeLogSource.Process, HomeLogKind.Detection, "检测启动失败：" + ex.Message);
		}
		finally
		{
			context.DetectionState.FinishStartProcessing();
			context.InvalidateCommands();
		}
	}

	/// <summary>
	/// 执行首页停止命令并统一处理停机收尾
	/// </summary>
	/// By:ChengLei
	/// <param name="context">停止命令上下文</param>
	/// <returns>返回停止执行任务</returns>
	/// <remarks>
	/// 保留原有未运行时仅发送停止信号 运行中时停止流程与数量同步的语义
	/// </remarks>
	public async Task StopAsync(HomeStopCommandContext context)
	{
		if (!context.DetectionState.MarkStopped())
		{
			await context.SendStopAsync().ConfigureAwait(true);
			return;
		}

		context.RefreshDetectionState();
		await context.StopTubeCountSyncAsync().ConfigureAwait(true);
		context.ClearTubeProcessRuntimeState();
		context.ClearRackProcessStates();
		await context.StopWorkflowAsync().ConfigureAwait(true);
		await context.SendStopAsync().ConfigureAwait(true);
		context.AddLog(HomeLogLevel.Info, HomeLogSource.Process, HomeLogKind.Detection, "检测已停止。");
	}

	/// <summary>
	/// 执行首页急停命令并统一处理急停收尾
	/// </summary>
	/// By:ChengLei
	/// <param name="context">急停命令上下文</param>
	/// <returns>返回急停执行任务</returns>
	/// <remarks>
	/// 保留原有急停后同时发送急停与停止信号的语义
	/// </remarks>
	public async Task EmergencyStopAsync(HomeEmergencyStopCommandContext context)
	{
		context.DetectionState.MarkEmergencyStopped();
		context.RefreshDetectionState();
		await context.StopTubeCountSyncAsync().ConfigureAwait(true);
		context.ClearTubeProcessRuntimeState();
		context.ClearRackProcessStates();
		await context.StopWorkflowAsync().ConfigureAwait(true);
		await context.SendEmergencyStopAsync().ConfigureAwait(true);
		await context.SendStopAsync().ConfigureAwait(true);
		context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Operation, "急停触发，已停止当前动作。");
	}

	/// <summary>
	/// 执行报警触发后的自动停机处理
	/// </summary>
	/// By:ChengLei
	/// <param name="context">报警自动停机上下文</param>
	/// <returns>返回自动停机执行任务</returns>
	/// <remarks>
	/// 保留原有报警停机时不清理采血管运行上下文 仅停止流程并清理料架状态的语义
	/// </remarks>
	public async Task AutoStopByAlarmAsync(HomeAutoStopCommandContext context)
	{
		context.DetectionState.MarkAlarmStopped();
		context.RefreshDetectionState();
		await context.StopTubeCountSyncAsync().ConfigureAwait(true);
		context.ClearRackProcessStates();
		await context.StopWorkflowAsync().ConfigureAwait(true);
		await context.SendStopAsync().ConfigureAwait(true);
		context.AddLog(HomeLogLevel.Error, HomeLogSource.Hardware, HomeLogKind.Detection, "检测过程中报警汇总(M2=1)，已自动停止检测。");
	}
}

/// <summary>
/// 作用
/// 首页初始化命令上下文
internal sealed class HomeInitializeCommandContext
{
	public required HomeDetectionStateCoordinator DetectionState { get; init; }
	public required Func<ProcessParameterConfig> LoadProcessParameterConfig { get; init; }
	public required Action<ProcessParameterConfig> ApplyConditions { get; init; }
	public required Func<ProcessParameterConfig, Action<string>, Task<HomePlcInitializeResult>> InitializePlcAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
	public required Action InvalidateCommands { get; init; }
}

/// <summary>
/// 作用
/// 首页开始命令上下文
internal sealed class HomeStartCommandContext
{
	public required HomeDetectionStateCoordinator DetectionState { get; init; }
	public required int SelectedTubeCount { get; init; }
	public required int SelectedHeadspaceCount { get; init; }
	public required Func<bool> IsPlcConnected { get; init; }
	public required Func<Task> ResetStartCommandLowAsync { get; init; }
	public required Func<Task<HomeStartAttemptResult>> TryStartAsync { get; init; }
	public required Action<bool> SetAlarmActive { get; init; }
	public required Action ClearTubeProcessRuntimeState { get; init; }
	public required Func<string> AllocateNextBatchNo { get; init; }
	public required Action<string> SetCurrentBatchNo { get; init; }
	public required Action RefreshDetectionState { get; init; }
	public required Action StartWorkflow { get; init; }
	public required Action StartTubeCountSync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
	public required Action InvalidateCommands { get; init; }
}

/// <summary>
/// 作用
/// 首页停止命令上下文
internal sealed class HomeStopCommandContext
{
	public required HomeDetectionStateCoordinator DetectionState { get; init; }
	public required Action RefreshDetectionState { get; init; }
	public required Func<Task> StopTubeCountSyncAsync { get; init; }
	public required Action ClearTubeProcessRuntimeState { get; init; }
	public required Action ClearRackProcessStates { get; init; }
	public required Func<Task> StopWorkflowAsync { get; init; }
	public required Func<Task> SendStopAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页急停命令上下文
internal sealed class HomeEmergencyStopCommandContext
{
	public required HomeDetectionStateCoordinator DetectionState { get; init; }
	public required Action RefreshDetectionState { get; init; }
	public required Func<Task> StopTubeCountSyncAsync { get; init; }
	public required Action ClearTubeProcessRuntimeState { get; init; }
	public required Action ClearRackProcessStates { get; init; }
	public required Func<Task> StopWorkflowAsync { get; init; }
	public required Func<Task> SendEmergencyStopAsync { get; init; }
	public required Func<Task> SendStopAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}

/// <summary>
/// 作用
/// 首页报警自动停机上下文
internal sealed class HomeAutoStopCommandContext
{
	public required HomeDetectionStateCoordinator DetectionState { get; init; }
	public required Action RefreshDetectionState { get; init; }
	public required Func<Task> StopTubeCountSyncAsync { get; init; }
	public required Action ClearRackProcessStates { get; init; }
	public required Func<Task> StopWorkflowAsync { get; init; }
	public required Func<Task> SendStopAsync { get; init; }
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> AddLog { get; init; }
}
