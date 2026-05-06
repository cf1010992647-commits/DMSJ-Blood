using System.Windows.Controls;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 原位工位控制页视图，承载从点位配置提取出的原位工位按钮列表。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由设置页 TabControl 承载，DataContext 在 XAML 中创建。
    /// </remarks>
    public partial class PointPositionControlView : UserControl
    {
        /// <summary>
        /// 初始化原位工位控制页视图。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，仅负责加载界面组件。
        /// </remarks>
        public PointPositionControlView()
        {
            InitializeComponent();
        }
    }
}
