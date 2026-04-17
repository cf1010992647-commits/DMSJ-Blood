using System;
using System.Threading;
using System.Threading.Tasks;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页后台任务生命周期协调器
internal sealed class HomeBackgroundTaskCoordinator
{
	private readonly TimeSpan _stopTimeout;
	private readonly Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> _addLog;

	/// <summary>
	/// 初始化首页后台任务生命周期协调器
	/// </summary>
	/// By:ChengLei
	/// <param name="stopTimeout">后台任务停止等待超时时间</param>
	/// <param name="addLog">首页日志写入委托</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建 用于统一管理后台任务启动 停止和超时日志
	/// </remarks>
	public HomeBackgroundTaskCoordinator(TimeSpan stopTimeout, Action<HomeLogLevel, HomeLogSource, HomeLogKind, string> addLog)
	{
		_stopTimeout = stopTimeout;
		_addLog = addLog ?? throw new ArgumentNullException(nameof(addLog));
	}

	/// <summary>
	/// 重启指定后台任务槽位
	/// </summary>
	/// By:ChengLei
	/// <param name="slot">后台任务槽位</param>
	/// <param name="taskFactory">基于取消令牌创建后台任务的委托</param>
	/// <remarks>
	/// 启动前会先同步停止旧任务实例 确保同一槽位不会并发运行多个后台循环
	/// </remarks>
	public void Restart(HomeBackgroundTaskSlot slot, Func<CancellationToken, Task> taskFactory)
	{
		StopAsync(slot).GetAwaiter().GetResult();
		CancellationTokenSource cts = new CancellationTokenSource();
		slot.Set(cts, Task.Run(() => taskFactory(cts.Token)));
	}

	/// <summary>
	/// 停止指定后台任务槽位并等待退出
	/// </summary>
	/// By:ChengLei
	/// <param name="slot">后台任务槽位</param>
	/// <returns>返回异步停止任务</returns>
	/// <remarks>
	/// 所有等待均设置最大超时 避免页面销毁或流程停止时无限阻塞
	/// </remarks>
	public async Task StopAsync(HomeBackgroundTaskSlot slot)
	{
		CancellationTokenSource? cts = slot.CancellationTokenSource;
		Task? task = slot.Task;
		cts?.Cancel();

		if (task != null && !task.IsCompleted)
		{
			try
			{
				await task.WaitAsync(_stopTimeout).ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				_addLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, $"{slot.TaskName}停止超时（{_stopTimeout.TotalSeconds:F0}s）。");
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_addLog(HomeLogLevel.Warning, HomeLogSource.System, HomeLogKind.Operation, $"{slot.TaskName}停止异常：{ex.Message}");
			}
		}

		cts?.Dispose();
		slot.Clear();
	}
}

/// <summary>
/// 作用
/// 首页后台任务槽位
internal sealed class HomeBackgroundTaskSlot
{
	/// <summary>
	/// 初始化首页后台任务槽位
	/// </summary>
	/// By:ChengLei
	/// <param name="taskName">后台任务名称</param>
	/// <remarks>
	/// 一个槽位仅对应一类后台任务 由生命周期协调器统一启动和停止
	/// </remarks>
	public HomeBackgroundTaskSlot(string taskName)
	{
		TaskName = taskName ?? throw new ArgumentNullException(nameof(taskName));
	}

	/// <summary>
	/// 后台任务名称
	/// </summary>
	/// By:ChengLei
	public string TaskName { get; }

	/// <summary>
	/// 当前后台任务取消源
	/// </summary>
	/// By:ChengLei
	public CancellationTokenSource? CancellationTokenSource { get; private set; }

	/// <summary>
	/// 当前后台任务实例
	/// </summary>
	/// By:ChengLei
	public Task? Task { get; private set; }

	/// <summary>
	/// 设置后台任务槽位中的运行实例
	/// </summary>
	/// By:ChengLei
	/// <param name="cts">后台任务取消源</param>
	/// <param name="task">后台任务实例</param>
	/// <remarks>
	/// 由生命周期协调器在重启后台任务时调用
	/// </remarks>
	public void Set(CancellationTokenSource cts, Task task)
	{
		CancellationTokenSource = cts;
		Task = task;
	}

	/// <summary>
	/// 清空后台任务槽位中的运行实例
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由生命周期协调器在任务停止后调用
	/// </remarks>
	public void Clear()
	{
		CancellationTokenSource = null;
		Task = null;
	}
}
