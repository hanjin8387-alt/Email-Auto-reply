using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookDraftComposer
{
    Task CreateDraftAsync(ReplyDraftRequest request, CancellationToken ct = default);
}
