using System;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookDraftComposer : IOutlookDraftComposer
{
    private readonly IOutlookSessionHost _sessionHost;
    private readonly ILogger<OutlookDraftComposer> _logger;

    public OutlookDraftComposer(
        IOutlookSessionHost sessionHost,
        ILogger<OutlookDraftComposer> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
        _logger = logger ?? NullLogger<OutlookDraftComposer>.Instance;
    }

    public async Task CreateDraftAsync(ReplyDraftRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = await OutlookOperationExecutor.ExecuteAsync(
            _sessionHost,
            _logger,
            operationName: nameof(CreateDraftAsync),
            unavailableMessage: "Failed to create Outlook draft. Verify Classic Outlook state.",
            failureMessage: "An error occurred while creating Outlook draft.",
            operation: async () =>
            {
                await _sessionHost.InvokeAsync(
                    ctx => CreateDraftInternal(ctx, request),
                    OutlookOperationPriority.UserInitiated,
                    ct).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
    }

    private static void CreateDraftInternal(OutlookSessionContext context, ReplyDraftRequest request)
    {
        Outlook.MailItem? draft = null;
        try
        {
            draft = (Outlook.MailItem)context.App.CreateItem(Outlook.OlItemType.olMailItem);
            draft.To = request.To ?? string.Empty;
            draft.Subject = request.Subject ?? string.Empty;
            draft.Body = request.Body ?? string.Empty;
            draft.Display(false);
        }
        finally
        {
            OutlookSessionHost.SafeReleaseComObject(draft);
        }
    }
}
