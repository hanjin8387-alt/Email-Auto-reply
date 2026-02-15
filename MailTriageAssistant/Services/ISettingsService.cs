using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MailTriageAssistant.Services;

public interface ISettingsService
{
    Task<IReadOnlyList<string>> LoadVipSendersAsync(CancellationToken ct = default);

    Task SaveVipSendersAsync(IEnumerable<string> vipSenders, CancellationToken ct = default);
}

