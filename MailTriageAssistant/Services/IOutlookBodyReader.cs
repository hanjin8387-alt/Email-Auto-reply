using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookBodyReader
{
    Task<RawEmailContent> GetRawEmailContentAsync(
        string entryId,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);

    Task<RawEmailBodyBatchResult> GetRawEmailContentsBatchAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, RawEmailContent>> GetRawEmailContentsAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);
}
