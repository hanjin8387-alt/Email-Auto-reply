using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class ClipboardSecurityHelper : IDisposable
{
    private readonly IRedactionService _redactionService;
    private readonly IClipboardService _clipboardService;
    private readonly IOptionsMonitor<TriageSettings>? _settingsMonitor;
    private readonly ILogger<ClipboardSecurityHelper> _logger;
    private DispatcherTimer? _clearTimer;
    private string? _copiedContent;
    private uint _copiedSequenceNumber;
    private bool _hasCopiedSequenceNumber;
    private bool _disposed;

    public ClipboardSecurityHelper(IRedactionService redactionService)
        : this(redactionService, new WpfClipboardService(), settingsMonitor: null, NullLogger<ClipboardSecurityHelper>.Instance)
    {
    }

    public ClipboardSecurityHelper(IRedactionService redactionService, ILogger<ClipboardSecurityHelper> logger)
        : this(redactionService, new WpfClipboardService(), settingsMonitor: null, logger)
    {
    }

    public ClipboardSecurityHelper(
        IRedactionService redactionService,
        IClipboardService clipboardService,
        IOptionsMonitor<TriageSettings>? settingsMonitor,
        ILogger<ClipboardSecurityHelper> logger)
    {
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _settingsMonitor = settingsMonitor;
        _logger = logger ?? NullLogger<ClipboardSecurityHelper>.Instance;
    }

    public void SecureCopy(string text)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardSecurityHelper));
        }

        // Enforce redaction before anything leaves the app via the clipboard.
        var content = _redactionService.Redact(text ?? string.Empty);
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        dispatcher.Invoke(() =>
        {
            _copiedContent = content;
            _clipboardService.SetText(content);
            _copiedSequenceNumber = _clipboardService.GetSequenceNumber();
            _hasCopiedSequenceNumber = true;
            StartClearTimer();
        });

        _logger.LogInformation("Clipboard set; auto-clear scheduled.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _copiedContent = null;
        _hasCopiedSequenceNumber = false;
        _copiedSequenceNumber = 0;

        if (_clearTimer is null)
        {
            return;
        }

        _clearTimer.Stop();
        _clearTimer.Tick -= OnClearTimerTick;
        _clearTimer = null;
    }

    private void StartClearTimer()
    {
        if (_clearTimer is null)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _clearTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(GetAutoClearSeconds()),
            };
            _clearTimer.Tick += OnClearTimerTick;
        }

        _clearTimer.Interval = TimeSpan.FromSeconds(GetAutoClearSeconds());
        _clearTimer.Stop();
        _clearTimer.Start();
    }

    private void OnClearTimerTick(object? sender, EventArgs e)
    {
        _clearTimer?.Stop();

        try
        {
            if (_copiedContent is not null && _hasCopiedSequenceNumber)
            {
                var currentSequence = _clipboardService.GetSequenceNumber();
                if (currentSequence != _copiedSequenceNumber)
                {
                    _logger.LogInformation("Clipboard clear skipped (sequence changed).");
                    return;
                }
            }

            if (_copiedContent is not null &&
                _clipboardService.ContainsText() &&
                string.Equals(_clipboardService.GetText(), _copiedContent, StringComparison.Ordinal))
            {
                _clipboardService.Clear();
                _logger.LogInformation("Clipboard cleared.");
            }
            else
            {
                _logger.LogInformation("Clipboard clear skipped.");
            }
        }
        catch (Exception ex)
        {
            // Ignore clipboard failures (e.g. locked by another process).
            // Do not log exception messages since they could contain clipboard text from other apps.
            _logger.LogWarning("Clipboard clear failed: {ExceptionType}.", ex.GetType().Name);
        }
        finally
        {
            _copiedContent = null;
            _hasCopiedSequenceNumber = false;
            _copiedSequenceNumber = 0;
        }
    }

    private int GetAutoClearSeconds()
    {
        var configured = _settingsMonitor?.CurrentValue?.ClipboardAutoClearSeconds ?? 30;
        return Math.Clamp(configured, 5, 300);
    }
}
