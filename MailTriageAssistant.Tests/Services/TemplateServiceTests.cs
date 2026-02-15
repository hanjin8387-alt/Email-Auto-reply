using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MailTriageAssistant.Services;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class TemplateServiceTests
{
    private readonly TemplateService _sut = new();

    [Fact]
    public void FillTemplate_SinglePlaceholder_Replaced()
    {
        var body = "안녕하세요, {TargetDate}까지";
        var values = new Dictionary<string, string> { ["TargetDate"] = "2026-02-20" };

        var result = _sut.FillTemplate(body, values);

        result.Should().Be("안녕하세요, 2026-02-20까지");
    }

    [Fact]
    public void FillTemplate_MultiplePlaceholders_AllReplaced()
    {
        var body = "- {Date1}\n- {Date2}";
        var values = new Dictionary<string, string>
        {
            ["Date1"] = "옵션1",
            ["Date2"] = "옵션2",
        };

        var result = _sut.FillTemplate(body, values);

        result.Should().Be("- 옵션1\n- 옵션2");
    }

    [Fact]
    public void FillTemplate_MissingValue_ReplacedWithUnderscores()
    {
        var body = "{MissingInfo} 확인";

        var result = _sut.FillTemplate(body, new Dictionary<string, string>());

        result.Should().Be("___ 확인");
    }

    [Fact]
    public void FillTemplate_EmptyTemplate_ReturnsEmpty()
    {
        var result = _sut.FillTemplate(string.Empty, new Dictionary<string, string> { ["Key"] = "v" });

        result.Should().BeEmpty();
    }

    [Fact]
    public void FillTemplate_NullTemplate_ReturnsEmpty()
    {
        var result = _sut.FillTemplate(null!, new Dictionary<string, string> { ["Key"] = "v" });

        result.Should().BeEmpty();
    }

    [Fact]
    public void FillTemplate_NoPlaceholders_ReturnsOriginal()
    {
        var body = "플레이스홀더가 없습니다";

        var result = _sut.FillTemplate(body, new Dictionary<string, string>());

        result.Should().Be(body);
    }

    [Fact]
    public void FillTemplate_WhitespaceValue_ReplacedWithUnderscores()
    {
        var result = _sut.FillTemplate("{Key}", new Dictionary<string, string> { ["Key"] = "  " });

        result.Should().Be("___");
    }

    [Fact]
    public void GetTemplates_Returns8Templates()
    {
        var templates = _sut.GetTemplates();

        templates.Should().HaveCount(8);
    }

    [Fact]
    public void GetTemplates_ReturnsDeepCopies()
    {
        var templates1 = _sut.GetTemplates();
        templates1[0].Title = "CHANGED";

        var templates2 = _sut.GetTemplates();
        templates2[0].Title.Should().NotBe("CHANGED");
    }

    [Fact]
    public async Task SendDraft_ValidTemplate_CallsOutlookCreateDraft()
    {
        var mock = new Mock<IOutlookService>(MockBehavior.Strict);
        mock.Setup(o => o.CreateDraft(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _sut.SendDraft(
            mock.Object,
            recipientEmail: "user@test.com",
            subject: "SUBJ",
            templateId: "TMP_01",
            values: new Dictionary<string, string> { ["TargetDate"] = "2026-02-20" });

        mock.Verify(o => o.CreateDraft(
            "user@test.com",
            "SUBJ",
            It.Is<string>(b => b.Contains("2026-02-20", StringComparison.Ordinal))),
            Times.Once);
    }

    [Fact]
    public async Task SendDraft_InvalidTemplateId_ThrowsInvalidOperation()
    {
        var mock = new Mock<IOutlookService>();

        var act = () => _sut.SendDraft(
            mock.Object,
            recipientEmail: "user@test.com",
            subject: "SUBJ",
            templateId: "INVALID",
            values: new Dictionary<string, string>());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendDraft_NullOutlookService_ThrowsArgNull()
    {
        var act = () => _sut.SendDraft(
            null!,
            recipientEmail: "user@test.com",
            subject: "SUBJ",
            templateId: "TMP_01",
            values: new Dictionary<string, string>());

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

