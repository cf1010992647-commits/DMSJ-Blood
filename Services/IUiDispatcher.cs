using System;
using System.Windows;
using System.Windows.Threading;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// UI线程调度抽象。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由视图模型使用，避免直接依赖 WPF Dispatcher 以便单元测试替换。
    /// </remarks>
    public interface IUiDispatcher
    {
        /// <summary>
        /// 判断当前线程是否可以直接访问 UI。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前线程是否具备 UI 访问权限。</returns>
        /// <remarks>
        /// 由视图模型在更新绑定集合或属性前调用。
        /// </remarks>
        bool CheckAccess();

        /// <summary>
        /// 在 UI 线程同步执行操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要执行的 UI 操作。</param>
        /// <remarks>
        /// 当前已经在 UI 线程时直接执行，否则通过 Dispatcher.Invoke 执行。
        /// </remarks>
        void Invoke(Action action);

        /// <summary>
        /// 在 UI 线程异步排队执行操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要排队执行的 UI 操作。</param>
        /// <remarks>
        /// 当前已经在 UI 线程时直接执行，否则通过 Dispatcher.BeginInvoke 排队。
        /// </remarks>
        void BeginInvoke(Action action);
    }

    /// <summary>
    /// 基于 WPF Dispatcher 的 UI 调度器。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 默认运行路径使用该实现，测试可替换为同步调度器。
    /// </remarks>
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        /// <summary>
        /// 判断当前线程是否可以直接访问 WPF UI。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前线程是否具备 UI 访问权限。</returns>
        /// <remarks>
        /// Application.Current 为空时按可直接访问处理，便于测试环境运行。
        /// </remarks>
        public bool CheckAccess()
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            return dispatcher == null || dispatcher.CheckAccess();
        }

        /// <summary>
        /// 在 WPF UI 线程同步执行操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要执行的 UI 操作。</param>
        /// <remarks>
        /// 空操作直接忽略，当前已经在 UI 线程时不再派发。
        /// </remarks>
        public void Invoke(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        /// <summary>
        /// 在 WPF UI 线程异步排队执行操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要排队执行的 UI 操作。</param>
        /// <remarks>
        /// 空操作直接忽略，当前已经在 UI 线程时立即执行。
        /// </remarks>
        public void BeginInvoke(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action);
        }
    }
}
