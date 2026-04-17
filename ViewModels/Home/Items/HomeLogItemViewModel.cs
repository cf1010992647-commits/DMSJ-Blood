using System;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志显示项模型。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页日志列表绑定使用，负责把日志枚举转换为界面文本。
/// </remarks>
public class HomeLogItemViewModel
{
	/// <summary>
	/// 日志时间戳。
	/// </summary>
	/// By:ChengLei
	public DateTime Timestamp { get; set; } = DateTime.Now;

	/// <summary>
	/// 日志时间显示文本。
	/// </summary>
	/// By:ChengLei
	public string Time { get; set; } = string.Empty;

	/// <summary>
	/// 日志消息文本。
	/// </summary>
	/// By:ChengLei
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// 日志级别。
	/// </summary>
	/// By:ChengLei
	public HomeLogLevel Level { get; set; }

	/// <summary>
	/// 日志来源。
	/// </summary>
	/// By:ChengLei
	public HomeLogSource Source { get; set; }

	/// <summary>
	/// 日志类型。
	/// </summary>
	/// By:ChengLei
	public HomeLogKind Kind { get; set; }

	/// <summary>
	/// 关联采血管序号。
	/// </summary>
	/// By:ChengLei
	public int TubeIndex { get; set; }

	/// <summary>
	/// 日志级别显示文本。
	/// </summary>
	/// By:ChengLei
	public string LevelText => Level switch
	{
		HomeLogLevel.Warning => "警告",
		HomeLogLevel.Error => "错误",
		_ => "信息"
	};

	/// <summary>
	/// 日志来源显示文本。
	/// </summary>
	/// By:ChengLei
	public string SourceText => Source switch
	{
		HomeLogSource.System => "系统日志",
		HomeLogSource.Process => "进程日志",
		HomeLogSource.Debug => "调试日志",
		HomeLogSource.Hardware => "硬件日志",
		_ => "未知来源"
	};

	/// <summary>
	/// 日志类型显示文本。
	/// </summary>
	/// By:ChengLei
	public string KindText => Kind switch
	{
		HomeLogKind.Detection => "检测日志",
		_ => "普通操作日志"
	};

	/// <summary>
	/// 采血管序号显示文本。
	/// </summary>
	/// By:ChengLei
	public string TubeText => TubeIndex > 0 ? TubeIndex.ToString() : "运行";
}
