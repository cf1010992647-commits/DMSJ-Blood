using Blood_Alcohol.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Blood_Alcohol
{
    public partial class App : Application
    {
        private DateTime _lastPlcErrorToastAt = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);

            CommunicationManager.LoadSettings();
            CommunicationManager.AutoConnect();

            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (TryHandlePlcCommunicationException(e.Exception))
            {
                e.Handled = true;
                return;
            }
        }

        private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                TryHandlePlcCommunicationException(ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (TryHandlePlcCommunicationException(e.Exception))
            {
                e.SetObserved();
            }
        }

        private bool TryHandlePlcCommunicationException(Exception ex)
        {
            if (!ContainsPlcTimeout(ex))
            {
                return false;
            }

            CommunicationManager.Log485Message("PLC通讯超时，请检查协议/串口参数/站号/接线。");

            // Throttle popups to avoid blocking during polling loops.
            DateTime now = DateTime.Now;
            if ((now - _lastPlcErrorToastAt).TotalSeconds >= 5)
            {
                _lastPlcErrorToastAt = now;
                MessageBox.Show(
                    "PLC通讯超时：请确认PLC为Modbus RTU从站，串口参数一致(波特率/8N1)，站号一致，485接线正确。",
                    "PLC通讯异常",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return true;
        }

        private static bool ContainsPlcTimeout(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is TimeoutException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
