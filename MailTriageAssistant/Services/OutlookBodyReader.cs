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
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly ILogger<OutlookBodyReader> _logger;
    private readonly ConcurrentDictionary<string, Task<RawEmailContent>> _inFlightByEntryId = new(StringComparer.Ordinal);

    public OutlookBodyReader(
        IOutlookSessionHost sessionHost,
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookBodyReader> logger)
    {
        _sessionHost = sessionHost ?? throw new ArgumentNullException(nameof(sessionHost));
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

        Task<RawEmailContent> CreateLoader(string key)
            => LoadRawEmailContentCoreAsync(key, priority);

        var task = _inFlightByEntryId.GetOrAdd(entryId, CreateLoader);
        try
        {
            return await task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            if (task.IsCompleted)
            {
                _inFlightByEntryId.TryRemove(new KeyValuePair<string, Task<RawEmailContent>>(entryId, task));
            }
        }
    }

    public async Task<IReadOnlyDictionary<string, RawEmailContent>> GetRawEmailContentsAsync(
        IReadOnlyList<string> entryIds,
        OutlookOperationPriority priority = OutlookOperationPriority.UserInitiated,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entryIds);

        var result = new Dictionary<string, RawEmailContent>(StringComparer.Ordinal);
        var distinct = entryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var entryId in distinct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                result[entryId] = await GetRawEmailContentAsync(entryId, priority, ct).ConfigureAwait(false);
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
                _logger.LogDebug("GetRawEmailContents skipped item due to {ExceptionType}.", ex.GetType().Name);
            }
        }

        return result;
    }

    private async Task<RawEmailContent> LoadRawEmailContentCoreAsync(string entryId, OutlookOperationPriority priority)
    {
        try
        {
            return await _sessionHost.InvokeAsync(
                ctx => GetRawEmailContentInternal(ctx, entryId, Math.Max(200, _optionsMonitor.CurrentValue.MaxBodyLength)),
                priority,
                CancellationToken.None).ConfigureAwait(false);
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
}
