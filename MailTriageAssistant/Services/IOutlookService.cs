using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookService
{
    Task<List<RawEmailHeader>> FetchInboxHeaders(CancellationToken ct = default);

    Task<string> GetBody(string entryId, CancellationToken ct = default);

    Task OpenItem(string entryId, CancellationToken ct = default);

    Task CreateDraft(string to, string subject, string body, CancellationToken ct = default);
}
