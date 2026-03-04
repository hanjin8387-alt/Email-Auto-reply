using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookItemLauncher : IOutlookItemLauncher
{
    private readonly IOutlookSessionHost _sessionHost;
    private readonly ILogger<OutlookItemLauncher> _logger;

    public OutlookItemLauncher(
        IOutlookSessionHost sessionHost,
        ILogger<OutlookItemLauncher> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
        _logger = logger ?? NullLogger<OutlookItemLauncher>.Instance;
    }

    public async Task OpenItemAsync(string entryId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return;
        }

        try
        {
            await _sessionHost.InvokeAsync(
                ctx => OpenItemInternal(ctx, entryId),
                OutlookOperationPriority.UserInitiated,
                ct).ConfigureAwait(false);
        }
        catch (COMException ex)
        {
            _logger.LogWarning("OpenItem failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("Failed to open Outlook item. Verify Classic Outlook state.");
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("OpenItem failed: {ExceptionType}.", ex.GetType().Name);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("An error occurred while opening the Outlook item.");
        }
    }

    private static void OpenItemInternal(OutlookSessionContext context, string entryId)
    {
        object? raw = null;
        try
        {
            raw = context.Session.GetItemFromID(entryId);
            if (raw is Outlook.MailItem mail)
            {
                mail.Display(false);
            }
        }
        finally
        {
            OutlookSessionHost.SafeReleaseComObject(raw);
        }
    }
}
