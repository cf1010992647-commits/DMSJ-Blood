namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志筛选状态快照。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由 HomeViewModel 在刷新日志可见集合时传入，避免日志控制器反向依赖页面属性。
/// </remarks>
internal sealed class HomeLogFilterState
{
	/// <summary>
	/// 是否显示系统日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowSystemLogs { get; init; }

	/// <summary>
	/// 是否显示进程日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowProcessLogs { get; init; }

	/// <summary>
	/// 是否显示调试日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowDebugLogs { get; init; }

	/// <summary>
	/// 是否显示硬件日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowHardwareLogs { get; init; }

	/// <summary>
	/// 是否显示普通操作日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowOperationLogs { get; init; }

	/// <summary>
	/// 是否显示检测日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowDetectionLogs { get; init; }

	/// <summary>
	/// 是否显示信息级别日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowInfoLogs { get; init; }

	/// <summary>
	/// 是否显示警告级别日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowWarningLogs { get; init; }

	/// <summary>
	/// 是否显示错误级别日志。
	/// </summary>
	/// By:ChengLei
	public bool ShowErrorLogs { get; init; }
}
