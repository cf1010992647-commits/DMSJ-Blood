using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 提供带执行互斥能力的异步命令实现。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由各页面ViewModel在绑定异步按钮命令时创建，避免重复点击并集中处理异常。
    /// </remarks>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isExecuting;

        /// <summary>
        /// 初始化异步命令实例并绑定执行逻辑。
        /// </summary>
        /// By:ChengLei
        /// <param name="executeAsync">命令触发时执行的异步委托。</param>
        /// <param name="canExecute">命令可用性判断委托，为空时默认可执行。</param>
        /// <param name="onError">命令执行异常回调，用于页面统一提示。</param>
        /// <remarks>
        /// 由页面ViewModel构造函数调用，用于创建可绑定到按钮的异步命令。
        /// </remarks>
        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _onError = onError;
        }

        /// <summary>
        /// 可执行状态变更事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 通过挂接 CommandManager.RequerySuggested 通知WPF刷新按钮可用状态。
        /// </remarks>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断命令当前是否允许执行。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameter">命令参数（当前实现未使用）。</param>
        /// <returns>返回命令是否可执行。</returns>
        /// <remarks>
        /// 由WPF命令系统在界面状态刷新时调用。
        /// </remarks>
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        /// <summary>
        /// 执行异步命令并处理执行期状态与异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameter">命令参数（当前实现未使用）。</param>
        /// <remarks>
        /// 由按钮点击触发；执行期间会临时禁用命令，结束后恢复可用状态。
        /// </remarks>
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _executeAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
