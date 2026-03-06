using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookSessionHost : IOutlookSessionHost
{
    private readonly ILogger<OutlookSessionHost> _logger;
    private readonly IOutlookCapabilityDetector _capabilityDetector;
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly Thread _comThread;
    private readonly Task<Dispatcher> _comDispatcherTask;
    private readonly SemaphoreSlim _comLock = new(initialCount: 1, maxCount: 1);
    private int _interactiveWaiters;
    private int _userInitiatedWaiters;
    private bool _disposed;
    private Outlook.Application? _app;
    private Outlook.NameSpace? _session;

    public OutlookSessionHost(
        IOutlookCapabilityDetector capabilityDetector,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookSessionHost> logger)
    {
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? NullLogger<OutlookSessionHost>.Instance;

        var tcs = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        _comThread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                tcs.SetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Outlook COM (STA)",
        };

        _comThread.SetApartmentState(ApartmentState.STA);
        _comThread.Start();
        _comDispatcherTask = tcs.Task;
    }

    public async Task<T> InvokeAsync<T>(
        Func<OutlookSessionContext, T> operation,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();

        await EnterPriorityGateAsync(priority, ct).ConfigureAwait(false);
        try
        {
            var dispatcher = await _comDispatcherTask.ConfigureAwait(false);
            var op = dispatcher.InvokeAsync(() =>
            {
                EnsureClassicOutlookOrThrow();
                return operation(new OutlookSessionContext(_app!, _session!));
            });

            var timeout = TimeSpan.FromSeconds(Math.Max(1, _optionsMonitor.CurrentValue.ComTimeoutSeconds));
            var task = op.Task;
            var timeoutOrCancel = Task.Delay(timeout, ct);
            var completed = await Task.WhenAny(task, timeoutOrCancel).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                try
                {
                    op.Abort();
                }
                catch
                {
                    // Ignore abort failures.
                }

                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("Outlook COM operation timed out.");
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            _comLock.Release();
        }
    }

    public Task InvokeAsync(
        Action<OutlookSessionContext> operation,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return InvokeAsync(
            ctx =>
            {
                operation(ctx);
                return 0;
            },
            priority,
            ct);
    }

    public void ResetConnection()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_comDispatcherTask.IsCompletedSuccessfully)
            {
                var dispatcher = _comDispatcherTask.Result;
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                {
                    if (dispatcher.CheckAccess())
                    {
                        ResetConnectionCore();
                        return;
                    }

                    try
                    {
                        dispatcher.Invoke(ResetConnectionCore);
                        return;
                    }
                    catch
                    {
                        // Fall through to best-effort direct reset.
                    }
                }
            }

            ResetConnectionCore();
        }
        catch
        {
            // Connection reset is best effort and must not mask the original Outlook failure.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_comDispatcherTask.IsCompletedSuccessfully)
            {
                var dispatcher = _comDispatcherTask.Result;
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                {
                    dispatcher.Invoke(() =>
                    {
                        try
                        {
                            ResetConnectionCore();
                        }
                        catch
                        {
                            // Ignore disposal errors.
                        }
                    });

                    dispatcher.InvokeShutdown();
                }
            }
        }
        catch
        {
            // Ignore dispatcher shutdown issues.
        }

        try
        {
            _comThread.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore join failures.
        }

        _comLock.Dispose();
    }

    private async Task EnterPriorityGateAsync(OutlookOperationPriority priority, CancellationToken ct)
    {
        if (priority == OutlookOperationPriority.Interactive)
        {
            await WaitWithCounterAsync(
                ct,
                onEnter: () => Interlocked.Increment(ref _interactiveWaiters),
                onExit: () => Interlocked.Decrement(ref _interactiveWaiters),
                waitForHigherPriority: static () => false).ConfigureAwait(false);
            return;
        }

        if (priority == OutlookOperationPriority.UserInitiated)
        {
            await WaitWithCounterAsync(
                ct,
                onEnter: () => Interlocked.Increment(ref _userInitiatedWaiters),
                onExit: () => Interlocked.Decrement(ref _userInitiatedWaiters),
                waitForHigherPriority: () => Volatile.Read(ref _interactiveWaiters) > 0).ConfigureAwait(false);
            return;
        }

        while (Volatile.Read(ref _interactiveWaiters) > 0 || Volatile.Read(ref _userInitiatedWaiters) > 0)
        {
            await Task.Delay(15, ct).ConfigureAwait(false);
        }

        await _comLock.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task WaitWithCounterAsync(
        CancellationToken ct,
        Action onEnter,
        Action onExit,
        Func<bool> waitForHigherPriority)
    {
        onEnter();
        try
        {
            while (waitForHigherPriority())
            {
                await Task.Delay(15, ct).ConfigureAwait(false);
            }

            await _comLock.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            onExit();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OutlookSessionHost));
        }
    }

    private void EnsureClassicOutlookOrThrow()
    {
        if (_app is not null && _session is not null)
        {
            return;
        }

        var capability = _capabilityDetector.GetSnapshot();
        if (capability.IsNewOutlookOnly)
        {
            throw new NotSupportedException(
                $"Classic Outlook is required. New Outlook only environment detected ({capability.DiagnosticCode}).");
        }

        try
        {
            _app = GetActiveOrCreateOutlookApplication();
            _session = _app.Session;
            _session ??= _app.GetNamespace("MAPI");

            if (_session is null)
            {
                throw new InvalidOperationException("Outlook MAPI session was null.");
            }
        }
        catch (COMException ex)
        {
            _logger.LogWarning("Outlook connect failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            ResetConnection();
            if (capability.IsNewOutlookOnly)
            {
                throw new NotSupportedException(
                    $"Classic Outlook is required. New Outlook only environment detected ({capability.DiagnosticCode}).");
            }

            throw new InvalidOperationException(
                $"Outlook is not available (diagnostic={capability.DiagnosticCode}). Please start Classic Outlook first.");
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Outlook connect failed: {ExceptionType}.", ex.GetType().Name);
            ResetConnection();
            throw new InvalidOperationException(
                $"Outlook connection failed (diagnostic={capability.DiagnosticCode}). Verify Classic Outlook state.");
        }
    }

    private Outlook.Application GetActiveOrCreateOutlookApplication()
    {
        try
        {
            return GetActiveOutlookApplication();
        }
        catch (COMException ex)
        {
            _logger.LogInformation(
                "GetActiveObject(Outlook.Application) failed: {ExceptionType} (HResult={HResult}). Trying new Outlook.Application.",
                ex.GetType().Name,
                ex.HResult);
            return new Outlook.Application();
        }
    }

    private static Outlook.Application GetActiveOutlookApplication()
    {
        var hr = CLSIDFromProgID("Outlook.Application", out var clsid);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = GetActiveObject(ref clsid, IntPtr.Zero, out var punk);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            return (Outlook.Application)Marshal.GetObjectForIUnknown(punk);
        }
        finally
        {
            Marshal.Release(punk);
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, out IntPtr ppunk);

    internal static void SafeReleaseComObject(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(obj))
            {
                Marshal.FinalReleaseComObject(obj);
            }
        }
        catch
        {
            // Ignore COM release issues.
        }
    }

    private void ResetConnectionCore()
    {
        SafeReleaseComObject(_session);
        SafeReleaseComObject(_app);
        _session = null;
        _app = null;
        _capabilityDetector.Invalidate();
    }
}
