using System.Windows;
using System.Windows.Input;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 初始化前置手动模式提示弹窗。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 当设备当前不在手动档时弹出，提示操作员先切换到手动模式再重试初始化。
    /// </remarks>
    public partial class ManualModeRequiredDialog : Window
    {
        /// <summary>
        /// 初始化手动模式提示弹窗。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由系统初始化确认服务创建，沿用安全位确认弹窗的布局风格。
        /// </remarks>
        public ManualModeRequiredDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 处理已知晓按钮点击并关闭弹窗。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 点击按钮或关闭图标后仅关闭提示窗，不继续执行初始化流程。
        /// </remarks>
        private void AcknowledgeButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 处理窗口空白区域按下并允许拖动弹窗。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">鼠标按键事件参数。</param>
        /// <remarks>
        /// 自定义无边框窗口需要手动支持标题区域拖动。
        /// </remarks>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
