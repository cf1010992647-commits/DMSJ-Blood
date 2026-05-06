using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Blood_Alcohol.Views.Behaviors
{
    /// <summary>
    /// 按钮按下和释放命令行为
    /// </summary>
    public static class ButtonPressBehavior
    {
        public static readonly DependencyProperty PressCommandProperty =
            DependencyProperty.RegisterAttached(
                "PressCommand",
                typeof(ICommand),
                typeof(ButtonPressBehavior),
                new PropertyMetadata(null, OnCommandPropertyChanged));

        public static readonly DependencyProperty ReleaseCommandProperty =
            DependencyProperty.RegisterAttached(
                "ReleaseCommand",
                typeof(ICommand),
                typeof(ButtonPressBehavior),
                new PropertyMetadata(null, OnCommandPropertyChanged));

        private static readonly DependencyProperty IsHookedProperty =
            DependencyProperty.RegisterAttached(
                "IsHooked",
                typeof(bool),
                typeof(ButtonPressBehavior),
                new PropertyMetadata(false));

        private static readonly DependencyProperty IsPressedActiveProperty =
            DependencyProperty.RegisterAttached(
                "IsPressedActive",
                typeof(bool),
                typeof(ButtonPressBehavior),
                new PropertyMetadata(false));

        /// <summary>
        /// 获取按钮按下命令
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <returns>返回按钮按下时执行的命令。</returns>
        /// <remarks>
        /// 由WPF附加属性系统调用，用于点动按钮按下置位。
        /// </remarks>
        public static ICommand? GetPressCommand(DependencyObject obj)
        {
            return (ICommand?)obj.GetValue(PressCommandProperty);
        }

        /// <summary>
        /// 设置按钮按下命令
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <param name="value">按钮按下时执行的命令。</param>
        /// <remarks>
        /// 由XAML绑定调用，用于点动按钮按下置位。
        /// </remarks>
        public static void SetPressCommand(DependencyObject obj, ICommand? value)
        {
            obj.SetValue(PressCommandProperty, value);
        }

        /// <summary>
        /// 获取按钮释放命令
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <returns>返回按钮释放时执行的命令。</returns>
        /// <remarks>
        /// 由WPF附加属性系统调用，用于点动按钮释放复位。
        /// </remarks>
        public static ICommand? GetReleaseCommand(DependencyObject obj)
        {
            return (ICommand?)obj.GetValue(ReleaseCommandProperty);
        }

        /// <summary>
        /// 设置按钮释放命令
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <param name="value">按钮释放时执行的命令。</param>
        /// <remarks>
        /// 由XAML绑定调用，用于点动按钮释放复位。
        /// </remarks>
        public static void SetReleaseCommand(DependencyObject obj, ICommand? value)
        {
            obj.SetValue(ReleaseCommandProperty, value);
        }

        /// <summary>
        /// 获取按钮事件是否已经挂接
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <returns>返回按钮事件是否已经挂接。</returns>
        /// <remarks>
        /// 由命令属性变更回调调用，避免重复注册鼠标事件。
        /// </remarks>
        private static bool GetIsHooked(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsHookedProperty);
        }

        /// <summary>
        /// 设置按钮事件挂接状态
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <param name="value">按钮事件是否已经挂接。</param>
        /// <remarks>
        /// 由命令属性变更回调调用，避免重复注册鼠标事件。
        /// </remarks>
        private static void SetIsHooked(DependencyObject obj, bool value)
        {
            obj.SetValue(IsHookedProperty, value);
        }

        /// <summary>
        /// 获取按钮是否处于已按下且需要释放的状态
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <returns>返回按钮是否需要补发释放命令。</returns>
        /// <remarks>
        /// 由释放事件调用，防止鼠标离开和失去捕获时重复发送复位命令。
        /// </remarks>
        private static bool GetIsPressedActive(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsPressedActiveProperty);
        }

        /// <summary>
        /// 设置按钮是否处于已按下且需要释放的状态
        /// </summary>
        /// By:ChengLei
        /// <param name="obj">附加属性所属对象。</param>
        /// <param name="value">按钮是否需要补发释放命令。</param>
        /// <remarks>
        /// 由按下和释放事件调用，保证一次按下最多对应一次释放。
        /// </remarks>
        private static void SetIsPressedActive(DependencyObject obj, bool value)
        {
            obj.SetValue(IsPressedActiveProperty, value);
        }

        /// <summary>
        /// 在命令属性变化时挂接按钮鼠标事件
        /// </summary>
        /// By:ChengLei
        /// <param name="d">附加属性所属对象。</param>
        /// <param name="e">属性变更事件参数。</param>
        /// <remarks>
        /// 由WPF属性系统调用，绑定按下、释放、移出、失焦和卸载兜底事件。
        /// </remarks>
        private static void OnCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Button button)
            {
                return;
            }

            if (GetIsHooked(button))
            {
                return;
            }

            button.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            button.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            button.MouseLeave += OnMouseLeave;
            button.LostMouseCapture += OnLostMouseCapture;
            button.Unloaded += OnUnloaded;
            SetIsHooked(button, true);
        }

        /// <summary>
        /// 处理鼠标左键按下并执行按下命令
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件来源按钮。</param>
        /// <param name="e">鼠标按键事件参数。</param>
        /// <remarks>
        /// 由按钮预览鼠标按下事件触发，会捕获鼠标以提高释放事件可靠性。
        /// </remarks>
        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            SetIsPressedActive(button, true);
            button.CaptureMouse();

            ICommand? command = GetPressCommand(button);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        /// <summary>
        /// 处理鼠标左键释放并执行释放命令
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件来源按钮。</param>
        /// <param name="e">鼠标按键事件参数。</param>
        /// <remarks>
        /// 由按钮预览鼠标释放事件触发，用于点动按钮复位。
        /// </remarks>
        private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ExecuteReleaseCommand(sender as Button);
        }

        /// <summary>
        /// 处理鼠标移出按钮时的释放兜底
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件来源按钮。</param>
        /// <param name="e">鼠标事件参数。</param>
        /// <remarks>
        /// 由按钮鼠标移出事件触发，避免按住拖出按钮后没有复位。
        /// </remarks>
        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ExecuteReleaseCommand(sender as Button);
            }
        }

        /// <summary>
        /// 处理鼠标捕获丢失时的释放兜底
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件来源按钮。</param>
        /// <param name="e">鼠标事件参数。</param>
        /// <remarks>
        /// 由按钮失去鼠标捕获事件触发，避免窗口切换等情况导致点动保持置位。
        /// </remarks>
        private static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            ExecuteReleaseCommand(sender as Button);
        }

        /// <summary>
        /// 处理按钮卸载时的释放兜底
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件来源按钮。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由按钮卸载事件触发，避免页面切换时点动保持置位。
        /// </remarks>
        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExecuteReleaseCommand(sender as Button);
        }

        /// <summary>
        /// 执行按钮释放命令
        /// </summary>
        /// By:ChengLei
        /// <param name="button">需要释放的按钮。</param>
        /// <remarks>
        /// 由多个释放兜底事件共用，确保一次按下只发送一次释放命令。
        /// </remarks>
        private static void ExecuteReleaseCommand(Button? button)
        {
            if (button == null)
            {
                return;
            }

            if (!GetIsPressedActive(button))
            {
                return;
            }

            SetIsPressedActive(button, false);
            if (button.IsMouseCaptured)
            {
                button.ReleaseMouseCapture();
            }

            ICommand? command = GetReleaseCommand(button);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }
    }
}
