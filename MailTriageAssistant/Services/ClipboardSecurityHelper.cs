using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class ClipboardSecurityHelper : IDisposable
{
    private readonly RedactionService _redactionService;
    private readonly ILogger<ClipboardSecurityHelper> _logger;
    private DispatcherTimer? _clearTimer;
    private string? _copiedContent;
    private bool _disposed;

    public ClipboardSecurityHelper(RedactionService redactionService)
        : this(redactionService, NullLogger<ClipboardSecurityHelper>.Instance)
    {
    }

    public ClipboardSecurityHelper(RedactionService redactionService, ILogger<ClipboardSecurityHelper> logger)
    {
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _logger = logger ?? NullLogger<ClipboardSecurityHelper>.Instance;
    }

    public void SecureCopy(string text, bool alreadyRedacted = false)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardSecurityHelper));
        }

        // Enforce redaction before anything leaves the app via the clipboard, unless the input is explicitly pre-redacted.
        var content = alreadyRedacted
            ? (text ?? string.Empty)
            : _redactionService.Redact(text ?? string.Empty);

        Application.Current.Dispatcher.Invoke(() =>
        {
            _copiedContent = content;
            var dataObj = new DataObject();
            dataObj.SetData(DataFormats.UnicodeText, content);
            dataObj.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
            Clipboard.SetDataObject(dataObj, false);
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
            _clearTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(30),
            };
            _clearTimer.Tick += OnClearTimerTick;
        }

        _clearTimer.Stop();
        _clearTimer.Start();
    }

    private void OnClearTimerTick(object? sender, EventArgs e)
    {
        _clearTimer?.Stop();

        try
        {
            var cleared = false;
            if (_copiedContent is not null &&
                Clipboard.ContainsText() &&
                string.Equals(Clipboard.GetText(), _copiedContent, StringComparison.Ordinal))
            {
                Clipboard.Clear();
                cleared = true;
            }

            _logger.LogInformation(cleared ? "Clipboard cleared." : "Clipboard clear skipped.");
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
        }
    }
}
