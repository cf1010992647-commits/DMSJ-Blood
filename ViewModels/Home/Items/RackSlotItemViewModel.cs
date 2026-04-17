using System.Windows.Media;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页料架槽位显示模型。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页采血管架、顶空瓶架和针头状态区复用，承载编号与颜色状态。
/// </remarks>
public class RackSlotItemViewModel : BaseViewModel
{
	private Brush _fill = Brushes.WhiteSmoke;

	private Brush _foreground = Brushes.Black;

	/// <summary>
	/// 槽位编号。
	/// </summary>
	/// By:ChengLei
	public int Number { get; set; }

	/// <summary>
	/// 槽位填充画刷。
	/// </summary>
	/// By:ChengLei
	public Brush Fill
	{
		get
		{
			return _fill;
		}
		set
		{
			if (_fill != value)
			{
				_fill = value;
				OnPropertyChanged(nameof(Fill));
			}
		}
	}

	/// <summary>
	/// 槽位文字画刷。
	/// </summary>
	/// By:ChengLei
	public Brush Foreground
	{
		get
		{
			return _foreground;
		}
		set
		{
			if (_foreground != value)
			{
				_foreground = value;
				OnPropertyChanged(nameof(Foreground));
			}
		}
	}
}
