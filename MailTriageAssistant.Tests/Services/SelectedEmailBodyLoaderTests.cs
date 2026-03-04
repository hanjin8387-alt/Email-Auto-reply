using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class SelectedEmailBodyLoaderTests
{
    [Fact]
    public async Task EnsureBodiesLoadedAsync_ShouldRespectCancellation()
    {
        var gateway = new Mock<IOutlookMailGateway>(MockBehavior.Strict);
        gateway
            .Setup(g => g.GetRawEmailContentsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                OutlookOperationPriority.Background,
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<string>, OutlookOperationPriority, CancellationToken>(async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new Dictionary<string, RawEmailContent>();
            });

        var triage = new Mock<ITriageService>(MockBehavior.Strict);
        var redaction = new Mock<IRedactionService>(MockBehavior.Strict);

        var loader = new SelectedEmailBodyLoader(
            gateway.Object,
            triage.Object,
            redaction.Object,
            NullLogger<SelectedEmailBodyLoader>.Instance);

        var item = new AnalyzedItem { EntryId = "A" };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Func<Task> action = async () => await loader.EnsureBodiesLoadedAsync(
            new[] { item },
            OutlookOperationPriority.Background,
            cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
