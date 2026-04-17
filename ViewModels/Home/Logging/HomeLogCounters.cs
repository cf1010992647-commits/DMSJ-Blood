namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志级别计数快照。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由日志控制器返回给 HomeViewModel，用于更新首页计数绑定属性。
/// </remarks>
internal readonly struct HomeLogCounters
{
	/// <summary>
	/// 初始化首页日志计数快照。
	/// </summary>
	/// By:ChengLei
	/// <param name="infoCount">信息日志数量。</param>
	/// <param name="warningCount">警告日志数量。</param>
	/// <param name="errorCount">错误日志数量。</param>
	/// <remarks>
	/// 由日志控制器在重新统计时创建。
	/// </remarks>
	public HomeLogCounters(int infoCount, int warningCount, int errorCount)
	{
		InfoCount = infoCount;
		WarningCount = warningCount;
		ErrorCount = errorCount;
	}

	/// <summary>
	/// 信息日志数量。
	/// </summary>
	/// By:ChengLei
	public int InfoCount { get; }

	/// <summary>
	/// 警告日志数量。
	/// </summary>
	/// By:ChengLei
	public int WarningCount { get; }

	/// <summary>
	/// 错误日志数量。
	/// </summary>
	/// By:ChengLei
	public int ErrorCount { get; }
}
