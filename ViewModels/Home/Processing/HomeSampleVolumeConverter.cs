using System;
using System.Globalization;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页采血管重量转体积换算器
internal sealed class HomeSampleVolumeConverter
{
	private readonly ConfigService<WeightToZCalibrationConfig> _configService;

	/// <summary>
	/// 初始化首页采血管重量转体积换算器
	/// </summary>
	/// By:ChengLei
	/// <param name="configService">重量到体积标定配置服务</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建 供采血管流程状态机通过委托调用
	/// </remarks>
	public HomeSampleVolumeConverter(ConfigService<WeightToZCalibrationConfig> configService)
	{
		_configService = configService ?? throw new ArgumentNullException(nameof(configService));
	}

	/// <summary>
	/// 根据称重值换算首页体积显示文本
	/// </summary>
	/// By:ChengLei
	/// <param name="measuredWeight">称重值 克</param>
	/// <returns>返回换算是否成功和体积文本</returns>
	/// <remarks>
	/// 优先读取最新保存的标定配置 未配置有效系数时返回失败
	/// </remarks>
	public (bool Success, string SampleVolume) BuildSampleVolumeFromWeight(double measuredWeight)
	{
		if (!TryGetMicroliterPerWeight(out double microliterPerWeight))
		{
			return (false, "0");
		}

		double microliter = Math.Max(0.0, measuredWeight * microliterPerWeight);
		return (true, microliter.ToString("F1", CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// 读取重量到微升系数配置并校验有效性
	/// </summary>
	/// By:ChengLei
	/// <param name="microliterPerWeight">输出重量到微升系数</param>
	/// <returns>返回系数是否有效</returns>
	/// <remarks>
	/// 由 BuildSampleVolumeFromWeight 调用 读取异常时统一返回无效
	/// </remarks>
	private bool TryGetMicroliterPerWeight(out double microliterPerWeight)
	{
		microliterPerWeight = 0.0;
		try
		{
			WeightToZCalibrationConfig config = _configService.Load() ?? new WeightToZCalibrationConfig();
			if (!config.HasMicroliterCoefficient || Math.Abs(config.MicroliterPerWeight) <= 1E-07)
			{
				return false;
			}

			microliterPerWeight = config.MicroliterPerWeight;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
