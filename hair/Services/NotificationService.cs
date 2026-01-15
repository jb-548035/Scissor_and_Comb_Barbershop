using System;
using System.Threading;
using System.Threading.Tasks;

namespace hair.Services
{
    public enum NotificationType
    {
        Success,
        Error,
        Info
    }

    public sealed class NotificationState
    {
        public NotificationType Type { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class NotificationService
    {
        private readonly object _lock = new();
        private CancellationTokenSource? _autoClearCts;

        public event Action? OnChange;

        public NotificationState? Current { get; private set; }

        public void ShowSuccess(string message, int autoClearMs = 5000) => Show(NotificationType.Success, message, autoClearMs);

        public void ShowError(string message, int autoClearMs = 8000) => Show(NotificationType.Error, message, autoClearMs);

        public void ShowInfo(string message, int autoClearMs = 5000) => Show(NotificationType.Info, message, autoClearMs);

        public void Clear()
        {
            lock (_lock)
            {
                _autoClearCts?.Cancel();
                _autoClearCts?.Dispose();
                _autoClearCts = null;

                Current = null;
            }

            OnChange?.Invoke();
        }

        private void Show(NotificationType type, string message, int autoClearMs)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            CancellationTokenSource? localCts;

            lock (_lock)
            {
                _autoClearCts?.Cancel();
                _autoClearCts?.Dispose();

                _autoClearCts = new CancellationTokenSource();
                localCts = _autoClearCts;

                Current = new NotificationState
                {
                    Type = type,
                    Message = message
                };
            }

            OnChange?.Invoke();

            if (autoClearMs > 0 && localCts != null)
            {
                _ = AutoClearAsync(localCts, autoClearMs);
            }
        }

        private async Task AutoClearAsync(CancellationTokenSource cts, int delayMs)
        {
            try
            {
                await Task.Delay(delayMs, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            lock (_lock)
            {
                if (!ReferenceEquals(_autoClearCts, cts))
                {
                    return;
                }

                _autoClearCts?.Dispose();
                _autoClearCts = null;
                Current = null;
            }

            OnChange?.Invoke();
        }
    }
}
