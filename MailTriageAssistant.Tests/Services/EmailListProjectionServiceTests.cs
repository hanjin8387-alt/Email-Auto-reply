using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class EmailListProjectionServiceTests
{
    [Fact]
    public void RestoreSelection_ShouldReturnMatchingItem()
    {
        var a = new AnalyzedItem { EntryId = "A" };
        var b = new AnalyzedItem { EntryId = "B" };

        var restored = EmailListProjectionService.RestoreSelection(new[] { a, b }, "B");

        restored.Should().BeSameAs(b);
    }
}
