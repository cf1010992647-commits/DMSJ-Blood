using System;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.Models;

/// <summary>
/// 表示单根采血管在当前批次内的完整上下文。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页事件串行归档流程维护，用于统一承载采血管编码、顶空瓶编码和详情显示数据。
/// </remarks>
internal sealed class TubeContext
{
	/// <summary>
	/// 采血管序号。
	/// </summary>
	/// By:ChengLei
	public int TubeIndex { get; set; }

	/// <summary>
	/// 采血管编码。
	/// </summary>
	/// By:ChengLei
	public string TubeCode { get; set; } = "未识别";

	/// <summary>
	/// 顶空瓶A编码。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceACode { get; set; } = "未识别+A";

	/// <summary>
	/// 顶空瓶B编码。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceBCode { get; set; } = "未识别+B";

	/// <summary>
	/// 是否已建立扫码绑定。
	/// </summary>
	/// By:ChengLei
	public bool IsRecognized { get; set; }

	/// <summary>
	/// 是否为未识别占位上下文。
	/// </summary>
	/// By:ChengLei
	public bool IsPlaceholder { get; set; } = true;

	/// <summary>
	/// 是否已完成。
	/// </summary>
	/// By:ChengLei
	public bool IsCompleted { get; set; }

	/// <summary>
	/// 当前采血管体积显示文本。
	/// </summary>
	/// By:ChengLei
	public string SampleVolume { get; set; } = "0";

	/// <summary>
	/// 顶空瓶A样品重量文本。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceASampleWeight { get; set; } = "0.0";

	/// <summary>
	/// 顶空瓶A叔丁醇重量文本。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceAButanolWeight { get; set; } = "0.0";

	/// <summary>
	/// 顶空瓶B样品重量文本。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceBSampleWeight { get; set; } = "0.0";

	/// <summary>
	/// 顶空瓶B叔丁醇重量文本。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceBButanolWeight { get; set; } = "0.0";

	/// <summary>
	/// 最近工序名称。
	/// </summary>
	/// By:ChengLei
	public string LatestProcessName { get; set; } = string.Empty;

	/// <summary>
	/// 最近事件名称。
	/// </summary>
	/// By:ChengLei
	public string LatestEventName { get; set; } = string.Empty;

	/// <summary>
	/// 最近更新时间。
	/// </summary>
	/// By:ChengLei
	public DateTime LastUpdatedAt { get; set; } = DateTime.MinValue;
}

/// <summary>
/// 表示待串行处理的一条采血管流程事件。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由 WorkflowEngine 和首页寄存器监控统一投递，最终用于更新上下文、首页详情与CSV。
/// </remarks>
internal sealed class TubeProcessEvent
{
	/// <summary>
	/// 事件时间戳。
	/// </summary>
	/// By:ChengLei
	public DateTime Timestamp { get; set; } = DateTime.Now;

	/// <summary>
	/// 批次号文本。
	/// </summary>
	/// By:ChengLei
	public string BatchNo { get; set; } = string.Empty;

	/// <summary>
	/// 采血管序号。
	/// </summary>
	/// By:ChengLei
	public int TubeIndex { get; set; }

	/// <summary>
	/// 顶空瓶标识。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceBottleTag { get; set; } = string.Empty;

	/// <summary>
	/// 扫码编码。
	/// </summary>
	/// By:ChengLei
	public string ScanCode { get; set; } = string.Empty;

	/// <summary>
	/// 工序名称。
	/// </summary>
	/// By:ChengLei
	public string ProcessName { get; set; } = string.Empty;

	/// <summary>
	/// 事件名称。
	/// </summary>
	/// By:ChengLei
	public string EventName { get; set; } = string.Empty;

	/// <summary>
	/// PLC值文本。
	/// </summary>
	/// By:ChengLei
	public string PlcValue { get; set; } = string.Empty;

	/// <summary>
	/// 持续时长秒。
	/// </summary>
	/// By:ChengLei
	public double? DurationSeconds { get; set; }

	/// <summary>
	/// 备注文本。
	/// </summary>
	/// By:ChengLei
	public string Note { get; set; } = string.Empty;

	/// <summary>
	/// 称重值。
	/// </summary>
	/// By:ChengLei
	public double? MeasuredWeight { get; set; }

	/// <summary>
	/// 称重步骤键。
	/// </summary>
	/// By:ChengLei
	public string WeightStepKey { get; set; } = string.Empty;

	/// <summary>
	/// 首页日志级别。
	/// </summary>
	/// By:ChengLei
	public HomeLogLevel HomeLogLevel { get; set; } = HomeLogLevel.Info;

	/// <summary>
	/// 首页日志来源。
	/// </summary>
	/// By:ChengLei
	public HomeLogSource HomeLogSource { get; set; } = HomeLogSource.Process;

	/// <summary>
	/// 首页日志类型。
	/// </summary>
	/// By:ChengLei
	public HomeLogKind HomeLogKind { get; set; } = HomeLogKind.Detection;

	/// <summary>
	/// 首页日志消息。
	/// </summary>
	/// By:ChengLei
	public string HomeLogMessage { get; set; } = string.Empty;

	/// <summary>
	/// 首页日志是否落文件。
	/// </summary>
	/// By:ChengLei
	public bool PersistHomeLogToFile { get; set; }
}
