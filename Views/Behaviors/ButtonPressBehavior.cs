using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Blood_Alcohol.Views.Behaviors
{
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

        public static ICommand? GetPressCommand(DependencyObject obj)
        {
            return (ICommand?)obj.GetValue(PressCommandProperty);
        }

        public static void SetPressCommand(DependencyObject obj, ICommand? value)
        {
            obj.SetValue(PressCommandProperty, value);
        }

        public static ICommand? GetReleaseCommand(DependencyObject obj)
        {
            return (ICommand?)obj.GetValue(ReleaseCommandProperty);
        }

        public static void SetReleaseCommand(DependencyObject obj, ICommand? value)
        {
            obj.SetValue(ReleaseCommandProperty, value);
        }

        private static bool GetIsHooked(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsHookedProperty);
        }

        private static void SetIsHooked(DependencyObject obj, bool value)
        {
            obj.SetValue(IsHookedProperty, value);
        }

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
            SetIsHooked(button, true);
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            ICommand? command = GetPressCommand(button);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ExecuteReleaseCommand(sender as Button);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ExecuteReleaseCommand(sender as Button);
            }
        }

        private static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            ExecuteReleaseCommand(sender as Button);
        }

        private static void ExecuteReleaseCommand(Button? button)
        {
            if (button == null)
            {
                return;
            }

            ICommand? command = GetReleaseCommand(button);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }
    }
}
