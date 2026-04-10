using System;
using System.Windows;
using System.Windows.Threading;

namespace Blood_Alcohol.Services
{
    public interface IUiDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        void BeginInvoke(Action action);
    }

    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            return dispatcher == null || dispatcher.CheckAccess();
        }

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
