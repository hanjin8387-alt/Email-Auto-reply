using System;
using System.Windows;
using System.Windows.Threading;

namespace MailTriageAssistant.Services;

public sealed class ClipboardSecurityHelper : IDisposable
{
    private readonly RedactionService _redactionService;
    private DispatcherTimer? _clearTimer;
    private string? _copiedContent;
    private bool _disposed;

    public ClipboardSecurityHelper(RedactionService redactionService)
    {
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
    }

    public void SecureCopy(string text)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardSecurityHelper));
        }

        // Enforce redaction before anything leaves the app via the clipboard.
        var redacted = _redactionService.Redact(text ?? string.Empty);

        Application.Current.Dispatcher.Invoke(() =>
        {
            _copiedContent = redacted;
            var dataObj = new DataObject();
            dataObj.SetData(DataFormats.UnicodeText, redacted);
            dataObj.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
            Clipboard.SetDataObject(dataObj, false);
            StartClearTimer();
        });
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
            if (_copiedContent is not null &&
                Clipboard.ContainsText() &&
                string.Equals(Clipboard.GetText(), _copiedContent, StringComparison.Ordinal))
            {
                Clipboard.Clear();
            }
        }
        catch
        {
            // Ignore clipboard failures (e.g. locked by another process).
        }
        finally
        {
            _copiedContent = null;
        }
    }
}
