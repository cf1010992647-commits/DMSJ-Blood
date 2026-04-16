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

    /// <summary>
    /// 提供带参数和执行互斥能力的异步命令实现。
    /// </summary>
    /// By:ChengLei
    /// <typeparam name="T">命令参数类型。</typeparam>
    /// <remarks>
    /// 由需要命令参数的 ViewModel 使用，避免 RelayCommand 中手工 fire-and-forget 异步任务。
    /// </remarks>
    public sealed class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _executeAsync;
        private readonly Predicate<T>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isExecuting;

        /// <summary>
        /// 初始化带参数异步命令实例。
        /// </summary>
        /// By:ChengLei
        /// <param name="executeAsync">命令触发时执行的异步委托。</param>
        /// <param name="canExecute">命令可用性判断委托，为空时默认可执行。</param>
        /// <param name="onError">命令执行异常回调，用于页面统一写入日志或状态文本。</param>
        /// <remarks>
        /// 构造时保存执行委托，执行中会自动禁用命令防止重复触发。
        /// </remarks>
        public AsyncRelayCommand(
            Func<T, Task> executeAsync,
            Predicate<T>? canExecute = null,
            Action<Exception>? onError = null)
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
        /// 通过 CommandManager.RequerySuggested 通知 WPF 刷新按钮可用状态。
        /// </remarks>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断带参数命令当前是否允许执行。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameter">WPF 命令参数。</param>
        /// <returns>返回命令是否可执行。</returns>
        /// <remarks>
        /// 参数无法转换为目标类型时返回不可执行，执行中也会返回不可执行。
        /// </remarks>
        public bool CanExecute(object? parameter)
        {
            if (_isExecuting)
            {
                return false;
            }

            if (!TryGetParameter(parameter, out T typedParameter))
            {
                return false;
            }

            return _canExecute?.Invoke(typedParameter) ?? true;
        }

        /// <summary>
        /// 执行带参数异步命令并处理互斥状态与异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameter">WPF 命令参数。</param>
        /// <remarks>
        /// 执行期间禁用命令，异常通过 onError 统一处理，避免异步异常静默丢失。
        /// </remarks>
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            if (!TryGetParameter(parameter, out T typedParameter))
            {
                return;
            }

            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _executeAsync(typedParameter).ConfigureAwait(true);
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

        /// <summary>
        /// 尝试将 WPF 命令参数转换为目标类型。
        /// </summary>
        /// By:ChengLei
        /// <param name="parameter">WPF 命令参数。</param>
        /// <param name="typedParameter">输出转换后的强类型参数。</param>
        /// <returns>返回参数是否可用于执行命令。</returns>
        /// <remarks>
        /// 空参数仅在目标类型允许空值时可用，值类型会回退为默认值。
        /// </remarks>
        private static bool TryGetParameter(object? parameter, out T typedParameter)
        {
            if (parameter is T value)
            {
                typedParameter = value;
                return true;
            }

            if (parameter == null)
            {
                typedParameter = default!;
                return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
            }

            typedParameter = default!;
            return false;
        }
    }
}
