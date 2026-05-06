using System.Windows;
using System.Windows.Input;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 轴安全位置确认弹窗。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 在系统初始化前展示，只有点击是才允许继续初始化。
    /// </remarks>
    public partial class AxisSafePositionConfirmDialog : Window
    {
        /// <summary>
        /// 初始化轴安全位置确认弹窗。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由系统初始化确认服务创建。
        /// </remarks>
        public AxisSafePositionConfirmDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 处理确认按钮点击并关闭弹窗。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 点击是时把 DialogResult 置为 true，调用方据此写入 M40 并继续初始化。
        /// </remarks>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 处理取消按钮点击并关闭弹窗。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 点击否或右上角关闭时把 DialogResult 置为 false，调用方不执行初始化。
        /// </remarks>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
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
