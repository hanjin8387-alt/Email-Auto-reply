using System.Diagnostics;
using System.Linq;
using Xunit;

namespace MailTriageAssistant.Tests.Interop;

public sealed class OutlookInteropFactAttribute : FactAttribute
{
    public OutlookInteropFactAttribute()
    {
        if (!Process.GetProcessesByName("outlook").Any())
        {
            Skip = "Classic Outlook process not found. Skipping COM smoke test.";
        }
    }
}
