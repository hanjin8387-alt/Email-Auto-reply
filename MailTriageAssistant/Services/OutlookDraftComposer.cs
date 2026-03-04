using System;
using System.Runtime.InteropServices;
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

        try
        {
            await _sessionHost.InvokeAsync(
                ctx => CreateDraftInternal(ctx, request),
                OutlookOperationPriority.UserInitiated,
                ct).ConfigureAwait(false);
        }
        catch (COMException ex)
        {
            _logger.LogWarning("CreateDraft failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("Failed to create Outlook draft. Verify Classic Outlook state.");
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
            _logger.LogError("CreateDraft failed: {ExceptionType}.", ex.GetType().Name);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("An error occurred while creating Outlook draft.");
        }
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
