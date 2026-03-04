using System;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Tests;

internal sealed class FakeOptionsMonitor<T> : IOptionsMonitor<T>
{
    private T _currentValue;

    public FakeOptionsMonitor(T value)
    {
        _currentValue = value;
    }

    public T CurrentValue => _currentValue;

    public T Get(string? name) => _currentValue;

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        Listener = listener;
        return new CallbackDisposable(() => Listener = null);
    }

    public Action<T, string?>? Listener { get; private set; }

    public void Set(T value, string? name = null)
    {
        _currentValue = value;
        Listener?.Invoke(value, name);
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action _dispose;

        public CallbackDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }
}

internal sealed class FakeClock : MailTriageAssistant.Services.IClock
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    public void Advance(TimeSpan delta)
    {
        Now = Now.Add(delta);
        UtcNow = UtcNow.Add(delta);
    }
}
