using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页采血管事件处理结果。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由采血管上下文状态机返回给 HomeViewModel，用于追加 CSV 和刷新选中详情。
/// </remarks>
internal sealed class HomeTubeProcessResult
{
	/// <summary>
	/// 初始化采血管事件处理结果。
	/// </summary>
	/// By:ChengLei
	/// <param name="context">更新后的采血管上下文。</param>
	/// <param name="headspaceCode">本次事件关联的顶空瓶编码。</param>
	/// <param name="selectedDetailTubeIndex">处理后应该选中的采血管序号。</param>
	/// <remarks>
	/// 由 HomeTubeProcessState.ApplyEvent 调用。
	/// </remarks>
	public HomeTubeProcessResult(TubeContext context, string headspaceCode, int selectedDetailTubeIndex)
	{
		Context = context;
		HeadspaceCode = headspaceCode;
		SelectedDetailTubeIndex = selectedDetailTubeIndex;
	}

	/// <summary>
	/// 更新后的采血管上下文。
	/// </summary>
	/// By:ChengLei
	public TubeContext Context { get; }

	/// <summary>
	/// 本次事件关联的顶空瓶编码。
	/// </summary>
	/// By:ChengLei
	public string HeadspaceCode { get; }

	/// <summary>
	/// 处理后应该选中的采血管序号。
	/// </summary>
	/// By:ChengLei
	public int SelectedDetailTubeIndex { get; }
}
