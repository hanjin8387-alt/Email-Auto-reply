using FluentAssertions;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class RedactionServiceTests
{
    private readonly RedactionService _sut = new();

    [Theory]
    [InlineData("010-1234-5678", "[PHONE]")]
    [InlineData("０１０-１２３４-５６７８", "[PHONE]")]
    [InlineData("900101-1234567", "[SSN]")]
    [InlineData("9001011234567", "[SSN]")]
    [InlineData("1234-5678-9012-3456", "[CARD]")]
    [InlineData("1234 5678 9012 3456", "[CARD]")]
    [InlineData("user@example.com", "[EMAIL]")]
    [InlineData("192.168.0.10", "[IP]")]
    [InlineData("access_token=abcd1234", "access_token=[URL_TOKEN]")]
    [InlineData("계좌: 123-45-678901", "계좌: [ACCOUNT]")]
    [InlineData("여권번호: M12345678", "여권번호: [PASSPORT]")]
    public void Redact_SinglePattern_IsReplaced(string input, string expected)
    {
        _sut.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_MultiplePatterns_AllReplaced()
    {
        var input = "전화: 010-1234-5678, 주민: 900101-1234567, 메일: a@b.com";

        var result = _sut.Redact(input);

        result.Should().Contain("[PHONE]")
            .And.Contain("[SSN]")
            .And.Contain("[EMAIL]");
    }

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        _sut.Redact(null!).Should().BeNull();
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmpty()
    {
        _sut.Redact(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Redact_NoSensitiveData_ReturnsOriginal()
    {
        _sut.Redact("일반 텍스트입니다").Should().Be("일반 텍스트입니다");
    }

    [Fact]
    public void Redact_PhoneInMiddleOfText_IsReplaced()
    {
        _sut.Redact("전화번호는010-9876-5432 입니다")
            .Should().Be("전화번호는[PHONE] 입니다");
    }

    [Fact]
    public void Redact_MultiplePhones_AllReplaced()
    {
        _sut.Redact("010-1111-2222, 010-3333-4444")
            .Should().Be("[PHONE], [PHONE]");
    }

    [Fact]
    public void Redact_CardBeforeSSN_OrderMatters()
    {
        _sut.Redact("1234-5678-9012-3456 vs 900101-1234567")
            .Should().Be("[CARD] vs [SSN]");
    }

    [Fact]
    public void Redact_NonMatchingPhoneFormat_NotReplaced()
    {
        _sut.Redact("02-1234-5678").Should().Be("02-1234-5678");
    }
}
