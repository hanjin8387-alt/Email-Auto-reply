using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class AutoRefreshController : IDisposable
{
    private readonly DispatcherTimer _autoRefreshTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<TriageSettings> _settingsMonitor;
    private readonly Func<CancellationToken, Task<InboxRefreshOutcome>> _refreshOperation;
    private readonly Func<bool> _isLoading;
    private readonly Action<string> _setStatusMessage;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AutoRefreshController> _logger;
    private readonly IDisposable? _settingsSubscription;
    private CancellationTokenSource? _refreshCts;
    private int _failureStreak;

    public AutoRefreshController(
        Dispatcher dispatcher,
        IClock clock,
        IOptionsMonitor<TriageSettings> settingsMonitor,
        Func<CancellationToken, Task<InboxRefreshOutcome>> refreshOperation,
        Func<bool> isLoading,
        Action<string> setStatusMessage,
        IDialogService dialogService,
        ILogger<AutoRefreshController> logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _refreshOperation = refreshOperation ?? throw new ArgumentNullException(nameof(refreshOperation));
        _isLoading = isLoading ?? throw new ArgumentNullException(nameof(isLoading));
        _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<AutoRefreshController>.Instance;

        var resolvedDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _autoRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, resolvedDispatcher)
        {
            IsEnabled = false,
        };
        _autoRefreshTimer.Tick += OnAutoRefreshTick;

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background, resolvedDispatcher)
        {
            IsEnabled = false,
            Interval = TimeSpan.FromMinutes(1),
        };
        _statusTimer.Tick += (_, _) => PublishState();

        _settingsSubscription = _settingsMonitor.OnChange((settings, _) => ApplySettings(settings));
        ApplySettings(_settingsMonitor.CurrentValue);
    }

    public bool IsPaused { get; private set; }

    public DateTimeOffset? NextRunAt { get; private set; }

    public string StatusText { get; private set; } = string.Empty;

    public event EventHandler? StateChanged;

    public void ApplySettings(TriageSettings settings)
    {
        var minutes = GetIntervalMinutes(settings);
        if (minutes <= 0)
        {
            Stop();
            return;
        }

        _statusTimer.Start();
        _autoRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);
        if (IsPaused)
        {
            _autoRefreshTimer.Stop();
            NextRunAt = null;
            PublishState();
            return;
        }

        ResetTimer();
    }

    public void NotifyManualRunSucceeded()
    {
        _failureStreak = 0;
        if (IsPaused)
        {
            IsPaused = false;
        }

        ResetTimer();
    }

    public void Stop()
    {
        _autoRefreshTimer.Stop();
        _statusTimer.Stop();

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;

        _failureStreak = 0;
        IsPaused = false;
        NextRunAt = null;
        PublishState();
    }

    public void Dispose()
    {
        try
        {
            Stop();
        }
        catch
        {
            // Ignore timer stop failures.
        }

        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
        _settingsSubscription?.Dispose();
    }

    private void ResetTimer()
    {
        var minutes = GetIntervalMinutes(_settingsMonitor.CurrentValue);
        if (minutes <= 0 || IsPaused)
        {
            _autoRefreshTimer.Stop();
            NextRunAt = null;
            PublishState();
            return;
        }

        _autoRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Start();
        NextRunAt = _clock.Now.AddMinutes(minutes);
        PublishState();
    }

    private static int GetIntervalMinutes(TriageSettings settings)
        => Math.Max(0, settings.AutoRefreshIntervalMinutes);

    private async void OnAutoRefreshTick(object? sender, EventArgs e)
    {
        _autoRefreshTimer.Stop();
        try
        {
            await HandleAutoRefreshTickAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Auto refresh tick failed: {ExceptionType}.", ex.GetType().Name);
        }
    }

    private async Task HandleAutoRefreshTickAsync()
    {
        if (IsPaused)
        {
            return;
        }

        if (GetIntervalMinutes(_settingsMonitor.CurrentValue) <= 0)
        {
            Stop();
            return;
        }

        if (_isLoading())
        {
            ResetTimer();
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();

        var outcome = await _refreshOperation(_refreshCts.Token).ConfigureAwait(true);
        if (outcome == InboxRefreshOutcome.Success)
        {
            _failureStreak = 0;
            ResetTimer();
            return;
        }

        if (outcome == InboxRefreshOutcome.Failure)
        {
            _failureStreak++;
            var threshold = Math.Max(1, _settingsMonitor.CurrentValue.AutoRefreshFailurePauseThreshold);
            if (_failureStreak >= threshold)
            {
                IsPaused = true;
                NextRunAt = null;
                var message = LocalizedStrings.Get(
                    "Str.Status.AutoRefreshPausedByFailures",
                    "Auto refresh paused after repeated failures.");
                _setStatusMessage(message);
                _dialogService.ShowWarning(
                    message,
                    LocalizedStrings.Get("Str.Dialog.AutoRefreshTitle", "Auto Refresh"));
                PublishState();
                return;
            }
        }

        ResetTimer();
    }

    internal Task TriggerTickForTestAsync()
        => HandleAutoRefreshTickAsync();

    private void PublishState()
    {
        var configuredMinutes = GetIntervalMinutes(_settingsMonitor.CurrentValue);
        if (configuredMinutes <= 0)
        {
            StatusText = string.Empty;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsPaused)
        {
            StatusText = LocalizedStrings.Get("Str.Status.AutoRefreshPaused", "Auto refresh: Paused");
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (NextRunAt is null)
        {
            StatusText = LocalizedStrings.GetFormat(
                "Str.Status.AutoRefreshNextInMinutes",
                "Next refresh in {0} min",
                configuredMinutes);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var remaining = NextRunAt.Value - _clock.Now;
        if (remaining <= TimeSpan.Zero)
        {
            StatusText = LocalizedStrings.Get("Str.Status.AutoRefreshSoon", "Next refresh soon");
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var remainingMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
        StatusText = LocalizedStrings.GetFormat(
            "Str.Status.AutoRefreshNextInMinutes",
            "Next refresh in {0} min",
            remainingMinutes);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
