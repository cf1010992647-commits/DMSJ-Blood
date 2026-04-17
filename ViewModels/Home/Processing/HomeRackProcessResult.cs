using System.Collections.Generic;
using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页料架工序解析结果。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由料架工序状态机返回给 HomeViewModel，用于刷新槽位颜色和入队流程事件。
/// </remarks>
internal sealed class HomeRackProcessResult
{
	/// <summary>
	/// 初始化料架工序解析结果。
	/// </summary>
	/// By:ChengLei
	/// <param name="changed">料架视觉状态是否发生变化。</param>
	/// <param name="events">解析出的采血管流程事件。</param>
	/// <remarks>
	/// 由 HomeRackProcessState 创建。
	/// </remarks>
	public HomeRackProcessResult(bool changed, IReadOnlyList<TubeProcessEvent> events)
	{
		Changed = changed;
		Events = events;
	}

	/// <summary>
	/// 料架视觉状态是否发生变化。
	/// </summary>
	/// By:ChengLei
	public bool Changed { get; }

	/// <summary>
	/// 解析出的采血管流程事件。
	/// </summary>
	/// By:ChengLei
	public IReadOnlyList<TubeProcessEvent> Events { get; }
}
