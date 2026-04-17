using System;
using System.IO;
using System.Linq;
using Blood_Alcohol.Logs;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页日志输出与批次管理协调器
internal sealed class HomeLogOutputCoordinator
{
	private const string ExportPathConfigFileName = "HomeExportPathConfig.json";
	private const string BatchCounterConfigFileName = "HomeLogBatchCounterConfig.json";
	private readonly ConfigService<HomeExportPathConfig> _exportPathConfigService = new(ExportPathConfigFileName);
	private readonly ConfigService<HomeLogBatchCounterConfig> _batchCounterConfigService = new(BatchCounterConfigFileName);

	/// <summary>
	/// 初始化首页日志输出与批次管理协调器
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由 HomeViewModel 构造时创建 统一管理首页日志目录 批次号与单管轨迹输出
	/// </remarks>
	public HomeLogOutputCoordinator()
	{
		LogTool = new LogTool();
	}

	/// <summary>
	/// 当前日志工具实例
	/// </summary>
	/// By:ChengLei
	public LogTool LogTool { get; private set; }

	/// <summary>
	/// 当前导出目录
	/// </summary>
	/// By:ChengLei
	public string ExportDirectory { get; private set; } = string.Empty;

	/// <summary>
	/// 默认项目日志目录
	/// </summary>
	/// By:ChengLei
	public string DefaultProjectLogsDirectory => GetDefaultProjectLogsDirectory();

	/// <summary>
	/// 初始化导出目录并恢复上次保存的日志输出位置
	/// </summary>
	/// By:ChengLei
	/// <returns>返回初始化后的日志输出状态</returns>
	/// <remarks>
	/// 若配置缺失或目录非法 将回退到项目根目录下的 Logs 目录
	/// </remarks>
	public HomeLogOutputState Initialize()
	{
		HomeExportPathConfig config = _exportPathConfigService.Load() ?? new HomeExportPathConfig();
		string directoryPath = string.IsNullOrWhiteSpace(config.ExportDirectory)
			? DefaultProjectLogsDirectory
			: config.ExportDirectory;
		return ApplyExportDirectory(directoryPath, saveToConfig: true);
	}

	/// <summary>
	/// 应用导出目录并按需保存到配置
	/// </summary>
	/// By:ChengLei
	/// <param name="directoryPath">目标目录路径</param>
	/// <param name="saveToConfig">是否持久化到配置文件</param>
	/// <returns>返回应用后的日志输出状态</returns>
	/// <remarks>
	/// 该方法会创建目录并替换当前 LogTool 实例
	/// </remarks>
	public HomeLogOutputState ApplyExportDirectory(string directoryPath, bool saveToConfig)
	{
		string normalizedPath = NormalizePathOrEmpty(directoryPath);
		if (string.IsNullOrWhiteSpace(normalizedPath))
		{
			normalizedPath = DefaultProjectLogsDirectory;
		}

		Directory.CreateDirectory(normalizedPath);
		ExportDirectory = normalizedPath;
		LogTool = new LogTool(normalizedPath);

		if (saveToConfig)
		{
			_exportPathConfigService.Save(new HomeExportPathConfig
			{
				ExportDirectory = normalizedPath
			});
		}

		return new HomeLogOutputState(normalizedPath, LogTool);
	}

	/// <summary>
	/// 获取当前日志使用的批次号文本
	/// </summary>
	/// By:ChengLei
	/// <param name="currentBatchNo">当前运行中的批次号</param>
	/// <returns>返回当前批次号 未开始时返回占位批次名</returns>
	/// <remarks>
	/// 由首页日志 流程日志和导出逻辑统一调用
	/// </remarks>
	public string GetCurrentBatchNoForLogging(string currentBatchNo)
	{
		return string.IsNullOrWhiteSpace(currentBatchNo) ? "批次_未开始" : currentBatchNo;
	}

	/// <summary>
	/// 为新一轮成功启动的检测流程分配批次号
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当天新的批次号文本</returns>
	/// <remarks>
	/// 仅在开始检测成功后调用 校验失败不会占用批次序号
	/// </remarks>
	public string AllocateNextBatchNo()
	{
		string today = DateTime.Now.ToString("yyyy-MM-dd");
		HomeLogBatchCounterConfig config = _batchCounterConfigService.Load() ?? new HomeLogBatchCounterConfig();
		int nextNumber = string.Equals(config.LastBatchDate, today, StringComparison.Ordinal)
			? Math.Max(0, config.LastBatchNumber) + 1
			: 1;

		config.LastBatchDate = today;
		config.LastBatchNumber = nextNumber;
		_batchCounterConfigService.Save(config);
		return $"批次_{nextNumber:000}";
	}

	/// <summary>
	/// 追加一条单管轨迹CSV记录
	/// </summary>
	/// By:ChengLei
	/// <param name="currentBatchNo">当前运行中的批次号</param>
	/// <param name="timestamp">记录时间</param>
	/// <param name="batchNo">事件批次号</param>
	/// <param name="context">采血管上下文</param>
	/// <param name="headspaceCode">顶空瓶编号</param>
	/// <param name="processName">工序名称</param>
	/// <param name="eventName">事件名称</param>
	/// <param name="plcValue">PLC值文本</param>
	/// <param name="durationSeconds">持续时长秒</param>
	/// <param name="note">备注文本</param>
	/// <remarks>
	/// 由首页采血管事件串行处理流程调用 用于生成单管轨迹CSV
	/// </remarks>
	public void AppendTubeTraceRecord(
		string currentBatchNo,
		DateTime timestamp,
		string batchNo,
		TubeContext context,
		string headspaceCode,
		string processName,
		string eventName,
		string plcValue,
		double? durationSeconds,
		string note)
	{
		if (context == null || context.TubeIndex <= 0)
		{
			return;
		}

		LogTool.AppendTubeTraceCsv(new TubeTraceCsvRecord
		{
			Timestamp = timestamp,
			BatchNo = string.IsNullOrWhiteSpace(batchNo) ? GetCurrentBatchNoForLogging(currentBatchNo) : batchNo,
			TubeIndex = context.TubeIndex,
			TubeCode = context.TubeCode,
			HeadspaceCode = headspaceCode,
			ProcessName = processName,
			EventName = eventName,
			PlcValue = plcValue,
			DurationSeconds = durationSeconds,
			Note = note
		});
	}

	/// <summary>
	/// 规范化路径文本并转换为可比较格式
	/// </summary>
	/// By:ChengLei
	/// <param name="path">待规范化的路径文本</param>
	/// <returns>返回规范化后的路径 无效时返回空字符串</returns>
	/// <remarks>
	/// 由导出目录处理流程调用 统一比较与持久化格式
	/// </remarks>
	private static string NormalizePathOrEmpty(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		try
		{
			return Path.GetFullPath(path.Trim());
		}
		catch
		{
			return string.Empty;
		}
	}

	/// <summary>
	/// 获取项目默认日志目录路径
	/// </summary>
	/// By:ChengLei
	/// <returns>返回默认日志目录绝对路径</returns>
	/// <remarks>
	/// 由导出目录初始化与目录选择流程调用
	/// </remarks>
	private static string GetDefaultProjectLogsDirectory()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		for (DirectoryInfo? directoryInfo = new DirectoryInfo(baseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (directoryInfo.EnumerateFiles("*.csproj").Any())
			{
				return Path.Combine(directoryInfo.FullName, "Logs");
			}
		}

		return Path.Combine(baseDirectory, "Logs");
	}
}

/// <summary>
/// 作用
/// 首页日志输出状态
internal readonly record struct HomeLogOutputState(string ExportDirectory, LogTool LogTool);
