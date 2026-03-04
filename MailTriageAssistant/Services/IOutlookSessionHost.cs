using System;
using System.Threading;
using System.Threading.Tasks;

namespace MailTriageAssistant.Services;

public interface IOutlookSessionHost : IDisposable
{
    Task<T> InvokeAsync<T>(
        Func<OutlookSessionContext, T> operation,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);

    Task InvokeAsync(
        Action<OutlookSessionContext> operation,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default);

    void ResetConnection();
}
