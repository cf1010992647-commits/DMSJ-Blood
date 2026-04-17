using System;
using System.Threading.Tasks;
using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页 PLC 指令协调器
internal sealed class HomePlcCommandCoordinator
{
	private readonly HomePlcGateway _plcGateway;
	private readonly TimeSpan _initTimeout;
	private readonly TimeSpan _initPollInterval;

	/// <summary>
	/// 初始化首页PLC指令协调器并保存初始化等待参数
	/// </summary>
	/// By:ChengLei
	/// <param name="plcGateway">首页PLC操作网关</param>
	/// <param name="initTimeout">初始化等待超时时间</param>
	/// <param name="initPollInterval">初始化完成位轮询周期</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建 统一协调首页初始化与启停相关 PLC 指令
	/// </remarks>
	public HomePlcCommandCoordinator(HomePlcGateway plcGateway, TimeSpan initTimeout, TimeSpan initPollInterval)
	{
		_plcGateway = plcGateway ?? throw new ArgumentNullException(nameof(plcGateway));
		_initTimeout = initTimeout;
		_initPollInterval = initPollInterval;
	}

	/// <summary>
	/// 下发初始化参数与初始化命令并等待初始化完成位
	/// </summary>
	/// By:ChengLei
	/// <param name="config">流程参数配置</param>
	/// <param name="onReadError">初始化完成位读取失败时的回调</param>
	/// <returns>返回初始化执行结果</returns>
	/// <remarks>
	/// 命令发送失败直接返回错误 读取失败仅记录回调并继续等待直到超时或成功
	/// </remarks>
	public async Task<HomePlcInitializeResult> InitializeAsync(ProcessParameterConfig config, Action<string>? onReadError = null)
	{
		try
		{
			await _plcGateway.SendInitParametersWithVerifyAsync(config).ConfigureAwait(false);
			await _plcGateway.SendInitCommandAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return new HomePlcInitializeResult(false, false, ex.Message);
		}

		bool completed = await WaitForInitDoneAsync(onReadError).ConfigureAwait(false);
		return completed
			? new HomePlcInitializeResult(true, false, null)
			: new HomePlcInitializeResult(false, true, null);
	}

	/// <summary>
	/// 检查开始前置条件并在满足时发送开始脉冲
	/// </summary>
	/// By:ChengLei
	/// <returns>返回开始尝试结果</returns>
	/// <remarks>
	/// 保留原有前置条件语义 报警 自动模式和初始化完成位任一不满足时不发送开始脉冲
	/// </remarks>
	public async Task<HomeStartAttemptResult> TryStartAsync()
	{
		try
		{
			(bool Alarm, bool AutoMode, bool InitDone) preconditions = await _plcGateway.ReadStartPreconditionsAsync().ConfigureAwait(false);
			if (preconditions.Alarm || !preconditions.AutoMode || !preconditions.InitDone)
			{
				return new HomeStartAttemptResult(false, preconditions.Alarm, preconditions.AutoMode, preconditions.InitDone, null);
			}

			await _plcGateway.PulseStartCommandAsync().ConfigureAwait(false);
			return new HomeStartAttemptResult(true, preconditions.Alarm, preconditions.AutoMode, preconditions.InitDone, null);
		}
		catch (Exception ex)
		{
			return new HomeStartAttemptResult(false, false, false, false, ex.Message);
		}
	}

	/// <summary>
	/// 确保开始命令位复位为低电平
	/// </summary>
	/// By:ChengLei
	/// <returns>返回命令执行结果</returns>
	/// <remarks>
	/// 用于开始前置校验失败或异常后的命令位复位
	/// </remarks>
	public Task<HomeCommandResult> EnsureStartCommandLowAsync()
	{
		return ExecuteAsync(_plcGateway.EnsureStartCommandLowAsync);
	}

	/// <summary>
	/// 发送停止命令脉冲
	/// </summary>
	/// By:ChengLei
	/// <returns>返回命令执行结果</returns>
	/// <remarks>
	/// 由首页停止与报警自动停机流程复用
	/// </remarks>
	public Task<HomeCommandResult> SendStopAsync()
	{
		return ExecuteAsync(_plcGateway.PulseStopCommandAsync);
	}

	/// <summary>
	/// 发送急停命令脉冲
	/// </summary>
	/// By:ChengLei
	/// <returns>返回命令执行结果</returns>
	/// <remarks>
	/// 由首页急停流程调用
	/// </remarks>
	public Task<HomeCommandResult> SendEmergencyStopAsync()
	{
		return ExecuteAsync(_plcGateway.PulseEmergencyStopCommandAsync);
	}

	/// <summary>
	/// 轮询初始化完成位直到成功或超时
	/// </summary>
	/// By:ChengLei
	/// <param name="onReadError">读取失败时的日志回调</param>
	/// <returns>返回是否在超时前检测到初始化完成</returns>
	/// <remarks>
	/// 保持原有容错语义 初始化完成位读取失败时继续等待并避免重复刷日志
	/// </remarks>
	private async Task<bool> WaitForInitDoneAsync(Action<string>? onReadError)
	{
		DateTime deadline = DateTime.UtcNow.Add(_initTimeout);
		bool readErrorLogged = false;
		bool lastState;
		bool seenLow;

		try
		{
			lastState = await _plcGateway.ReadInitDoneDirectAsync().ConfigureAwait(false);
			seenLow = !lastState;
		}
		catch (Exception ex)
		{
			onReadError?.Invoke(ex.Message);
			lastState = false;
			seenLow = true;
			readErrorLogged = true;
		}

		while (DateTime.UtcNow < deadline)
		{
			try
			{
				bool currentState = await _plcGateway.ReadInitDoneDirectAsync().ConfigureAwait(false);
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
			catch (Exception ex)
			{
				if (!readErrorLogged)
				{
					onReadError?.Invoke(ex.Message);
					readErrorLogged = true;
				}
			}

			await Task.Delay(_initPollInterval).ConfigureAwait(false);
		}

		return false;
	}

	/// <summary>
	/// 执行PLC命令并把异常转换为统一结果
	/// </summary>
	/// By:ChengLei
	/// <param name="executeAsync">实际PLC命令委托</param>
	/// <returns>返回命令执行结果</returns>
	/// <remarks>
	/// 用于首页停止 急停 和命令位复位等无返回值 PLC 指令
	/// </remarks>
	private static async Task<HomeCommandResult> ExecuteAsync(Func<Task> executeAsync)
	{
		try
		{
			await executeAsync().ConfigureAwait(false);
			return new HomeCommandResult(true, null);
		}
		catch (Exception ex)
		{
			return new HomeCommandResult(false, ex.Message);
		}
	}
}

/// <summary>
/// 作用
/// 首页PLC通用命令结果
internal readonly record struct HomeCommandResult(bool Success, string? Error);

/// <summary>
/// 作用
/// 首页初始化命令执行结果
internal readonly record struct HomePlcInitializeResult(bool Completed, bool TimedOut, string? CommandError);

/// <summary>
/// 作用
/// 首页开始命令前置校验与执行结果
internal readonly record struct HomeStartAttemptResult(bool Success, bool AlarmActive, bool AutoModeEnabled, bool InitDone, string? Error);
