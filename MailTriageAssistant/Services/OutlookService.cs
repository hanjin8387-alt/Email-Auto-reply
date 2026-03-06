using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class OutlookService : IOutlookMailGateway, IDisposable
{
    private readonly IOutlookInboxReader _inboxReader;
    private readonly IOutlookBodyReader _bodyReader;
    private readonly IOutlookDraftComposer _draftComposer;
    private readonly IOutlookItemLauncher _itemLauncher;
    private readonly IOutlookSessionHost _sessionHost;

    public OutlookService(
        IOutlookInboxReader inboxReader,
        IOutlookBodyReader bodyReader,
        IOutlookDraftComposer draftComposer,
        IOutlookItemLauncher itemLauncher,
        IOutlookSessionHost sessionHost)
    {
        _inboxReader = inboxReader ?? throw new ArgumentNullException(nameof(inboxReader));
        _bodyReader = bodyReader ?? throw new ArgumentNullException(nameof(bodyReader));
        _draftComposer = draftComposer ?? throw new ArgumentNullException(nameof(draftComposer));
        _itemLauncher = itemLauncher ?? throw new ArgumentNullException(nameof(itemLauncher));
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
    }

    public Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersAsync(CancellationToken ct = default)
        => _inboxReader.FetchInboxHeadersAsync(ct);

    public Task<RawEmailContent> GetRawEmailContentAsync(
        string entryId,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
        => _bodyReader.GetRawEmailContentAsync(entryId, priority, ct);

    public Task<IReadOnlyDictionary<string, RawEmailContent>> GetRawEmailContentsAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
        => _bodyReader.GetRawEmailContentsAsync(entryIds, priority, ct);

    public Task<RawEmailBodyBatchResult> GetRawEmailContentsBatchAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
        => _bodyReader.GetRawEmailContentsBatchAsync(entryIds, priority, ct);

    public Task OpenItemAsync(string entryId, CancellationToken ct = default)
        => _itemLauncher.OpenItemAsync(entryId, ct);

    public Task CreateDraftAsync(ReplyDraftRequest request, CancellationToken ct = default)
        => _draftComposer.CreateDraftAsync(request, ct);

    public void Dispose()
    {
        _sessionHost.Dispose();
    }
}
