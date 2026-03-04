using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class AutoRefreshControllerTests
{
    [Fact]
    public async Task TriggerTickForTestAsync_ShouldPauseAfterConfiguredFailures()
    {
        var settings = new TriageSettings
        {
            AutoRefreshIntervalMinutes = 1,
            AutoRefreshFailurePauseThreshold = 2,
        };

        var monitor = new FakeOptionsMonitor<TriageSettings>(settings);
        var dialog = new Mock<IDialogService>();
        var clock = new FakeClock();
        var status = string.Empty;

        var controller = new AutoRefreshController(
            dispatcher: Dispatcher.CurrentDispatcher,
            clock: clock,
            settingsMonitor: monitor,
            refreshOperation: _ => Task.FromResult(InboxRefreshOutcome.Failure),
            isLoading: static () => false,
            setStatusMessage: message => status = message,
            dialogService: dialog.Object,
            logger: NullLogger<AutoRefreshController>.Instance);

        await controller.TriggerTickForTestAsync();
        await controller.TriggerTickForTestAsync();

        controller.IsPaused.Should().BeTrue();
        status.Should().NotBeNullOrWhiteSpace();
        dialog.Verify(d => d.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        controller.Dispose();
    }

    [Fact]
    public async Task NotifyManualRunSucceeded_ShouldResumeWhenPaused()
    {
        var settings = new TriageSettings
        {
            AutoRefreshIntervalMinutes = 1,
            AutoRefreshFailurePauseThreshold = 1,
        };

        var controller = new AutoRefreshController(
            dispatcher: Dispatcher.CurrentDispatcher,
            clock: new FakeClock(),
            settingsMonitor: new FakeOptionsMonitor<TriageSettings>(settings),
            refreshOperation: _ => Task.FromResult(InboxRefreshOutcome.Failure),
            isLoading: static () => false,
            setStatusMessage: _ => { },
            dialogService: Mock.Of<IDialogService>(),
            logger: NullLogger<AutoRefreshController>.Instance);

        await controller.TriggerTickForTestAsync();
        controller.IsPaused.Should().BeTrue();

        controller.NotifyManualRunSucceeded();

        controller.IsPaused.Should().BeFalse();
        controller.NextRunAt.Should().NotBeNull();

        controller.Dispose();
    }
}
