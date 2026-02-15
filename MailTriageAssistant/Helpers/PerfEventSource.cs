using System.Diagnostics.Tracing;

namespace MailTriageAssistant.Helpers;

[EventSource(Name = "MailTriageAssistant-Perf")]
internal sealed class PerfEventSource : EventSource
{
    public static readonly PerfEventSource Log = new();

    private PerfEventSource()
    {
    }

    [Event(1, Level = EventLevel.Informational)]
    public void Measure(string name, long elapsedMs)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(1, name ?? string.Empty, elapsedMs);
    }
}

