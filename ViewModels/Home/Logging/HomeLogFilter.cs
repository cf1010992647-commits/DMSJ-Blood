using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志筛选器。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页根据来源、类型和级别开关决定日志是否显示。
/// </remarks>
internal static class HomeLogFilter
{
	/// <summary>
	/// 判断日志是否满足当前筛选条件。
	/// </summary>
	/// By:ChengLei
	/// <param name="log">待判断的首页日志。</param>
	/// <param name="showSystemLogs">是否显示系统日志。</param>
	/// <param name="showProcessLogs">是否显示进程日志。</param>
	/// <param name="showDebugLogs">是否显示调试日志。</param>
	/// <param name="showHardwareLogs">是否显示硬件日志。</param>
	/// <param name="showOperationLogs">是否显示普通操作日志。</param>
	/// <param name="showDetectionLogs">是否显示检测日志。</param>
	/// <param name="showInfoLogs">是否显示信息级别日志。</param>
	/// <param name="showWarningLogs">是否显示警告级别日志。</param>
	/// <param name="showErrorLogs">是否显示错误级别日志。</param>
	/// <returns>返回日志是否应该显示。</returns>
	/// <remarks>
	/// 由首页可见日志刷新流程调用。
	/// </remarks>
	public static bool IsVisible(
		HomeLogItemViewModel log,
		bool showSystemLogs,
		bool showProcessLogs,
		bool showDebugLogs,
		bool showHardwareLogs,
		bool showOperationLogs,
		bool showDetectionLogs,
		bool showInfoLogs,
		bool showWarningLogs,
		bool showErrorLogs)
	{
		bool sourceVisible = log.Source switch
		{
			HomeLogSource.System => showSystemLogs,
			HomeLogSource.Process => showProcessLogs,
			HomeLogSource.Debug => showDebugLogs,
			HomeLogSource.Hardware => showHardwareLogs,
			_ => true
		};

		bool kindVisible = log.Kind switch
		{
			HomeLogKind.Operation => showOperationLogs,
			HomeLogKind.Detection => showDetectionLogs,
			_ => true
		};

		bool levelVisible = log.Level switch
		{
			HomeLogLevel.Info => showInfoLogs,
			HomeLogLevel.Warning => showWarningLogs,
			HomeLogLevel.Error => showErrorLogs,
			_ => true
		};

		return sourceVisible && kindVisible && levelVisible;
	}
}
