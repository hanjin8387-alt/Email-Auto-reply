using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookMailGateway
{
    Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersAsync(CancellationToken ct = default);

    Task<RawEmailContent> GetRawEmailContentAsync(string entryId, OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, RawEmailContent>> GetRawEmailContentsAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);

    Task OpenItemAsync(string entryId, CancellationToken ct = default);

    Task CreateDraftAsync(ReplyDraftRequest request, CancellationToken ct = default);
}
