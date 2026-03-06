using System;
using System.Windows.Threading;

namespace MailTriageAssistant.Services;

public interface IClipboardClearTimer
{
    TimeSpan Interval { get; set; }

    event EventHandler? Tick;

    void Start();

    void Stop();
}

public interface IClipboardClearTimerFactory
{
    IClipboardClearTimer Create(Dispatcher dispatcher, DispatcherPriority priority);
}

public sealed class DispatcherClipboardClearTimerFactory : IClipboardClearTimerFactory
{
    public IClipboardClearTimer Create(Dispatcher dispatcher, DispatcherPriority priority)
        => new DispatcherClipboardClearTimer(dispatcher, priority);

    private sealed class DispatcherClipboardClearTimer : IClipboardClearTimer
    {
        private readonly DispatcherTimer _timer;

        public DispatcherClipboardClearTimer(Dispatcher dispatcher, DispatcherPriority priority)
        {
            _timer = new DispatcherTimer(priority, dispatcher);
        }

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public event EventHandler? Tick
        {
            add => _timer.Tick += value;
            remove => _timer.Tick -= value;
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();
    }
}
