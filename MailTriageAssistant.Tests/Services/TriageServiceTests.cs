using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class TriageServiceTests
{
    private readonly TriageService _sut = new();

    [Fact]
    public void AnalyzeHeader_VipSender_ReturnsVip()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "보고");

        result.Category.Should().Be(EmailCategory.VIP);
        result.Score.Should().Be(80);
    }

    [Fact]
    public void AnalyzeHeader_ActionKeyword_ReturnsAction()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "긴급 요청");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(70);
    }

    [Fact]
    public void AnalyzeHeader_ApprovalKeyword_ReturnsApproval()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "결재 건");

        result.Category.Should().Be(EmailCategory.Approval);
        result.Score.Should().Be(65);
    }

    [Fact]
    public void AnalyzeHeader_MeetingKeyword_ReturnsMeeting()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "Teams 회의 초대");

        result.Category.Should().Be(EmailCategory.Meeting);
        result.Score.Should().Be(60);
    }

    [Fact]
    public void AnalyzeHeader_NewsletterKeyword_ReturnsNewsletter()
    {
        var result = _sut.AnalyzeHeader("noreply@news.com", "Unsubscribe 안내");

        result.Category.Should().Be(EmailCategory.Newsletter);
        result.Score.Should().Be(0);
    }

    [Fact]
    public void AnalyzeHeader_FyiKeyword_ReturnsFYI()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "FYI 참고");

        result.Category.Should().Be(EmailCategory.FYI);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeHeader_NoKeyword_ReturnsOther()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "일반 내용");

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(50);
    }

    [Fact]
    public void AnalyzeHeader_VipWithAction_ScoreCapped100()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "긴급 요청 확인");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(100);
    }

    [Fact]
    public void AnalyzeHeader_UnknownSender_PenaltyApplied()
    {
        var result = _sut.AnalyzeHeader("unknown", "테스트");

        result.Category.Should().Be(EmailCategory.Other);
        result.Score.Should().Be(40);
    }

    [Fact]
    public void AnalyzeHeader_NullSender_NoVip()
    {
        var result = _sut.AnalyzeHeader(null!, "확인 요청");

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
        var result = _sut.AnalyzeWithBody("user@test.com", "제목", "긴급 요청");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(70);
    }

    [Fact]
    public void AnalyzeHeader_ActionPriorityOverVip()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "긴급 확인 요청");

        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(100);
    }

    [Fact]
    public void AnalyzeHeader_NewsletterDeductionClamps0()
    {
        var result = _sut.AnalyzeHeader("no-reply@ad.com", "광고 구독");

        result.Category.Should().Be(EmailCategory.Newsletter);
        result.Score.Should().Be(0);
    }

    [Fact]
    public void AnalyzeHeader_TagsContainAllMatched()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "긴급 요청");

        result.Tags.Should().Contain("VIP").And.Contain("Action");
    }

    [Fact]
    public void AnalyzeHeader_ActionHint_MatchesCategory()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "일반");

        result.Category.Should().Be(EmailCategory.Other);
        result.ActionHint.Should().Be("검토");
    }
}

