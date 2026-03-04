using System;

namespace MailTriageAssistant.Services;

public interface IAutoRefreshController : IDisposable
{
    bool IsPaused { get; }

    DateTimeOffset? NextRunAt { get; }

    string StatusText { get; }

    event EventHandler? StateChanged;

    void NotifyManualRunSucceeded();
}
