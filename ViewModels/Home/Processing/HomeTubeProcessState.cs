using System;
using System.Collections.Generic;
using System.Globalization;
using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页采血管流程上下文状态机。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 负责把流程事件应用到单管上下文，维护扫码、重量、体积和顶空瓶归属数据。
/// </remarks>
internal sealed class HomeTubeProcessState
{
	private readonly Dictionary<int, TubeContext> _tubeContexts = new Dictionary<int, TubeContext>();
	private readonly Func<double, (bool Success, string SampleVolume)> _sampleVolumeBuilder;

	/// <summary>
	/// 初始化采血管流程上下文状态机。
	/// </summary>
	/// By:ChengLei
	/// <param name="sampleVolumeBuilder">称重值到体积文本的转换委托。</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建，体积换算仍复用首页现有标定配置读取逻辑。
	/// </remarks>
	public HomeTubeProcessState(Func<double, (bool Success, string SampleVolume)> sampleVolumeBuilder)
	{
		_sampleVolumeBuilder = sampleVolumeBuilder ?? throw new ArgumentNullException(nameof(sampleVolumeBuilder));
	}

	/// <summary>
	/// 清空当前批次采血管上下文。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由新批次开始、停止检测和急停流程调用。
	/// </remarks>
	public void Clear()
	{
		_tubeContexts.Clear();
	}

	/// <summary>
	/// 尝试获取指定采血管上下文。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeIndex">采血管序号。</param>
	/// <param name="context">输出采血管上下文。</param>
	/// <returns>返回是否找到上下文。</returns>
	/// <remarks>
	/// 由首页详情刷新流程调用。
	/// </remarks>
	public bool TryGetContext(int tubeIndex, out TubeContext? context)
	{
		return _tubeContexts.TryGetValue(tubeIndex, out context);
	}

	/// <summary>
	/// 应用采血管事件并返回处理结果。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeEvent">待处理事件。</param>
	/// <param name="currentSelectedTubeIndex">当前详情区选中的采血管序号。</param>
	/// <returns>返回事件处理结果。</returns>
	/// <remarks>
	/// 由 HomeViewModel 的事件队列消费者串行调用。
	/// </remarks>
	public HomeTubeProcessResult ApplyEvent(TubeProcessEvent tubeEvent, int currentSelectedTubeIndex)
	{
		TubeContext context = GetOrCreateTubeContext(tubeEvent.TubeIndex);
		ApplyTubeEventToContext(context, tubeEvent);
		int selectedTubeIndex = currentSelectedTubeIndex <= 0 ? tubeEvent.TubeIndex : currentSelectedTubeIndex;
		string headspaceCode = ResolveHeadspaceCode(context, tubeEvent);
		return new HomeTubeProcessResult(context, headspaceCode, selectedTubeIndex);
	}

	/// <summary>
	/// 获取指定采血管序号对应的上下文，不存在时自动创建占位上下文。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeIndex">采血管序号。</param>
	/// <returns>返回对应上下文对象。</returns>
	/// <remarks>
	/// 由 ApplyEvent 调用，所有未识别事件也会落入自己的占位上下文。
	/// </remarks>
	private TubeContext GetOrCreateTubeContext(int tubeIndex)
	{
		if (!_tubeContexts.TryGetValue(tubeIndex, out TubeContext? context))
		{
			context = new TubeContext
			{
				TubeIndex = tubeIndex
			};
			_tubeContexts[tubeIndex] = context;
		}

		return context;
	}

	/// <summary>
	/// 将事件应用到采血管上下文。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">待更新上下文。</param>
	/// <param name="tubeEvent">事件对象。</param>
	/// <remarks>
	/// 由 ApplyEvent 调用，统一维护编码、A/B重量、体积和最近工序信息。
	/// </remarks>
	private void ApplyTubeEventToContext(TubeContext context, TubeProcessEvent tubeEvent)
	{
		if (!string.IsNullOrWhiteSpace(tubeEvent.ScanCode))
		{
			string scanCode = tubeEvent.ScanCode.Trim();
			context.TubeCode = scanCode;
			context.HeadspaceACode = scanCode + "+A";
			context.HeadspaceBCode = scanCode + "+B";
			context.IsRecognized = true;
			context.IsPlaceholder = false;
		}

		context.LatestProcessName = tubeEvent.ProcessName;
		context.LatestEventName = tubeEvent.EventName;
		context.LastUpdatedAt = tubeEvent.Timestamp;
		context.IsCompleted = string.Equals(tubeEvent.EventName, "完成", StringComparison.Ordinal);
		ApplyWeightToTubeContext(context, tubeEvent);
	}

	/// <summary>
	/// 根据称重事件刷新采血管上下文中的重量和体积数据。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">待更新上下文。</param>
	/// <param name="tubeEvent">包含称重信息的事件。</param>
	/// <remarks>
	/// 由 ApplyTubeEventToContext 调用，仅处理已携带称重值的流程事件。
	/// </remarks>
	private void ApplyWeightToTubeContext(TubeContext context, TubeProcessEvent tubeEvent)
	{
		if (!tubeEvent.MeasuredWeight.HasValue)
		{
			return;
		}

		string weightText = tubeEvent.MeasuredWeight.Value.ToString("F3", CultureInfo.InvariantCulture);
		switch (tubeEvent.WeightStepKey)
		{
			case "hs1_after_blood_weight":
				context.HeadspaceASampleWeight = weightText;
				break;
			case "hs1_after_butanol_weight":
				context.HeadspaceAButanolWeight = weightText;
				break;
			case "hs2_after_blood_weight":
				context.HeadspaceBSampleWeight = weightText;
				break;
			case "hs2_after_butanol_weight":
				context.HeadspaceBButanolWeight = weightText;
				break;
		}

		if (IsTubeWeightStep(tubeEvent.WeightStepKey))
		{
			(bool success, string sampleVolume) = _sampleVolumeBuilder(tubeEvent.MeasuredWeight.Value);
			if (success)
			{
				context.SampleVolume = sampleVolume;
			}
		}
	}

	/// <summary>
	/// 根据事件内容解析顶空瓶编码。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">当前采血管上下文。</param>
	/// <param name="tubeEvent">当前事件对象。</param>
	/// <returns>返回顶空瓶编码，非顶空瓶事件返回空。</returns>
	/// <remarks>
	/// 由 ApplyEvent 调用，优先使用显式 A/B 标识，其次回退到称重步骤键推断。
	/// </remarks>
	private static string ResolveHeadspaceCode(TubeContext context, TubeProcessEvent tubeEvent)
	{
		if (string.Equals(tubeEvent.HeadspaceBottleTag, "A", StringComparison.OrdinalIgnoreCase)
			|| IsHeadspaceAWeightStep(tubeEvent.WeightStepKey))
		{
			return context.HeadspaceACode;
		}

		if (string.Equals(tubeEvent.HeadspaceBottleTag, "B", StringComparison.OrdinalIgnoreCase)
			|| IsHeadspaceBWeightStep(tubeEvent.WeightStepKey))
		{
			return context.HeadspaceBCode;
		}

		return string.Empty;
	}

	/// <summary>
	/// 判断流程称重步骤是否属于顶空瓶A事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="weightStepKey">流程步骤键。</param>
	/// <returns>返回是否属于顶空瓶A。</returns>
	/// <remarks>
	/// 由顶空瓶编码解析流程调用。
	/// </remarks>
	private static bool IsHeadspaceAWeightStep(string? weightStepKey)
	{
		return !string.IsNullOrWhiteSpace(weightStepKey)
			&& weightStepKey.Contains("hs1", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 判断流程称重步骤是否属于顶空瓶B事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="weightStepKey">流程步骤键。</param>
	/// <returns>返回是否属于顶空瓶B。</returns>
	/// <remarks>
	/// 由顶空瓶编码解析流程调用。
	/// </remarks>
	private static bool IsHeadspaceBWeightStep(string? weightStepKey)
	{
		return !string.IsNullOrWhiteSpace(weightStepKey)
			&& weightStepKey.Contains("hs2", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 判断流程称重步骤是否属于采血管体积更新来源。
	/// </summary>
	/// By:ChengLei
	/// <param name="weightStepKey">流程称重步骤标识。</param>
	/// <returns>返回是否为采血管称重步骤。</returns>
	/// <remarks>
	/// 由称重事件处理流程调用。
	/// </remarks>
	private static bool IsTubeWeightStep(string? weightStepKey)
	{
		return string.Equals(weightStepKey, "tube_place_weight", StringComparison.Ordinal)
			|| string.Equals(weightStepKey, "tube_after_aspirate_weight", StringComparison.Ordinal);
	}
}
