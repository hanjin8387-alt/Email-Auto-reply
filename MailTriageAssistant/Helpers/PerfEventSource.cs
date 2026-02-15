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
    public void MeasureStart(long id, string name)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(1, id, name ?? string.Empty);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void MeasureStop(long id, long elapsedMs)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(2, id, elapsedMs);
    }

    [Event(3, Level = EventLevel.Informational)]
    public void Measure(string name, long elapsedMs)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(3, name ?? string.Empty, elapsedMs);
    }
}
