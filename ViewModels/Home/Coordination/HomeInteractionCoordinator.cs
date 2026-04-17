using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页交互协调器
internal sealed class HomeInteractionCoordinator
{
	/// <summary>
	/// 处理采血管槽位点击后的首页交互
	/// </summary>
	/// By:ChengLei
	/// <param name="context">槽位点击上下文</param>
	/// <remarks>
	/// 运行中仅切换详情目标 未运行时同时更新采血管数量
	/// </remarks>
	public void HandleTubeSlotClick(HomeTubeSlotClickContext context)
	{
		if (context.Slot == null)
		{
			return;
		}

		int tubeIndex = context.Slot.Number;
		context.SetSelectedDetailTubeIndex(tubeIndex);
		if (!context.IsDetectionStarted)
		{
			context.ApplyCount(tubeIndex, false);
		}

		context.RefreshSelectedTubeDetails();
	}

	/// <summary>
	/// 选择日志导出目录并返回交互结果
	/// </summary>
	/// By:ChengLei
	/// <param name="context">导出目录选择上下文</param>
	/// <returns>返回目录选择结果</returns>
	/// <remarks>
	/// 取消选择时返回未变更结果 发生异常时返回错误信息供首页统一写日志
	/// </remarks>
	public HomeExportDirectorySelectionResult SelectExportDirectory(HomeExportDirectorySelectionContext context)
	{
		try
		{
			OpenFolderDialog dialog = new OpenFolderDialog
			{
				Title = "选择日志导出目录",
				InitialDirectory = Directory.Exists(context.CurrentExportDirectory) ? context.CurrentExportDirectory : context.DefaultExportDirectory,
				FolderName = context.CurrentExportDirectory,
				Multiselect = false
			};

			if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
			{
				return new HomeExportDirectorySelectionResult(false, false, null, null);
			}

			HomeLogOutputState state = context.ApplyDirectory(dialog.FolderName);
			return new HomeExportDirectorySelectionResult(true, true, state, null);
		}
		catch (Exception ex)
		{
			return new HomeExportDirectorySelectionResult(false, false, null, ex.Message);
		}
	}

	/// <summary>
	/// 解析日志导出结果并返回首页需要展示的输出
	/// </summary>
	/// By:ChengLei
	/// <param name="exportedFiles">导出的文件路径集合</param>
	/// <param name="exportDirectory">当前导出目录</param>
	/// <param name="legacyMode">是否按旧版兼容格式返回最后导出路径</param>
	/// <returns>返回日志导出结果</returns>
	/// <remarks>
	/// 无导出文件时返回空结果 由首页统一决定写警告日志
	/// </remarks>
	public HomeLogExportResult BuildLogExportResult(IReadOnlyList<string> exportedFiles, string exportDirectory, bool legacyMode)
	{
		if (exportedFiles.Count == 0)
		{
			return new HomeLogExportResult(false, string.Empty, string.Empty);
		}

		if (legacyMode)
		{
			string legacyPath = exportedFiles.Count == 1
				? exportedFiles[0]
				: $"共导出 {exportedFiles.Count} 个文件，示例：{exportedFiles[0]}";
			return new HomeLogExportResult(true, legacyPath, legacyPath);
		}

		string message = exportedFiles.Count == 1
			? exportedFiles[0]
			: $"共导出 {exportedFiles.Count} 个文件，示例：{exportedFiles[0]}";
		return new HomeLogExportResult(true, exportDirectory, message);
	}
}

/// <summary>
/// 作用
/// 首页采血管槽位点击上下文
internal sealed class HomeTubeSlotClickContext
{
	public required RackSlotItemViewModel? Slot { get; init; }
	public required bool IsDetectionStarted { get; init; }
	public required Action<int> SetSelectedDetailTubeIndex { get; init; }
	public required Action<int, bool> ApplyCount { get; init; }
	public required Action RefreshSelectedTubeDetails { get; init; }
}

/// <summary>
/// 作用
/// 首页导出目录选择上下文
internal sealed class HomeExportDirectorySelectionContext
{
	public required string CurrentExportDirectory { get; init; }
	public required string DefaultExportDirectory { get; init; }
	public required Func<string, HomeLogOutputState> ApplyDirectory { get; init; }
}

/// <summary>
/// 作用
/// 首页导出目录选择结果
internal readonly record struct HomeExportDirectorySelectionResult(
	bool Applied,
	bool Changed,
	HomeLogOutputState? State,
	string? Error);

/// <summary>
/// 作用
/// 首页日志导出结果
internal readonly record struct HomeLogExportResult(
	bool HasExportedFiles,
	string LastExportPath,
	string SuccessMessage);
