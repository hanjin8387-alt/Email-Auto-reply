using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MailTriageAssistant.Services;

internal static class OutlookOperationExecutor
{
    public static async Task<T> ExecuteAsync<T>(
        IOutlookSessionHost sessionHost,
        ILogger logger,
        string operationName,
        string unavailableMessage,
        string failureMessage,
        Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (COMException ex)
        {
            logger.LogWarning(
                "{OperationName} failed: {ExceptionType} (HResult={HResult}).",
                operationName,
                ex.GetType().Name,
                ex.HResult);
            TryResetConnection(sessionHost, logger, operationName);
            throw new InvalidOperationException(unavailableMessage);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(
                "{OperationName} timed out: {ExceptionType}.",
                operationName,
                ex.GetType().Name);
            TryResetConnection(sessionHost, logger, operationName);
            throw new InvalidOperationException(unavailableMessage);
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                "{OperationName} failed: {ExceptionType}.",
                operationName,
                ex.GetType().Name);
            TryResetConnection(sessionHost, logger, operationName);
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static void TryResetConnection(IOutlookSessionHost sessionHost, ILogger logger, string operationName)
    {
        try
        {
            sessionHost.ResetConnection();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "{OperationName} reset skipped: {ExceptionType}.",
                operationName,
                ex.GetType().Name);
        }
    }
}
