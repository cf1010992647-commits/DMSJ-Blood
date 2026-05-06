using System.Windows;
using Blood_Alcohol.Views;

namespace Blood_Alcohol.Services;

/// <summary>
/// 系统初始化确认弹窗服务接口。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页初始化命令调用，用于在写入安全确认点位前询问操作员。
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
}

/// <summary>
/// 系统初始化确认弹窗服务。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 使用自定义 WPF 窗口展示安全确认内容，替代系统默认消息框。
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
