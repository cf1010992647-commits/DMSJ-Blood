using System;
using System.Collections.ObjectModel;
using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页条件展示构建器。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页把工艺参数配置转换为条件区可绑定的显示项。
/// </remarks>
internal static class HomeConditionPresenter
{
	/// <summary>
	/// 按流程参数配置刷新首页条件项。
	/// </summary>
	/// By:ChengLei
	/// <param name="conditions">首页条件集合。</param>
	/// <param name="config">流程参数配置。</param>
	/// <remarks>
	/// 条件行数量不匹配时重建集合，数量匹配时仅更新显示值。
	/// </remarks>
	public static void Apply(ObservableCollection<ConditionItemViewModel> conditions, ProcessParameterConfig config)
	{
		if (conditions.Count != 7)
		{
			conditions.Clear();
			conditions.Add(new ConditionItemViewModel("加热箱温度", config.HeatingBoxTemperature.ToString("F1"), "°C"));
			conditions.Add(new ConditionItemViewModel("定量环温度", config.QuantitativeLoopTemperature.ToString("F1"), "°C"));
			conditions.Add(new ConditionItemViewModel("传输线温度", config.TransferLineTemperature.ToString("F1"), "°C"));
			conditions.Add(new ConditionItemViewModel("样品瓶平衡", Math.Max(0, config.ShakeDurationSeconds).ToString(), "s"));
			conditions.Add(new ConditionItemViewModel("样品瓶加压", FormatSecondsFrom100ms(config.SampleBottlePressureTime100ms), "s"));
			conditions.Add(new ConditionItemViewModel("定量环平衡", FormatSecondsFrom100ms(config.QuantitativeLoopBalanceTime100ms), "s"));
			conditions.Add(new ConditionItemViewModel("进样时间", "0", "s"));
			return;
		}

		conditions[0].Value = config.HeatingBoxTemperature.ToString("F1");
		conditions[1].Value = config.QuantitativeLoopTemperature.ToString("F1");
		conditions[2].Value = config.TransferLineTemperature.ToString("F1");
		conditions[3].Value = Math.Max(0, config.ShakeDurationSeconds).ToString();
		conditions[4].Value = FormatSecondsFrom100ms(config.SampleBottlePressureTime100ms);
		conditions[5].Value = FormatSecondsFrom100ms(config.QuantitativeLoopBalanceTime100ms);
		conditions[6].Value = "0";
	}

	/// <summary>
	/// 将 100ms 单位时间格式化为秒文本。
	/// </summary>
	/// By:ChengLei
	/// <param name="time100ms">以 100ms 为单位的时间。</param>
	/// <returns>返回首页显示用秒文本。</returns>
	/// <remarks>
	/// 由条件展示刷新流程调用。
	/// </remarks>
	private static string FormatSecondsFrom100ms(int time100ms)
	{
		double seconds = Math.Max(0, time100ms) / 10.0;
		return seconds.ToString("0.##");
	}
}
