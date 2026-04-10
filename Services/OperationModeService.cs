using System;

namespace Blood_Alcohol.Services
{
    public enum OperationMode
    {
        Auto,
        Manual
    }

    public static class OperationModeService
    {
        private static readonly object SyncRoot = new();
        private static OperationMode _currentMode = OperationMode.Auto;

        public static event Action<OperationMode>? ModeChanged;

        public static OperationMode CurrentMode
        {
            get
            {
                lock (SyncRoot)
                {
                    return _currentMode;
                }
            }
            set
            {
                bool changed;
                lock (SyncRoot)
                {
                    changed = _currentMode != value;
                    if (changed)
                    {
                        _currentMode = value;
                    }
                }

                if (changed)
                {
                    ModeChanged?.Invoke(value);
                }
            }
        }

        public static bool IsManualMode => CurrentMode == OperationMode.Manual;
    }
}
