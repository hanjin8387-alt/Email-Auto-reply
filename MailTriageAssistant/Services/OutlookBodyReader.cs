using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookBodyReader : IOutlookBodyReader
{
    private readonly IOutlookSessionHost _sessionHost;
    private readonly SessionStatsService _sessionStats;
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly ILogger<OutlookBodyReader> _logger;
    private readonly ConcurrentDictionary<string, InFlightBodyLoad> _inFlightByEntryId = new(StringComparer.Ordinal);

    public OutlookBodyReader(
        IOutlookSessionHost sessionHost,
        SessionStatsService sessionStats,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookBodyReader> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? NullLogger<OutlookBodyReader>.Instance;
    }

    public async Task<RawEmailContent> GetRawEmailContentAsync(
        string entryId,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return RawEmailContent.Empty;
        }

        while (true)
        {
            if (_inFlightByEntryId.TryGetValue(entryId, out var existing))
            {
                if (IsHigherPriority(priority, existing.Priority))
                {
                    return await LoadRawEmailContentCoreAsync(entryId, priority, ct).ConfigureAwait(false);
                }

                return await WaitForSharedLoadAsync(existing, ct).ConfigureAwait(false);
            }

            var created = CreateInFlight(entryId, priority);
            if (_inFlightByEntryId.TryAdd(entryId, created))
            {
                return await WaitForSharedLoadAsync(created, ct).ConfigureAwait(false);
            }

            created.Cancel();
            created.Dispose();
        }
    }

    public async Task<RawEmailBodyBatchResult> GetRawEmailContentsBatchAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entryIds);

        var loadedByEntryId = new Dictionary<string, RawEmailContent>(StringComparer.Ordinal);
        var distinct = entryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var failedCount = 0;
        var canceledCount = 0;

        foreach (var entryId in distinct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                loadedByEntryId[entryId] = await GetRawEmailContentAsync(entryId, priority, ct).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                failedCount++;
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }

                canceledCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogDebug("GetRawEmailContents batch item skipped due to {ExceptionType}.", ex.GetType().Name);
            }
        }

        var result = new RawEmailBodyBatchResult(
            LoadedByEntryId: loadedByEntryId,
            RequestedCount: distinct.Count,
            LoadedCount: loadedByEntryId.Count,
            FailedCount: failedCount,
            CanceledCount: canceledCount);

        _sessionStats.RecordBodyBatch(
            requestedCount: result.RequestedCount,
            loadedCount: result.LoadedCount,
            failedCount: result.FailedCount,
            canceledCount: result.CanceledCount);

        if (result.FailedCount > 0 || result.CanceledCount > 0)
        {
            _logger.LogInformation(
                "Body batch completed with partial failures (priority={Priority}, requested={Requested}, loaded={Loaded}, failed={Failed}, canceled={Canceled}).",
                priority,
                result.RequestedCount,
                result.LoadedCount,
                result.FailedCount,
                result.CanceledCount);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, RawEmailContent>> GetRawEmailContentsAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        var batch = await GetRawEmailContentsBatchAsync(entryIds, priority, ct).ConfigureAwait(false);
        return batch.LoadedByEntryId;
    }

    private async Task<RawEmailContent> WaitForSharedLoadAsync(InFlightBodyLoad inFlight, CancellationToken ct)
    {
        inFlight.AddWaiter();
        try
        {
            return await inFlight.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            if (inFlight.RemoveWaiter() == 0 && !inFlight.Task.IsCompleted)
            {
                inFlight.Cancel();
            }
        }
    }

    private InFlightBodyLoad CreateInFlight(string entryId, OutlookOperationPriority priority)
    {
        var cts = new CancellationTokenSource();
        var task = LoadRawEmailContentCoreAsync(entryId, priority, cts.Token);
        var inFlight = new InFlightBodyLoad(task, cts, priority);

        _ = task.ContinueWith(
            _ =>
            {
                _inFlightByEntryId.TryRemove(new KeyValuePair<string, InFlightBodyLoad>(entryId, inFlight));
                inFlight.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return inFlight;
    }

    private static bool IsHigherPriority(OutlookOperationPriority candidate, OutlookOperationPriority baseline)
        => (int)candidate < (int)baseline;

    private async Task<RawEmailContent> LoadRawEmailContentCoreAsync(
        string entryId,
        OutlookOperationPriority priority,
        CancellationToken ct)
    {
        try
        {
            return await _sessionHost.InvokeAsync(
                ctx => GetRawEmailContentInternal(ctx, entryId, Math.Max(200, _optionsMonitor.CurrentValue.MaxBodyLength)),
                priority,
                ct).ConfigureAwait(false);
        }
        catch (COMException ex)
        {
            _logger.LogWarning("GetBody failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("Failed to load Outlook email body. Verify Classic Outlook state.");
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
            _logger.LogError("GetBody failed: {ExceptionType}.", ex.GetType().Name);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("An error occurred while loading Outlook email body.");
        }
    }

    private static RawEmailContent GetRawEmailContentInternal(OutlookSessionContext context, string entryId, int maxBodyLength)
    {
        object? raw = null;
        try
        {
            raw = context.Session.GetItemFromID(entryId);
            if (raw is not Outlook.MailItem mail)
            {
                return RawEmailContent.Empty;
            }

            var body = mail.Body ?? string.Empty;
            if (body.Length > maxBodyLength)
            {
                body = body[..maxBodyLength];
            }

            return new RawEmailContent(
                SenderName: mail.SenderName ?? string.Empty,
                SenderEmail: mail.SenderEmailAddress ?? string.Empty,
                Subject: mail.Subject ?? string.Empty,
                Body: body);
        }
        finally
        {
            OutlookSessionHost.SafeReleaseComObject(raw);
        }
    }

    private sealed class InFlightBodyLoad : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private int _waiterCount;

        public InFlightBodyLoad(Task<RawEmailContent> task, CancellationTokenSource cts, OutlookOperationPriority priority)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
            Priority = priority;
        }

        public Task<RawEmailContent> Task { get; }

        public OutlookOperationPriority Priority { get; }

        public void AddWaiter() => Interlocked.Increment(ref _waiterCount);

        public int RemoveWaiter() => Interlocked.Decrement(ref _waiterCount);

        public void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
                // Ignore cancellation races.
            }
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}
