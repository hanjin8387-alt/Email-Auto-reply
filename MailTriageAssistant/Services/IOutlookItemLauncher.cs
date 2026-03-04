using System.Threading;
using System.Threading.Tasks;

namespace MailTriageAssistant.Services;

public interface IOutlookItemLauncher
{
    Task OpenItemAsync(string entryId, CancellationToken ct = default);
}
