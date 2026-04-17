using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页料架视觉状态构建器。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页把采血管、顶空瓶和针头运行状态转换为槽位颜色。
/// </remarks>
internal static class HomeRackVisualPresenter
{
	private static readonly Brush ActiveSlotFill = BrushFromHex("#005ECC");

	private static readonly Brush ActiveSlotText = Brushes.White;

	private static readonly Brush IdleSlotFill = Brushes.White;

	private static readonly Brush IdleSlotText = BrushFromHex("#0F172A");

	private static readonly Brush RunningSlotFill = BrushFromHex("#7C3AED");

	private static readonly Brush CompletedSlotFill = BrushFromHex("#16A34A");

	private static readonly Brush NeedleUsedFill = Brushes.Black;

	private static readonly Brush NeedleIdleFill = Brushes.White;

	/// <summary>
	/// 刷新采血管、顶空瓶和针头槽位颜色。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeSlots">采血管槽位集合。</param>
	/// <param name="headspaceSlots">顶空瓶槽位集合。</param>
	/// <param name="needleHeadSlots">针头槽位集合。</param>
	/// <param name="selectedTubeCount">当前选择采血管数量。</param>
	/// <param name="selectedHeadspaceCount">当前选择顶空瓶数量。</param>
	/// <param name="tubeRunningSlots">运行中的采血管槽位。</param>
	/// <param name="tubeCompletedSlots">已完成的采血管槽位。</param>
	/// <param name="headspaceRunningSlots">运行中的顶空瓶槽位。</param>
	/// <param name="headspaceCompletedSlots">已完成的顶空瓶槽位。</param>
	/// <param name="usedNeedleHeadCount">已使用针头数量。</param>
	/// <remarks>
	/// 由首页数量变化和料架状态变化后调用。
	/// </remarks>
	public static void UpdateRackVisuals(
		IEnumerable<RackSlotItemViewModel> tubeSlots,
		IEnumerable<RackSlotItemViewModel> headspaceSlots,
		IEnumerable<RackSlotItemViewModel> needleHeadSlots,
		int selectedTubeCount,
		int selectedHeadspaceCount,
		IReadOnlySet<int> tubeRunningSlots,
		IReadOnlySet<int> tubeCompletedSlots,
		IReadOnlySet<int> headspaceRunningSlots,
		IReadOnlySet<int> headspaceCompletedSlots,
		int usedNeedleHeadCount)
	{
		UpdateSampleSlots(tubeSlots, selectedTubeCount, tubeRunningSlots, tubeCompletedSlots);
		UpdateSampleSlots(headspaceSlots, selectedHeadspaceCount, headspaceRunningSlots, headspaceCompletedSlots);
		UpdateNeedleHeadSlots(needleHeadSlots, usedNeedleHeadCount);
	}

	/// <summary>
	/// 构建采血管槽位集合。
	/// </summary>
	/// By:ChengLei
	/// <param name="target">目标槽位集合。</param>
	/// <param name="count">槽位数量。</param>
	/// <remarks>
	/// 由首页构造时调用。
	/// </remarks>
	public static void BuildSampleSlots(ObservableCollection<RackSlotItemViewModel> target, int count)
	{
		target.Clear();
		for (int index = 1; index <= count; index++)
		{
			target.Add(new RackSlotItemViewModel
			{
				Number = index,
				Fill = IdleSlotFill,
				Foreground = IdleSlotText
			});
		}
	}

	/// <summary>
	/// 构建针头槽位集合。
	/// </summary>
	/// By:ChengLei
	/// <param name="target">目标槽位集合。</param>
	/// <param name="count">槽位数量。</param>
	/// <remarks>
	/// 由首页构造时调用。
	/// </remarks>
	public static void BuildNeedleHeadSlots(ObservableCollection<RackSlotItemViewModel> target, int count)
	{
		target.Clear();
		for (int index = 1; index <= count; index++)
		{
			target.Add(new RackSlotItemViewModel
			{
				Number = index,
				Fill = NeedleIdleFill,
				Foreground = IdleSlotText
			});
		}
	}

	/// <summary>
	/// 刷新样本槽位颜色。
	/// </summary>
	/// By:ChengLei
	/// <param name="slots">样本槽位集合。</param>
	/// <param name="selectedCount">当前选中数量。</param>
	/// <param name="runningSlots">运行中的槽位集合。</param>
	/// <param name="completedSlots">已完成槽位集合。</param>
	/// <remarks>
	/// 由 UpdateRackVisuals 调用。
	/// </remarks>
	private static void UpdateSampleSlots(
		IEnumerable<RackSlotItemViewModel> slots,
		int selectedCount,
		IReadOnlySet<int> runningSlots,
		IReadOnlySet<int> completedSlots)
	{
		foreach (RackSlotItemViewModel slot in slots)
		{
			if (slot.Number > selectedCount)
			{
				ApplyBrush(slot, IdleSlotFill, IdleSlotText);
				continue;
			}

			if (completedSlots.Contains(slot.Number))
			{
				ApplyBrush(slot, CompletedSlotFill, ActiveSlotText);
				continue;
			}

			if (runningSlots.Contains(slot.Number))
			{
				ApplyBrush(slot, RunningSlotFill, ActiveSlotText);
				continue;
			}

			ApplyBrush(slot, ActiveSlotFill, ActiveSlotText);
		}
	}

	/// <summary>
	/// 刷新针头槽位颜色。
	/// </summary>
	/// By:ChengLei
	/// <param name="slots">针头槽位集合。</param>
	/// <param name="usedNeedleHeadCount">已使用针头数量。</param>
	/// <remarks>
	/// 由 UpdateRackVisuals 调用。
	/// </remarks>
	private static void UpdateNeedleHeadSlots(IEnumerable<RackSlotItemViewModel> slots, int usedNeedleHeadCount)
	{
		foreach (RackSlotItemViewModel slot in slots)
		{
			bool isUsed = slot.Number <= usedNeedleHeadCount;
			ApplyBrush(slot, isUsed ? NeedleUsedFill : NeedleIdleFill, isUsed ? ActiveSlotText : IdleSlotText);
		}
	}

	/// <summary>
	/// 应用槽位画刷。
	/// </summary>
	/// By:ChengLei
	/// <param name="slot">目标槽位。</param>
	/// <param name="fill">填充画刷。</param>
	/// <param name="foreground">文字画刷。</param>
	/// <remarks>
	/// 由槽位刷新流程调用，集中设置颜色属性。
	/// </remarks>
	private static void ApplyBrush(RackSlotItemViewModel slot, Brush fill, Brush foreground)
	{
		slot.Fill = fill;
		slot.Foreground = foreground;
	}

	/// <summary>
	/// 根据十六进制颜色创建画刷。
	/// </summary>
	/// By:ChengLei
	/// <param name="hex">十六进制颜色文本。</param>
	/// <returns>返回对应颜色画刷。</returns>
	/// <remarks>
	/// 由静态颜色字段初始化时调用。
	/// </remarks>
	private static Brush BrushFromHex(string hex)
	{
		object? brushObject = new BrushConverter().ConvertFromString(hex);
		if (brushObject is Brush brush)
		{
			return brush;
		}

		throw new InvalidOperationException("Invalid brush color: " + hex);
	}
}
