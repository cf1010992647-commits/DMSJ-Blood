using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页检测状态协调器
internal sealed class HomeDetectionStateCoordinator
{
	private const string IdleCountRuleText = "未开始检测：可自由选择采血管数量。";
	private const string StartedCountRuleText = "检测已开始：只能增加采血管数量，不能减少。";
	private const string StoppedCountRuleText = "检测已停止：可重新选择采血管数量。";
	private const string EmergencyStoppedCountRuleText = "急停已触发：请排查后复位。";
	private const string AlarmBlockedCountRuleText = "报警中：请先排查并清除报警，再开始检测。";
	private const string AlarmStoppedCountRuleText = "报警触发：检测已自动停止，请排查后复位。";
	private const string AlarmClearedCountRuleText = "报警已解除：可重新开始检测。";

	/// <summary>
	/// 当前是否处于初始化流程
	/// </summary>
	/// By:ChengLei
	public bool IsInitializing { get; private set; }

	/// <summary>
	/// 当前是否处于开始命令发送流程
	/// </summary>
	/// By:ChengLei
	public bool IsStartCommandProcessing { get; private set; }

	/// <summary>
	/// 当前是否处于检测运行中状态
	/// </summary>
	/// By:ChengLei
	public bool IsDetectionStarted { get; private set; }

	/// <summary>
	/// 当前首页数量规则提示文本
	/// </summary>
	/// By:ChengLei
	public string CountRuleText { get; private set; } = IdleCountRuleText;

	/// <summary>
	/// 当前是否允许选择采血管数量
	/// </summary>
	/// By:ChengLei
	public bool IsTubeSelectionEnabled => !IsDetectionStarted;

	/// <summary>
	/// 尝试进入初始化状态
	/// </summary>
	/// By:ChengLei
	/// <returns>返回是否成功进入初始化状态</returns>
	/// <remarks>
	/// 已在初始化中时返回 false 以保持原有防重入语义
	/// </remarks>
	public bool TryBeginInitialize()
	{
		if (IsInitializing)
		{
			return false;
		}

		IsInitializing = true;
		return true;
	}

	/// <summary>
	/// 结束初始化状态
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由初始化流程退出时调用 无论成功失败均需复位初始化标记
	/// </remarks>
	public void FinishInitialize()
	{
		IsInitializing = false;
	}

	/// <summary>
	/// 尝试进入开始命令处理状态
	/// </summary>
	/// By:ChengLei
	/// <returns>返回是否成功进入开始命令处理状态</returns>
	/// <remarks>
	/// 已在运行中或已在处理开始命令时返回 false 以避免重复开始
	/// </remarks>
	public bool TryBeginStartProcessing()
	{
		if (IsDetectionStarted || IsStartCommandProcessing)
		{
			return false;
		}

		IsStartCommandProcessing = true;
		return true;
	}

	/// <summary>
	/// 结束开始命令处理状态
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始流程 finally 统一调用 用于恢复命令可执行状态
	/// </remarks>
	public void FinishStartProcessing()
	{
		IsStartCommandProcessing = false;
	}

	/// <summary>
	/// 标记开始流程被报警阻断
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始前置条件校验发现报警时调用
	/// </remarks>
	public void MarkAlarmBlocked()
	{
		CountRuleText = AlarmBlockedCountRuleText;
	}

	/// <summary>
	/// 标记开始流程失败
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始流程发生异常时调用 确保检测运行标志回到未开始状态
	/// </remarks>
	public void MarkStartFailed()
	{
		IsDetectionStarted = false;
	}

	/// <summary>
	/// 标记检测已成功开始
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由开始信号发送成功并通过前置校验后调用
	/// </remarks>
	public void MarkStarted()
	{
		IsDetectionStarted = true;
		CountRuleText = StartedCountRuleText;
	}

	/// <summary>
	/// 标记检测已停止
	/// </summary>
	/// By:ChengLei
	/// <returns>返回停止前是否处于运行中状态</returns>
	/// <remarks>
	/// 即使当前未运行也会把运行标记复位 用于统一停止流程入口
	/// </remarks>
	public bool MarkStopped()
	{
		bool wasRunning = IsDetectionStarted;
		IsDetectionStarted = false;
		CountRuleText = StoppedCountRuleText;
		return wasRunning;
	}

	/// <summary>
	/// 标记急停已触发
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由急停流程调用 用于更新首页提示并退出运行状态
	/// </remarks>
	public void MarkEmergencyStopped()
	{
		IsDetectionStarted = false;
		CountRuleText = EmergencyStoppedCountRuleText;
	}

	/// <summary>
	/// 标记报警触发导致的自动停机
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由报警自动停机流程调用 用于更新首页提示并退出运行状态
	/// </remarks>
	public void MarkAlarmStopped()
	{
		IsDetectionStarted = false;
		CountRuleText = AlarmStoppedCountRuleText;
	}

	/// <summary>
	/// 标记报警已解除且当前未运行检测
	/// </summary>
	/// By:ChengLei
	/// <remarks>
	/// 由报警监听在报警解除且未运行时调用
	/// </remarks>
	public void MarkAlarmClearedWhileIdle()
	{
		if (!IsDetectionStarted)
		{
			CountRuleText = AlarmClearedCountRuleText;
		}
	}

	/// <summary>
	/// 计算采血管数量变更应使用的日志类别
	/// </summary>
	/// By:ChengLei
	/// <returns>返回首页日志业务类别</returns>
	/// <remarks>
	/// 运行中使用检测类别 未运行使用操作类别
	/// </remarks>
	public HomeLogKind ResolveCountChangeKind()
	{
		return IsDetectionStarted ? HomeLogKind.Detection : HomeLogKind.Operation;
	}

	/// <summary>
	/// 更新首页数量规则提示文本
	/// </summary>
	/// By:ChengLei
	/// <param name="text">新的提示文本</param>
	/// <returns>返回文本是否发生变化</returns>
	/// <remarks>
	/// 由首页属性包装器和特殊提示场景复用 统一通过协调器维护文案状态
	/// </remarks>
	public bool SetCountRuleText(string text)
	{
		text ??= string.Empty;
		if (string.Equals(CountRuleText, text, System.StringComparison.Ordinal))
		{
			return false;
		}

		CountRuleText = text;
		return true;
	}
}
