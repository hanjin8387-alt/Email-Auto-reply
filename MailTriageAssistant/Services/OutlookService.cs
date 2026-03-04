using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class OutlookService : IOutlookService, IOutlookMailGateway, IDisposable
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

    public async Task<List<RawEmailHeader>> FetchInboxHeaders(CancellationToken ct = default)
        => (await _inboxReader.FetchInboxHeadersAsync(ct).ConfigureAwait(false)).ToList();

    public async Task<string> GetBody(string entryId, CancellationToken ct = default)
    {
        var raw = await GetRawEmailContentAsync(
            entryId,
            priority: OutlookOperationPriority.UserInitiated,
            ct).ConfigureAwait(false);

        return raw.Body;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetBodies(IReadOnlyList<string> entryIds, CancellationToken ct = default)
    {
        var raws = await GetRawEmailContentsAsync(
            entryIds,
            priority: OutlookOperationPriority.Background,
            ct).ConfigureAwait(false);

        return raws.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Body, StringComparer.Ordinal);
    }

    public Task OpenItem(string entryId, CancellationToken ct = default)
        => OpenItemAsync(entryId, ct);

    public Task CreateDraft(string to, string subject, string body, CancellationToken ct = default)
        => CreateDraftAsync(new ReplyDraftRequest(to ?? string.Empty, subject ?? string.Empty, body ?? string.Empty), ct);

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

    public Task OpenItemAsync(string entryId, CancellationToken ct = default)
        => _itemLauncher.OpenItemAsync(entryId, ct);

    public Task CreateDraftAsync(ReplyDraftRequest request, CancellationToken ct = default)
        => _draftComposer.CreateDraftAsync(request, ct);

    public void Dispose()
    {
        _sessionHost.Dispose();
    }
}
