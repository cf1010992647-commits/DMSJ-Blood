using System.Windows.Controls;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 气缸控制页视图，承载固定地址表与手动按钮界面。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由设置页 TabControl 承载，DataContext 在 XAML 中创建。
    /// </remarks>
    public partial class CylinderControlView : UserControl
    {
        /// <summary>
        /// 初始化气缸控制页视图。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，仅负责加载界面组件。
        /// </remarks>
        public CylinderControlView()
        {
            InitializeComponent();
        }
    }
}
