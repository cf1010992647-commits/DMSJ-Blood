using System.Windows;
using System.Windows.Input;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 开始前置 Z 重量转坐标系数未标定提示弹窗。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 当检测开始前发现重量转 Z 坐标系数未完成有效标定时弹出，阻止操作员继续启动流程。
    /// </remarks>
    public partial class WeightToZCoefficientRequiredDialog : Window
    {
        /// <summary>
        /// 初始化 Z 重量转坐标系数未标定提示弹窗。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由首页弹窗服务创建，沿用 ManualModeRequiredDialog 的布局风格。
        /// </remarks>
        public WeightToZCoefficientRequiredDialog()
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
        /// 点击按钮或关闭图标后仅关闭提示窗，不继续执行开始流程。
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
