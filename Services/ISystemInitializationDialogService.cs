using System.Windows;
using Blood_Alcohol.Views;

namespace Blood_Alcohol.Services;

/// <summary>
/// 系统初始化确认弹窗服务接口。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页初始化命令调用，用于在执行初始化前展示前置确认和提示弹窗。
/// </remarks>
public interface ISystemInitializationDialogService
{
	/// <summary>
	/// 显示轴安全位置确认弹窗并返回操作员选择。
	/// </summary>
	/// By:ChengLei
	/// <returns>确认安全返回 true，点击否或关闭返回 false。</returns>
	/// <remarks>
	/// 仅当操作员明确点击是时允许后续初始化流程继续。
	/// </remarks>
	bool ConfirmAxisSafePosition();

	/// <summary>
	/// 显示初始化必须处于手动模式的提示弹窗。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页初始化前置校验调用，用于提示操作员先把设备切换到手动档。
	/// </remarks>
	void ShowManualModeRequired();

	/// <summary>
	/// 显示开始前必须先完成 Z 重量转坐标系数标定的提示弹窗。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由首页开始前置校验调用，用于阻止未标定状态直接启动检测流程。
	/// </remarks>
	void ShowWeightToZCoefficientRequired();
}

/// <summary>
/// 系统初始化确认弹窗服务。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 使用自定义 WPF 窗口展示初始化相关确认内容，替代系统默认消息框。
/// </remarks>
public sealed class SystemInitializationDialogService : ISystemInitializationDialogService
{
	/// <summary>
	/// 显示轴安全位置确认弹窗并返回操作员选择。
	/// </summary>
	/// By:ChengLei
	/// <returns>确认安全返回 true，点击否或关闭返回 false。</returns>
	/// <remarks>
	/// 弹窗会优先挂到当前激活窗口作为 Owner，便于居中和模态遮挡。
	/// </remarks>
	public bool ConfirmAxisSafePosition()
	{
		Window? dialogOwner = ResolveOwnerWindow();
		AxisSafePositionConfirmDialog dialog = new AxisSafePositionConfirmDialog
		{
			Owner = dialogOwner
		};

		return dialog.ShowDialog() == true;
	}

	/// <summary>
	/// 显示初始化必须处于手动模式的提示弹窗。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 弹窗会优先挂到当前激活窗口作为 Owner，保持与安全位确认弹窗一致的展示风格。
	/// </remarks>
	public void ShowManualModeRequired()
	{
		Window? dialogOwner = ResolveOwnerWindow();
		ManualModeRequiredDialog dialog = new ManualModeRequiredDialog
		{
			Owner = dialogOwner
		};

		dialog.ShowDialog();
	}

	/// <summary>
	/// 显示开始前必须先完成 Z 重量转坐标系数标定的提示弹窗。
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 弹窗会优先挂到当前激活窗口作为 Owner，保持与手动模式提示弹窗一致的展示风格。
	/// </remarks>
	public void ShowWeightToZCoefficientRequired()
	{
		Window? dialogOwner = ResolveOwnerWindow();
		WeightToZCoefficientRequiredDialog dialog = new WeightToZCoefficientRequiredDialog
		{
			Owner = dialogOwner
		};

		dialog.ShowDialog();
	}

	/// <summary>
	/// 解析当前可用的弹窗宿主窗口。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回当前激活窗口或应用主窗口。</returns>
	/// <remarks>
	/// 当前激活窗口不可用时回退到 MainWindow，避免弹窗无宿主导致位置异常。
	/// </remarks>
	private static Window? ResolveOwnerWindow()
	{
		if (Application.Current == null)
		{
			return null;
		}

		foreach (Window window in Application.Current.Windows)
		{
			if (window.IsActive)
			{
				return window;
			}
		}

		return Application.Current.MainWindow;
	}
}
