using System.Collections.Generic;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Security;

public sealed class RedactionSecurityTests
{
    [Fact]
    public void Redact_FullwidthDigits_CannotBypassPhoneMasking()
    {
        var sut = new RedactionService();

        sut.Redact("０１０-１２３４-５６７８").Should().Be("[PHONE]");
    }

    [Fact]
    public void Redact_AccountNumber_IsMasked()
    {
        var sut = new RedactionService();

        sut.Redact("계좌번호: 123-45-678901").Should().Be("계좌번호: [ACCOUNT]");
    }

    [Fact]
    public void Redact_PassportNumber_IsMasked()
    {
        var sut = new RedactionService();

        sut.Redact("여권번호: M12345678").Should().Be("여권번호: [PASSPORT]");
    }

    [Fact]
    public void Redact_IpAddress_IsMasked()
    {
        var sut = new RedactionService();

        sut.Redact("접속: 192.168.0.10").Should().Be("접속: [IP]");
    }

    [Fact]
    public void Redact_UrlTokenValue_IsMasked()
    {
        var sut = new RedactionService();

        var redacted = sut.Redact("https://example.com/?access_token=abcd1234&x=1");

        redacted.Should().Contain("access_token=[URL_TOKEN]");
        redacted.Should().NotContain("abcd1234");
    }

    [Fact]
    public void Template_ValueWithBraces_CannotInjectNewPlaceholders()
    {
        var sut = new TemplateService();

        var result = sut.FillTemplate(
            "안녕하세요, {Key}",
            new Dictionary<string, string> { ["Key"] = "{TargetDate}" });

        result.Should().Be("안녕하세요, TargetDate");
        result.Should().NotContain("{").And.NotContain("}");
    }

    [Fact]
    public void Digest_EscapesMarkdownSpecialChars()
    {
        var redaction = new RedactionService();
        var clipboard = new ClipboardSecurityHelper(redaction);
        var sut = new DigestService(clipboard, redaction);

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            new()
            {
                EntryId = "x",
                Sender = "A",
                SenderEmail = "a@test.com",
                Subject = "[x](y)!<z>",
                ReceivedTime = System.DateTime.Now,
                Score = 50,
                RedactedSummary = "OK",
            }
        });

        digest.Should().Contain("\\[x\\]\\(y\\)\\!\\<z\\>");
    }
}

