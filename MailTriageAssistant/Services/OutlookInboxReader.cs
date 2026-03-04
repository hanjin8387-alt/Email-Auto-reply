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
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly ILogger<OutlookInboxReader> _logger;
    private readonly object _cacheGate = new();
    private DateTimeOffset _headersCacheUtc;
    private List<RawEmailHeader>? _headersCache;

    public OutlookInboxReader(
        IOutlookSessionHost sessionHost,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookInboxReader> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
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

        return FetchInboxHeadersCoreAsync(ct);
    }

    public void InvalidateCache()
    {
        lock (_cacheGate)
        {
            _headersCache = null;
            _headersCacheUtc = default;
        }
    }

    private async Task<IReadOnlyList<RawEmailHeader>> FetchInboxHeadersCoreAsync(CancellationToken ct)
    {
        try
        {
            var headers = await _sessionHost.InvokeAsync(
                ctx => FetchInboxHeadersInternal(ctx, _optionsMonitor.CurrentValue),
                OutlookOperationPriority.UserInitiated,
                ct).ConfigureAwait(false);

            lock (_cacheGate)
            {
                _headersCacheUtc = DateTimeOffset.UtcNow;
                _headersCache = headers;
            }

            return new List<RawEmailHeader>(headers);
        }
        catch (COMException ex)
        {
            _logger.LogWarning("FetchInboxHeaders failed: {ExceptionType} (HResult={HResult}).", ex.GetType().Name, ex.HResult);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("Failed to communicate with Outlook. Verify Classic Outlook is running.");
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("FetchInboxHeaders failed: {ExceptionType}.", ex.GetType().Name);
            _sessionHost.ResetConnection();
            throw new InvalidOperationException("An error occurred while loading inbox headers.");
        }
    }

    private static List<RawEmailHeader> FetchInboxHeadersInternal(OutlookSessionContext context, OutlookOptions options)
    {
        Outlook.MAPIFolder? inbox = null;
        Outlook.Items? items = null;
        Outlook.Items? filteredItems = null;
        var filteredItemsIsSeparate = false;
        object? raw = null;

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
                }
                catch
                {
                    // Skip malformed item and continue.
                }
                finally
                {
                    OutlookSessionHost.SafeReleaseComObject(current);
                }

                raw = filteredItems.GetNext();
            }

            result.Sort((a, b) => b.ReceivedTime.CompareTo(a.ReceivedTime));
            return result;
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
}
