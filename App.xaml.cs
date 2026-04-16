using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using Blood_Alcohol.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
            ValidateStartupConfigurations();
            CommunicationManager.AutoConnect();

            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        /// <summary>
        /// 校验启动阶段关键配置。
        /// </summary>
        /// <remarks>
        /// 启动时仅记录配置错误；高风险流程启动由 WorkflowEngine 再次校验并阻断。
        /// </remarks>
        private static void ValidateStartupConfigurations()
        {
            var errors = new List<string>();

            AddPrefixedValidationErrors(
                errors,
                "流程信号配置",
                new ConfigService<WorkflowSignalConfig>("WorkflowSignalConfig.json").Load().Validate());
            AddPrefixedValidationErrors(
                errors,
                "轴调试地址配置",
                new ConfigService<AxisDebugAddressConfig>("AxisDebugAddressConfig.json").Load().Validate());
            AddPrefixedValidationErrors(
                errors,
                "工艺参数配置",
                new ConfigService<ProcessParameterConfig>("ProcessParameterConfig.json").Load().Validate());

            foreach (string error in errors)
            {
                CommunicationManager.LogConfigurationMessage("配置非法：" + error, CommunicationManager.LogLevel.Error);
            }
        }

        /// <summary>
        /// 添加带配置名称前缀的校验错误。
        /// </summary>
        /// <param name="target">目标错误列表。</param>
        /// <param name="prefix">配置名称前缀。</param>
        /// <param name="errors">原始错误列表。</param>
        private static void AddPrefixedValidationErrors(
            List<string> target,
            string prefix,
            IEnumerable<string> errors)
        {
            foreach (string error in errors)
            {
                target.Add($"{prefix}：{error}");
            }
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
            ShutdownApplicationAsync().GetAwaiter().GetResult();
            CommunicationManager.OnLogReceived -= OnCommunicationLogReceived;
            base.OnExit(e);
        }

        /// <summary>
        /// 按固定顺序关闭页面后台任务和通信资源。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回应用关闭任务。</returns>
        /// <remarks>
        /// 由 OnExit 调用，先停页面和流程，再停 TCP、PLC 轮询和 RS485。
        /// </remarks>
        private async Task ShutdownApplicationAsync()
        {
            await StopPageViewModelsAsync().ConfigureAwait(false);
            CommunicationManager.StopTcp();
            await CommunicationManager.PlcPolling.StopAsync().ConfigureAwait(false);
            CommunicationManager.DisconnectRs485();
        }

        /// <summary>
        /// 停止主窗口中已经创建的页面视图模型。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回页面停止任务。</returns>
        /// <remarks>
        /// 由 ShutdownApplicationAsync 调用，确保首页流程和调试页后台轮询先退出。
        /// </remarks>
        private async Task StopPageViewModelsAsync()
        {
            if (MainWindow == null)
            {
                return;
            }

            HashSet<object> visited = new HashSet<object>();
            List<object> dataContexts = EnumerateDataContexts(MainWindow)
                .Where(x => visited.Add(x))
                .ToList();
            foreach (object dataContext in dataContexts)
            {
                if (dataContext is HomeViewModel homeViewModel)
                {
                    await homeViewModel.DisposeAsync().ConfigureAwait(false);
                }
                else if (dataContext is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// 枚举窗口可视树和逻辑树中的数据上下文。
        /// </summary>
        /// By:ChengLei
        /// <param name="root">需要遍历的根节点。</param>
        /// <returns>返回已发现的数据上下文集合。</returns>
        /// <remarks>
        /// 由 StopPageViewModelsAsync 调用，用于找到已经创建的页面视图模型。
        /// </remarks>
        private static IEnumerable<object> EnumerateDataContexts(DependencyObject root)
        {
            if (root is FrameworkElement element && element.DataContext != null)
            {
                yield return element.DataContext;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyChild)
                {
                    foreach (object dataContext in EnumerateDataContexts(dependencyChild))
                    {
                        yield return dataContext;
                    }
                }
            }

            int visualChildrenCount;
            try
            {
                visualChildrenCount = VisualTreeHelper.GetChildrenCount(root);
            }
            catch (InvalidOperationException)
            {
                visualChildrenCount = 0;
            }

            for (int index = 0; index < visualChildrenCount; index++)
            {
                DependencyObject visualChild = VisualTreeHelper.GetChild(root, index);
                foreach (object dataContext in EnumerateDataContexts(visualChild))
                {
                    yield return dataContext;
                }
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
