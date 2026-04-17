using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Blood_Alcohol.Logs;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志集合控制器。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 负责首页日志追加、筛选、计数和导出记录构建，避免 HomeViewModel 直接维护日志集合细节。
/// </remarks>
internal sealed class HomeLogController
{
	private const int MaxLogs = 2000;
	private readonly List<HomeLogItemViewModel> _allLogs = new List<HomeLogItemViewModel>();
	private readonly Func<LogTool> _logToolProvider;
	private readonly Func<string> _batchNoProvider;

	/// <summary>
	/// 初始化首页日志集合控制器。
	/// </summary>
	/// By:ChengLei
	/// <param name="logToolProvider">日志工具提供委托。</param>
	/// <param name="batchNoProvider">批次号提供委托。</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建，运行期间通过委托获取最新日志工具和批次号。
	/// </remarks>
	public HomeLogController(Func<LogTool> logToolProvider, Func<string> batchNoProvider)
	{
		_logToolProvider = logToolProvider ?? throw new ArgumentNullException(nameof(logToolProvider));
		_batchNoProvider = batchNoProvider ?? throw new ArgumentNullException(nameof(batchNoProvider));
	}

	/// <summary>
	/// 当前可见日志集合。
	/// </summary>
	/// By:ChengLei
	public ObservableCollection<HomeLogItemViewModel> VisibleLogs { get; } = new ObservableCollection<HomeLogItemViewModel>();

	/// <summary>
	/// 清空全部日志和可见日志。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页默认日志初始化流程调用。
	/// </remarks>
	public void Clear()
	{
		_allLogs.Clear();
		VisibleLogs.Clear();
	}

	/// <summary>
	/// 追加首页日志并刷新筛选结果。
	/// </summary>
	/// By:ChengLei
	/// <param name="level">日志级别。</param>
	/// <param name="source">日志来源。</param>
	/// <param name="kind">日志类型。</param>
	/// <param name="message">日志消息。</param>
	/// <param name="tubeIndex">关联采血管序号。</param>
	/// <param name="persistToFile">是否写入本地日志文件。</param>
	/// <param name="filterState">当前日志筛选状态。</param>
	/// <returns>返回追加后的日志计数。</returns>
	/// <remarks>
	/// 由 HomeViewModel 的 AddLog 入口调用，确保追加、落盘、筛选和计数保持一致。
	/// </remarks>
	public HomeLogCounters Add(
		HomeLogLevel level,
		HomeLogSource source,
		HomeLogKind kind,
		string message,
		int? tubeIndex,
		bool persistToFile,
		HomeLogFilterState filterState)
	{
		DateTime now = DateTime.Now;
		int normalizedTubeIndex = Math.Max(0, tubeIndex.GetValueOrDefault());
		var logItem = new HomeLogItemViewModel
		{
			Timestamp = now,
			Time = now.ToString("yyyy-MM-dd HH:mm:ss"),
			Message = message,
			Level = level,
			Source = source,
			Kind = kind,
			TubeIndex = normalizedTubeIndex
		};

		_allLogs.Insert(0, logItem);
		if (_allLogs.Count > MaxLogs)
		{
			_allLogs.RemoveAt(_allLogs.Count - 1);
		}

		if (persistToFile)
		{
			WriteLogToFile(logItem);
		}

		return Refresh(filterState);
	}

	/// <summary>
	/// 按当前筛选状态刷新可见日志。
	/// </summary>
	/// By:ChengLei
	/// <param name="filterState">当前日志筛选状态。</param>
	/// <returns>返回刷新后的日志计数。</returns>
	/// <remarks>
	/// 由筛选开关变化和日志追加后调用。
	/// </remarks>
	public HomeLogCounters Refresh(HomeLogFilterState filterState)
	{
		List<HomeLogItemViewModel> visibleLogs = _allLogs
			.Where(log => HomeLogFilter.IsVisible(
				log,
				filterState.ShowSystemLogs,
				filterState.ShowProcessLogs,
				filterState.ShowDebugLogs,
				filterState.ShowHardwareLogs,
				filterState.ShowOperationLogs,
				filterState.ShowDetectionLogs,
				filterState.ShowInfoLogs,
				filterState.ShowWarningLogs,
				filterState.ShowErrorLogs))
			.ToList();

		VisibleLogs.Clear();
		foreach (HomeLogItemViewModel item in visibleLogs)
		{
			VisibleLogs.Add(item);
		}

		return GetCounters();
	}

	/// <summary>
	/// 导出当前可见日志。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回导出的文件路径列表。</returns>
	/// <remarks>
	/// 由首页导出日志命令调用，导出内容保持为当前可见日志。
	/// </remarks>
	public IReadOnlyList<string> ExportVisibleLogs()
	{
		List<LogCsvRecord> records = VisibleLogs.Select(x => new LogCsvRecord
		{
			Timestamp = x.Timestamp,
			TubeIndex = x.TubeIndex,
			Message = x.Message,
			LevelText = x.LevelText,
			SourceText = x.SourceText,
			KindText = x.KindText
		}).ToList();

		return _logToolProvider().ExportCsvByTube(records, _batchNoProvider(), DateTime.Now);
	}

	/// <summary>
	/// 重新统计日志级别数量。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回日志级别计数。</returns>
	/// <remarks>
	/// 由 Refresh 和 Add 调用，统计全部日志而不是仅可见日志。
	/// </remarks>
	public HomeLogCounters GetCounters()
	{
		return new HomeLogCounters(
			_allLogs.Count(x => x.Level == HomeLogLevel.Info),
			_allLogs.Count(x => x.Level == HomeLogLevel.Warning),
			_allLogs.Count(x => x.Level == HomeLogLevel.Error));
	}

	/// <summary>
	/// 将日志写入本地日志文件。
	/// </summary>
	/// By:ChengLei
	/// <param name="logItem">待写入的首页日志项。</param>
	/// <remarks>
	/// 由 Add 调用，保持原有日志落盘格式。
	/// </remarks>
	private void WriteLogToFile(HomeLogItemViewModel logItem)
	{
		_logToolProvider().WriteLog(
			logItem.SourceText,
			logItem.KindText,
			logItem.LevelText,
			"采血管:" + logItem.TubeText + " " + logItem.Message,
			_batchNoProvider(),
			logItem.TubeIndex,
			logItem.Timestamp);
	}
}
