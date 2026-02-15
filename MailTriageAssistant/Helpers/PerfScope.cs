using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MailTriageAssistant.Helpers;

internal readonly struct PerfScope : IDisposable
{
#if DEBUG
    private readonly string _name;
    private readonly long _startTimestamp;
    private readonly ILogger? _logger;

    private PerfScope(string name, long startTimestamp, ILogger? logger)
    {
        _name = name;
        _startTimestamp = startTimestamp;
        _logger = logger;
    }
#endif

    public static PerfScope Start(string name, ILogger? logger = null)
    {
#if DEBUG
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "unknown";
        }

        var start = Stopwatch.GetTimestamp();
        return new PerfScope(name, start, logger);
#else
        _ = name;
        _ = logger;
        return default;
#endif
    }

    public void Dispose()
    {
#if DEBUG
        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        var elapsedMs = (long)Math.Round(elapsed.TotalMilliseconds);

        PerfEventSource.Log.Measure(_name, elapsedMs);
        PerfMetrics.AddTiming(_name, elapsedMs);

        // Never log any email content; only scope name and elapsed time are emitted.
        _logger?.LogDebug("Perf: {Name} {ElapsedMs}ms.", _name, elapsedMs);
#endif
    }
}

internal static class PerfMetrics
{
#if DEBUG
    private static readonly object s_gate = new();
    private static readonly Dictionary<string, Timing> s_timings = new(StringComparer.Ordinal);

    private sealed class Timing
    {
        public int Count { get; set; }
        public long LastMs { get; set; }
        public long MinMs { get; set; } = long.MaxValue;
        public long MaxMs { get; set; }
        public long TotalMs { get; set; }
    }
#endif

    public static void AddTiming(string name, long elapsedMs)
    {
#if DEBUG
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (s_gate)
        {
            if (!s_timings.TryGetValue(name, out var t))
            {
                t = new Timing();
                s_timings[name] = t;
            }

            t.Count++;
            t.LastMs = elapsedMs;
            t.MinMs = Math.Min(t.MinMs, elapsedMs);
            t.MaxMs = Math.Max(t.MaxMs, elapsedMs);
            t.TotalMs += elapsedMs;
        }
#else
        _ = name;
        _ = elapsedMs;
#endif
    }

    public static Dictionary<string, object> Snapshot()
    {
#if DEBUG
        lock (s_gate)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (name, t) in s_timings)
            {
                var avg = t.Count <= 0 ? 0d : (double)t.TotalMs / t.Count;
                result[name] = new
                {
                    count = t.Count,
                    last_ms = t.LastMs,
                    min_ms = t.MinMs == long.MaxValue ? 0 : t.MinMs,
                    max_ms = t.MaxMs,
                    avg_ms = avg,
                };
            }

            return result;
        }
#else
        return new Dictionary<string, object>(StringComparer.Ordinal);
#endif
    }
}
