using System;
using System.Collections.Generic;
using System.Globalization;
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

public sealed class OutlookInboxReader : IOutlookInboxReader
{
    private readonly IOutlookSessionHost _sessionHost;
    private readonly SessionStatsService _sessionStats;
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly ILogger<OutlookInboxReader> _logger;
    private readonly object _cacheGate = new();
    private readonly object _inFlightGate = new();
    private DateTimeOffset _headersCacheUtc;
    private List<RawEmailHeader>? _headersCache;
    private InFlightHeaderFetch? _inFlightFetch;

    public OutlookInboxReader(
        IOutlookSessionHost sessionHost,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookInboxReader> logger)
        : this(sessionHost, new SessionStatsService(), optionsMonitor, logger)
    {
    }

    public OutlookInboxReader(
        IOutlookSessionHost sessionHost,
        SessionStatsService sessionStats,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookInboxReader> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? NullLogger<OutlookInboxReader>.Instance;
    }

    public Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersAsync(CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var cacheTtl = TimeSpan.FromSeconds(Math.Max(1, options.HeadersCacheTtlSeconds));

        lock (_cacheGate)
        {
            if (_headersCache is not null && DateTimeOffset.UtcNow - _headersCacheUtc <= cacheTtl)
            {
                return Task.FromResult<IReadOnlyList<RawEmailHeader>>(new List<RawEmailHeader>(_headersCache));
            }
        }

        InFlightHeaderFetch inFlight;
        lock (_inFlightGate)
        {
            if (_inFlightFetch is null)
            {
                var cts = new CancellationTokenSource();
                var task = FetchInboxHeadersCoreAsync(cts.Token);
                inFlight = new InFlightHeaderFetch(task, cts);
                _inFlightFetch = inFlight;

                _ = task.ContinueWith(
                    _ =>
                    {
                        lock (_inFlightGate)
                        {
                            if (ReferenceEquals(_inFlightFetch, inFlight))
                            {
                                _inFlightFetch = null;
                            }
                        }

                        inFlight.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            else
            {
                inFlight = _inFlightFetch;
            }
        }

        return WaitForSharedFetchAsync(inFlight, ct);
    }

    public void InvalidateCache()
    {
        lock (_cacheGate)
        {
            _headersCache = null;
            _headersCacheUtc = default;
        }
    }

    private async Task<IReadOnlyList<RawEmailHeader>> WaitForSharedFetchAsync(InFlightHeaderFetch inFlight, CancellationToken ct)
    {
        inFlight.AddWaiter();
        try
        {
            var headers = await inFlight.Task.WaitAsync(ct).ConfigureAwait(false);
            return new List<RawEmailHeader>(headers);
        }
        finally
        {
            if (inFlight.RemoveWaiter() == 0 && !inFlight.Task.IsCompleted)
            {
                inFlight.Cancel();
            }
        }
    }

    private async Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersCoreAsync(CancellationToken ct)
    {
        var fetchResult = await OutlookOperationExecutor.ExecuteAsync(
            _sessionHost,
            _logger,
            operationName: nameof(FetchInboxHeadersAsync),
            unavailableMessage: "Failed to communicate with Outlook. Verify Classic Outlook is running.",
            failureMessage: "An error occurred while loading inbox headers.",
            operation: () => _sessionHost.InvokeAsync(
                ctx => FetchInboxHeadersInternal(ctx, _optionsMonitor.CurrentValue),
                OutlookOperationPriority.UserInitiated,
                ct)).ConfigureAwait(false);

        lock (_cacheGate)
        {
            _headersCacheUtc = DateTimeOffset.UtcNow;
            _headersCache = fetchResult.Headers;
        }

        var skippedCount = fetchResult.SkippedItemCount + fetchResult.MalformedItemCount;
        if (skippedCount > 0)
        {
            _sessionStats.RecordOutlookItemsSkipped(skippedCount);
            _logger.LogInformation(
                "FetchInboxHeaders skipped items (skipped={Skipped}, malformed={Malformed}).",
                fetchResult.SkippedItemCount,
                fetchResult.MalformedItemCount);
        }

        return new List<RawEmailHeader>(fetchResult.Headers);
    }

    private static InboxHeaderFetchResult FetchInboxHeadersInternal(OutlookSessionContext context, OutlookOptions options)
    {
        Outlook.MAPIFolder? inbox = null;
        Outlook.Items? items = null;
        Outlook.Items? filteredItems = null;
        var filteredItemsIsSeparate = false;
        object? raw = null;
        var skippedItemCount = 0;
        var malformedItemCount = 0;

        try
        {
            inbox = context.Session.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);
            items = inbox.Items;
            filteredItems = TryRestrictRecentItems(items, Math.Max(1, options.RestrictDays), out filteredItemsIsSeparate);

            var result = new List<RawEmailHeader>(capacity: Math.Max(1, options.MaxFetchCount));
            raw = filteredItems.GetFirst();

            while (raw is not null && result.Count < Math.Max(1, options.MaxFetchCount))
            {
                var current = raw;
                raw = null;

                try
                {
                    if (current is Outlook.MailItem mail)
                    {
                        Outlook.Attachments? attachments = null;
                        bool hasAttachments;
                        try
                        {
                            attachments = mail.Attachments;
                            hasAttachments = attachments is not null && attachments.Count > 0;
                        }
                        finally
                        {
                            OutlookSessionHost.SafeReleaseComObject(attachments);
                        }

                        result.Add(new RawEmailHeader
                        {
                            EntryId = mail.EntryID ?? string.Empty,
                            SenderName = mail.SenderName ?? string.Empty,
                            SenderEmail = mail.SenderEmailAddress ?? string.Empty,
                            Subject = mail.Subject ?? string.Empty,
                            ReceivedTime = mail.ReceivedTime,
                            HasAttachments = hasAttachments,
                        });
                    }
                    else
                    {
                        skippedItemCount++;
                    }
                }
                catch
                {
                    malformedItemCount++;
                }
                finally
                {
                    OutlookSessionHost.SafeReleaseComObject(current);
                }

                raw = filteredItems.GetNext();
            }

            result.Sort((a, b) => b.ReceivedTime.CompareTo(a.ReceivedTime));
            return new InboxHeaderFetchResult(result, skippedItemCount, malformedItemCount);
        }
        finally
        {
            OutlookSessionHost.SafeReleaseComObject(raw);
            if (filteredItemsIsSeparate)
            {
                OutlookSessionHost.SafeReleaseComObject(filteredItems);
            }

            OutlookSessionHost.SafeReleaseComObject(items);
            OutlookSessionHost.SafeReleaseComObject(inbox);
        }
    }

    private static Outlook.Items TryRestrictRecentItems(Outlook.Items items, int days, out bool isSeparateComObject)
    {
        isSeparateComObject = false;

        var since = DateTime.Now.AddDays(-Math.Abs(days));
        var filter = BuildReceivedTimeFilter(since);
        try
        {
            var restricted = items.Restrict(filter);
            isSeparateComObject = true;
            return restricted;
        }
        catch (COMException)
        {
            return items;
        }
    }

    private static string BuildReceivedTimeFilter(DateTime since)
    {
        var formatted = since.ToString("g", CultureInfo.GetCultureInfo("en-US"));
        return $"[ReceivedTime] >= '{formatted}'";
    }

    private sealed class InFlightHeaderFetch : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private int _waiterCount;

        public InFlightHeaderFetch(Task<IReadOnlyList<RawEmailHeader>> task, CancellationTokenSource cts)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
        }

        public Task<IReadOnlyList<RawEmailHeader>> Task { get; }

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

        public void Dispose() => _cts.Dispose();
    }

    private sealed record InboxHeaderFetchResult(
        List<RawEmailHeader> Headers,
        int SkippedItemCount,
        int MalformedItemCount);
}
