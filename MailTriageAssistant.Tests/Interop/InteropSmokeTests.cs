using System.Threading.Tasks;
using FluentAssertions;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Tests.Interop;

public sealed class InteropSmokeTests
{
    [OutlookInteropFact]
    public async Task OutlookSessionHost_ShouldOpenSession_WhenClassicOutlookAvailable()
    {
        var optionsMonitor = new FakeOptionsMonitor<OutlookOptions>(new OutlookOptions());
        var detector = new OutlookCapabilityDetector(optionsMonitor, NullLogger<OutlookCapabilityDetector>.Instance);
        using var host = new OutlookSessionHost(detector, optionsMonitor, NullLogger<OutlookSessionHost>.Instance);

        var appName = await host.InvokeAsync(
            ctx => ctx.App.Name,
            OutlookOperationPriority.Interactive);

        appName.Should().NotBeNullOrWhiteSpace();
    }
}
