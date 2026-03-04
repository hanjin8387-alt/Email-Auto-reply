using System;
using System.Threading;
using System.Threading.Tasks;

namespace MailTriageAssistant.Services;

public interface IAutoRefreshControllerFactory
{
    IAutoRefreshController Create(
        Func<CancellationToken, Task<InboxRefreshOutcome>> refreshOperation,
        Func<bool> isLoading,
        Action<string> setStatusMessage);
}
