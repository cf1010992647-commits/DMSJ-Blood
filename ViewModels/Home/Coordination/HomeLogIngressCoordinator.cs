using System;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页日志入口协调器
internal sealed class HomeLogIngressCoordinator
{
	/// <summary>
	/// 追加首页日志并统一处理线程切换 筛选刷新 与计数更新
	/// </summary>
	/// By:ChengLei
	/// <param name="context">首页日志写入上下文</param>
	/// <param name="level">日志级别</param>
	/// <param name="source">日志来源分类</param>
	/// <param name="kind">日志业务类别</param>
	/// <param name="message">日志消息文本</param>
	/// <param name="tubeIndex">关联采血管序号</param>
	/// <param name="persistToFile">是否写入本地文件</param>
	/// <remarks>
	/// 保留原有非 UI 线程自动切回 UI 线程后再操作可见日志集合的行为
	/// </remarks>
	public void Add(
		HomeLogWriteContext context,
		HomeLogLevel level,
		HomeLogSource source,
		HomeLogKind kind,
		string message,
		int? tubeIndex,
		bool persistToFile)
	{
		if (!context.IsOnUiThread())
		{
			context.RunOnUiThread(() => Add(context, level, source, kind, message, tubeIndex, persistToFile));
			return;
		}

		HomeLogCounters counters = context.Controller.Add(level, source, kind, message, tubeIndex, persistToFile, context.CreateFilterState());
		context.ApplyLogCounters(counters);
	}

	/// <summary>
	/// 将通信层日志映射为首页日志
	/// </summary>
	/// By:ChengLei
	/// <param name="addLog">首页日志写入委托</param>
	/// <param name="log">通信层日志对象</param>
	/// <remarks>
	/// 保留原有通信来源前缀与级别映射规则
	/// </remarks>
	public void HandleCommunicationLog(
		Action<HomeLogLevel, HomeLogSource, HomeLogKind, string, int?, bool> addLog,
		CommunicationManager.LogMessage log)
	{
		HomeLogLevel level = log.Level switch
		{
			CommunicationManager.LogLevel.Error => HomeLogLevel.Error,
			CommunicationManager.LogLevel.Warning => HomeLogLevel.Warning,
			_ => HomeLogLevel.Info
		};
		string sourceText = string.IsNullOrWhiteSpace(log.Source) ? "通信" : log.Source;
		addLog(level, HomeLogSource.Hardware, HomeLogKind.Operation, $"[{sourceText}] {log.Message}", null, true);
	}

	/// <summary>
	/// 将流程引擎日志映射为首页日志或采血管事件
	/// </summary>
	/// By:ChengLei
	/// <param name="context">流程日志映射上下文</param>
	/// <param name="log">流程引擎日志对象</param>
	/// <remarks>
	/// 采血管相关日志继续转为串行事件 其余日志直接进入首页日志流
	/// </remarks>
	public void HandleWorkflowLog(HomeWorkflowLogIngressContext context, WorkflowEngine.WorkflowLogMessage log)
	{
		string? scanCode = string.IsNullOrWhiteSpace(log.ScanCode) ? HomeLogParser.ExtractScanCode(log.Message) : log.ScanCode;
		HomeLogLevel level = HomeLogParser.ParseLevel(log.LevelText);
		HomeLogKind kind = HomeLogParser.ParseKind(log.LogKind);
		if (log.TubeIndex > 0)
		{
			context.EnqueueTubeProcessEvent(new TubeProcessEvent
			{
				Timestamp = log.Timestamp,
				BatchNo = string.IsNullOrWhiteSpace(log.BatchNo) ? context.GetCurrentBatchNoForLogging() : log.BatchNo,
				TubeIndex = log.TubeIndex,
				HeadspaceBottleTag = log.HeadspaceBottleTag,
				ScanCode = scanCode ?? string.Empty,
				ProcessName = string.IsNullOrWhiteSpace(log.ProcessName) ? "流程日志" : log.ProcessName,
				EventName = string.IsNullOrWhiteSpace(log.EventName) ? "记录" : log.EventName,
				PlcValue = log.PlcValue,
				DurationSeconds = log.DurationSeconds,
				Note = log.Message,
				MeasuredWeight = log.MeasuredWeight,
				WeightStepKey = log.WeightStepKey,
				HomeLogLevel = level,
				HomeLogSource = HomeLogSource.Process,
				HomeLogKind = kind,
				HomeLogMessage = "流程：" + log.Message,
				PersistHomeLogToFile = false
			});
			return;
		}

		context.AddLog(level, HomeLogSource.Process, kind, "流程：" + log.Message, null, false);
	}
}

/// <summary>
/// 作用
/// 首页日志写入上下文
internal sealed class HomeLogWriteContext
{
	/// <summary>
	/// 当前线程是否为UI线程
	/// </summary>
	/// By:ChengLei
	public required Func<bool> IsOnUiThread { get; init; }

	/// <summary>
	/// 切回UI线程执行的委托
	/// </summary>
	/// By:ChengLei
	public required Action<Action> RunOnUiThread { get; init; }

	/// <summary>
	/// 首页日志控制器
	/// </summary>
	/// By:ChengLei
	public required HomeLogController Controller { get; init; }

	/// <summary>
	/// 生成当前日志筛选状态的委托
	/// </summary>
	/// By:ChengLei
	public required Func<HomeLogFilterState> CreateFilterState { get; init; }

	/// <summary>
	/// 应用日志计数快照的委托
	/// </summary>
	/// By:ChengLei
	public required Action<HomeLogCounters> ApplyLogCounters { get; init; }
}

/// <summary>
/// 作用
/// 首页流程日志映射上下文
internal sealed class HomeWorkflowLogIngressContext
{
	/// <summary>
	/// 获取当前日志批次号文本的委托
	/// </summary>
	/// By:ChengLei
	public required Func<string> GetCurrentBatchNoForLogging { get; init; }

	/// <summary>
	/// 入队采血管流程事件的委托
	/// </summary>
	/// By:ChengLei
	public required Action<TubeProcessEvent> EnqueueTubeProcessEvent { get; init; }

	/// <summary>
	/// 首页日志写入委托
	/// </summary>
	/// By:ChengLei
	public required Action<HomeLogLevel, HomeLogSource, HomeLogKind, string, int?, bool> AddLog { get; init; }
}
