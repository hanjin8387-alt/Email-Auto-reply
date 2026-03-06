using System;
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

        _ = await OutlookOperationExecutor.ExecuteAsync(
            _sessionHost,
            _logger,
            operationName: nameof(OpenItemAsync),
            unavailableMessage: "Failed to open Outlook item. Verify Classic Outlook state.",
            failureMessage: "An error occurred while opening the Outlook item.",
            operation: async () =>
            {
                await _sessionHost.InvokeAsync(
                    ctx => OpenItemInternal(ctx, entryId),
                    OutlookOperationPriority.UserInitiated,
                    ct).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
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
