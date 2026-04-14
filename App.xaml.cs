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
            CommunicationManager.OnLogReceived += OnCommunicationLogReceived;

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

        protected override void OnExit(ExitEventArgs e)
        {
            CommunicationManager.OnLogReceived -= OnCommunicationLogReceived;
            base.OnExit(e);
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
            if (!ContainsPlcCommunicationException(ex))
            {
                return false;
            }

            CommunicationManager.Log485Message("PLC通讯超时，请检查协议/串口参数/站号/接线。");
            ShowPlcCommPopup();
            return true;
        }

        private void OnCommunicationLogReceived(CommunicationManager.LogMessage log)
        {
            if (!string.Equals(log.Source, "RS485", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string message = log.Message ?? string.Empty;
            bool isCommError =
                message.Contains("PLC串口读取超时", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PLC串口写入超时", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PLC串口读取I/O异常", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PLC串口写入I/O异常", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PLC串口未就绪", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PLC通讯超时", StringComparison.OrdinalIgnoreCase);

            if (!isCommError)
            {
                return;
            }

            ShowPlcCommPopup();
        }

        private static bool ContainsPlcCommunicationException(Exception ex)
        {
            if (ex is TimeoutException)
            {
                return true;
            }

            if (ex is System.IO.IOException)
            {
                return true;
            }

            if (ex is InvalidOperationException ioe)
            {
                string msg = ioe.Message ?? string.Empty;
                if (msg.Contains("PLC通信", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Serial port", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("串口", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (ex is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    if (ContainsPlcCommunicationException(inner))
                    {
                        return true;
                    }
                }
            }

            return ex.InnerException != null && ContainsPlcCommunicationException(ex.InnerException);
        }

        private void ShowPlcCommPopup()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(ShowPlcCommPopup));
                return;
            }

            DateTime now = DateTime.Now;
            if ((now - _lastPlcErrorToastAt).TotalSeconds < 5)
            {
                return;
            }

            _lastPlcErrorToastAt = now;
            MessageBox.Show(
                "PLC通讯异常：请检查PLC电源/485接线/串口参数，恢复后软件会继续运行。",
                "PLC通讯异常",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
