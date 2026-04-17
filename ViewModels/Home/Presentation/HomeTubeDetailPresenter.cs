using Blood_Alcohol.Models;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 作用
/// 首页采血管详情展示呈现器
internal sealed class HomeTubeDetailPresenter
{
	/// <summary>
	/// 根据采血管上下文刷新首页详情字段
	/// </summary>
	/// By:ChengLei
	/// <param name="context">详情字段应用上下文</param>
	/// <param name="tubeContext">当前选中的采血管上下文</param>
	/// <remarks>
	/// 未找到上下文时会回退到默认展示值 避免首页残留上一次选中采血管的详情
	/// </remarks>
	public void Apply(HomeTubeDetailApplyContext context, TubeContext? tubeContext)
	{
		if (tubeContext != null)
		{
			context.SetScanCode(tubeContext.TubeCode);
			context.SetSampleVolume(tubeContext.SampleVolume);
			context.SetHeadspaceASampleWeight(tubeContext.HeadspaceASampleWeight);
			context.SetHeadspaceAButanolWeight(tubeContext.HeadspaceAButanolWeight);
			context.SetHeadspaceBSampleWeight(tubeContext.HeadspaceBSampleWeight);
			context.SetHeadspaceBButanolWeight(tubeContext.HeadspaceBButanolWeight);
			return;
		}

		context.SetScanCode("未识别");
		context.SetSampleVolume("0");
		context.SetHeadspaceASampleWeight("0.0");
		context.SetHeadspaceAButanolWeight("0.0");
		context.SetHeadspaceBSampleWeight("0.0");
		context.SetHeadspaceBButanolWeight("0.0");
	}
}

/// <summary>
/// 作用
/// 首页采血管详情字段应用上下文
internal sealed class HomeTubeDetailApplyContext
{
	/// <summary>
	/// 设置扫码编号展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetScanCode { get; init; }

	/// <summary>
	/// 设置采血管体积展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetSampleVolume { get; init; }

	/// <summary>
	/// 设置顶空瓶A样品重量展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetHeadspaceASampleWeight { get; init; }

	/// <summary>
	/// 设置顶空瓶A叔丁醇重量展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetHeadspaceAButanolWeight { get; init; }

	/// <summary>
	/// 设置顶空瓶B样品重量展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetHeadspaceBSampleWeight { get; init; }

	/// <summary>
	/// 设置顶空瓶B叔丁醇重量展示文本的委托
	/// </summary>
	/// By:ChengLei
	public required Action<string> SetHeadspaceBButanolWeight { get; init; }
}
