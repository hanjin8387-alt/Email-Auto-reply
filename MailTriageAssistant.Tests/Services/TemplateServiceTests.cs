using System.Collections.Generic;
using FluentAssertions;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class TemplateServiceTests
{
    private readonly TemplateService _sut = new(NullLogger<TemplateService>.Instance);

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
    public void FillTemplate_MissingValue_ReplacedWithMissingMarker()
    {
        var body = "{MissingInfo} 확인";

        var result = _sut.FillTemplate(body, new Dictionary<string, string>());

        result.Should().Be("[미입력] 확인");
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
    public void FillTemplate_WhitespaceValue_ReplacedWithMissingMarker()
    {
        var result = _sut.FillTemplate("{Key}", new Dictionary<string, string> { ["Key"] = "  " });

        result.Should().Be("[미입력]");
    }

    [Fact]
    public void FillTemplate_ValueWithBraces_BracesRemoved()
    {
        var result = _sut.FillTemplate("{Key}", new Dictionary<string, string> { ["Key"] = "{Injected}" });

        result.Should().Be("Injected");
    }

    [Fact]
    public void FillTemplate_ValueLongerThanLimit_IsTruncated()
    {
        var longValue = new string('a', 250);
        var result = _sut.FillTemplate("{Key}", new Dictionary<string, string> { ["Key"] = longValue });

        result.Should().HaveLength(200);
    }

    [Fact]
    public void FillTemplate_UnderscoreValue_ReplacedWithMissingMarker()
    {
        var result = _sut.FillTemplate("{Key}", new Dictionary<string, string> { ["Key"] = "___" });

        result.Should().Be("[미입력]");
    }

    [Fact]
    public void GetTemplates_Returns8Templates()
    {
        var templates = _sut.GetTemplates();

        templates.Should().HaveCount(8);
    }

    [Fact]
    public void GetTemplates_ReturnsListCopyWithSharedItems()
    {
        var templates1 = _sut.GetTemplates();
        var templates2 = _sut.GetTemplates();

        templates1.Should().NotBeSameAs(templates2);
        templates1[0].Should().BeSameAs(templates2[0]);
        templates1[0].Title.Should().Be(templates2[0].Title);
    }
}
