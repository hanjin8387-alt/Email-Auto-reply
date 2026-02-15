using System;
using System.Windows;
using System.Windows.Threading;

namespace MailTriageAssistant.Services;

public sealed class ClipboardSecurityHelper
{
    private readonly RedactionService _redactionService;
    private DispatcherTimer? _clearTimer;
    private string? _copiedContent;

    public ClipboardSecurityHelper(RedactionService redactionService)
    {
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
    }

    public void SecureCopy(string text)
    {
        // Enforce redaction before anything leaves the app via the clipboard.
        var redacted = _redactionService.Redact(text ?? string.Empty);

        Application.Current.Dispatcher.Invoke(() =>
        {
            _copiedContent = redacted;
            Clipboard.SetText(redacted);
            StartClearTimer();
        });
    }

    private void StartClearTimer()
    {
        _clearTimer?.Stop();

        _clearTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(30),
        };

        _clearTimer.Tick += (_, _) =>
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
        };

        _clearTimer.Start();
    }
}

