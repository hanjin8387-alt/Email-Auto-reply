using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookInboxReader
{
    Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersAsync(CancellationToken ct = default);

    void InvalidateCache();
}
