using System.Windows;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 轴点位配置窗口。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 承载 AxisDebugAddressConfig.json 的编辑界面。
    /// </remarks>
    public partial class AxisAddressConfigWindow : Window
    {
        /// <summary>
        /// 初始化轴点位配置窗口。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由轴调试页点击轴点位配置按钮时创建。
        /// </remarks>
        public AxisAddressConfigWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 获取窗口会话中是否保存过配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由轴调试页在窗口关闭后判断是否需要重新加载点位配置。
        /// </remarks>
        public bool HasSaved => DataContext is AxisAddressConfigViewModel viewModel && viewModel.HasSaved;

        /// <summary>
        /// 处理关闭按钮点击事件。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由窗口右上方关闭按钮触发。
        /// </remarks>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
