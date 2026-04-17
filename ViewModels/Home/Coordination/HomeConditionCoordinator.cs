using System;
using System.Collections.ObjectModel;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页条件配置展示协调器
internal sealed class HomeConditionCoordinator
{
	private readonly ConfigService<ProcessParameterConfig> _configService;

	/// <summary>
	/// 初始化首页条件配置展示协调器
	/// </summary>
	/// By:ChengLei
	/// <param name="configService">流程参数配置服务</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建 用于统一加载和刷新首页条件展示
	/// </remarks>
	public HomeConditionCoordinator(ConfigService<ProcessParameterConfig> configService)
	{
		_configService = configService ?? throw new ArgumentNullException(nameof(configService));
	}

	/// <summary>
	/// 将当前流程参数配置应用到首页条件项集合
	/// </summary>
	/// By:ChengLei
	/// <param name="conditions">首页条件项集合</param>
	/// <remarks>
	/// 读取配置失败时回退默认参数 避免配置文件异常影响首页加载
	/// </remarks>
	public void ApplyTo(ObservableCollection<ConditionItemViewModel> conditions)
	{
		HomeConditionPresenter.Apply(conditions, LoadSafely());
	}

	/// <summary>
	/// 安全读取流程参数配置
	/// </summary>
	/// By:ChengLei
	/// <returns>返回可用于首页展示的流程参数配置</returns>
	/// <remarks>
	/// 由首页条件初始化与刷新流程调用 读取异常时回退默认对象
	/// </remarks>
	public ProcessParameterConfig LoadSafely()
	{
		try
		{
			return _configService.Load() ?? new ProcessParameterConfig();
		}
		catch
		{
			return new ProcessParameterConfig();
		}
	}
}
