namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页条件展示项模型。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页条件区展示温度、时间等工艺参数摘要。
/// </remarks>
public class ConditionItemViewModel : BaseViewModel
{
	private string _value;

	/// <summary>
	/// 初始化条件展示项。
	/// </summary>
	/// By:ChengLei
	/// <param name="name">条件名称。</param>
	/// <param name="value">条件值。</param>
	/// <param name="unit">单位文本。</param>
	/// <remarks>
	/// 由首页条件构建器调用，用于初始化固定条件行。
	/// </remarks>
	public ConditionItemViewModel(string name, string value, string unit)
	{
		Name = name;
		_value = value;
		Unit = unit;
	}

	/// <summary>
	/// 条件名称。
	/// </summary>
	/// By:ChengLei
	public string Name { get; }

	/// <summary>
	/// 单位文本。
	/// </summary>
	/// By:ChengLei
	public string Unit { get; }

	/// <summary>
	/// 条件显示值。
	/// </summary>
	/// By:ChengLei
	public string Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (_value != value)
			{
				_value = value;
				OnPropertyChanged(nameof(Value));
			}
		}
	}
}
