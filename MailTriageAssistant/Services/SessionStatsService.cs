using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class SessionStatsService
{
    private int _headersLoaded;
    private int _digestsGenerated;
    private int _digestsCopied;
    private int _teamsOpenAttempts;
    private int _errors;

    private readonly Dictionary<EmailCategory, int> _categoryCounts = new();

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    public void RecordHeadersLoaded(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _headersLoaded, count);
    }

    public void RecordTriage(EmailCategory category)
    {
        lock (_categoryCounts)
        {
            _categoryCounts.TryGetValue(category, out var current);
            _categoryCounts[category] = current + 1;
        }
    }

    public void RecordDigestGenerated()
        => Interlocked.Increment(ref _digestsGenerated);

    public void RecordDigestCopied()
        => Interlocked.Increment(ref _digestsCopied);

    public void RecordTeamsOpenAttempt()
        => Interlocked.Increment(ref _teamsOpenAttempts);

    public void RecordError()
        => Interlocked.Increment(ref _errors);

    public SessionStatsSnapshot Snapshot()
    {
        lock (_categoryCounts)
        {
            return new SessionStatsSnapshot(
                StartedAt,
                Volatile.Read(ref _headersLoaded),
                Volatile.Read(ref _digestsGenerated),
                Volatile.Read(ref _digestsCopied),
                Volatile.Read(ref _teamsOpenAttempts),
                Volatile.Read(ref _errors),
                new ReadOnlyDictionary<EmailCategory, int>(new Dictionary<EmailCategory, int>(_categoryCounts)));
        }
    }

    public sealed record SessionStatsSnapshot(
        DateTimeOffset StartedAt,
        int HeadersLoaded,
        int DigestsGenerated,
        int DigestsCopied,
        int TeamsOpenAttempts,
        int Errors,
        IReadOnlyDictionary<EmailCategory, int> CategoryCounts);
}

