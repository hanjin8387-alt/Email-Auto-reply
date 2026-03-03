using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class TriageServiceTests
{
    private readonly TriageService _sut = new(null!, NullLogger<TriageService>.Instance);

    [Fact]
    public void AnalyzeHeader_VipSender_ReturnsVip()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "蹂닿퀬");

        result.Category.Should().Be(EmailCategory.VIP);
        result.Score.Should().Be(80);
    }

    [Fact]
    public void AnalyzeHeader_ActionKeyword_ReturnsAction()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "ASAP ?붿껌");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(70);
    }

    [Fact]
    public void AnalyzeHeader_ApprovalKeyword_ReturnsApproval()
    {
        var settings = new TriageSettings
        {
            ApprovalKeywords = new[] { "approve" },
        };

        var options = new Mock<IOptionsMonitor<TriageSettings>>(MockBehavior.Strict);
        options.Setup(o => o.CurrentValue).Returns(settings);
        options.Setup(o => o.OnChange(It.IsAny<Action<TriageSettings, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var sut = new TriageService(options.Object, NullLogger<TriageService>.Instance);
        var result = sut.AnalyzeHeader("user@test.com", "please approve");

        result.Category.Should().Be(EmailCategory.Approval);
        result.Score.Should().Be(65);
    }

    [Fact]
    public void AnalyzeHeader_MeetingKeyword_ReturnsMeeting()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "Teams invite");

        result.Category.Should().Be(EmailCategory.Meeting);
        result.Score.Should().Be(60);
    }

    [Fact]
    public void AnalyzeHeader_NewsletterKeyword_ReturnsNewsletter()
    {
        var result = _sut.AnalyzeHeader("noreply@news.com", "Unsubscribe ?덈궡");

        result.Category.Should().Be(EmailCategory.Newsletter);
        result.Score.Should().Be(0);
    }

    [Fact]
    public void AnalyzeHeader_FyiKeyword_ReturnsFYI()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "FYI 李멸퀬");

        result.Category.Should().Be(EmailCategory.FYI);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeHeader_NoKeyword_ReturnsOther()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "?쇰컲 ?댁슜");

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeHeader_VipWithAction_ScoreCapped100()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "ASAP ?뺤씤 ?붿껌");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(100);
    }

    [Fact]
    public void AnalyzeHeader_UnknownSender_PenaltyApplied()
    {
        var result = _sut.AnalyzeHeader("unknown", "test");

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(40);
    }

    [Fact]
    public void AnalyzeHeader_ExchangeSender_DoesNotApplyUnknownPenalty()
    {
        var result = _sut.AnalyzeHeader("/O=ORG/OU=ADMIN/CN=RECIPIENTS/CN=user", "?쇰컲 ?덈궡");

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeHeader_NullSender_NoVip()
    {
        var result = _sut.AnalyzeHeader(null!, "ASAP ?뺤씤");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(60);
    }

    [Fact]
    public void AnalyzeHeader_EmptySubject_ScoreIs50()
    {
        var result = _sut.AnalyzeHeader("user@test.com", string.Empty);

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeWithBody_ActionInBody_DetectedAsAction()
    {
        var result = _sut.AnalyzeWithBody("user@test.com", "subject", "ASAP ?붿껌");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(70);
    }

    [Fact]
    public void AnalyzeHeader_ActionPriorityOverVip()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "ASAP ?뺤씤 ?붿껌");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(100);
    }

    [Fact]
    public void AnalyzeHeader_NewsletterDeductionClamps0()
    {
        var result = _sut.AnalyzeHeader("no-reply@ad.com", "News");

        result.Category.Should().Be(EmailCategory.Newsletter);
        result.Score.Should().Be(0);
    }

    [Fact]
    public void AnalyzeHeader_TagsContainAllMatched()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "ASAP ?붿껌");

        result.Tags.Should().Contain("VIP").And.Contain("Action");
    }

    [Fact]
    public void AnalyzeHeader_ActionHint_MatchesCategory()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "?쇰컲");

        result.Category.Should().Be(EmailCategory.Other);
        result.ActionHint.Should().NotBeNullOrWhiteSpace();
    }
}


