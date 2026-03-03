using FluentAssertions;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class RedactionServiceTests
{
    private readonly RedactionService _sut = new(NullLogger<RedactionService>.Instance);

    [Theory]
    [InlineData("010-1234-5678", "[PHONE]")]
    [InlineData("900101-1234567", "[SSN]")]
    [InlineData("9001011234567", "[SSN]")]
    [InlineData("1234-5678-9012-3456", "[CARD]")]
    [InlineData("1234 5678 9012 3456", "[CARD]")]
    [InlineData("4111111111111111", "[CARD]")]
    [InlineData("user@example.com", "[EMAIL]")]
    [InlineData("192.168.0.10", "[IP]")]
    [InlineData("access_token=abcd1234", "access_token=[TOKEN]")]
    [InlineData("{\"access_token\":\"abcd1234\"}", "{\"access_token\":\"[TOKEN]\"}")]
    [InlineData("Authorization: Bearer abcdef", "Authorization: Bearer [TOKEN]")]
    [InlineData("Bearer abcdef", "Bearer [TOKEN]")]
    [InlineData("x-api-key: secret123", "x-api-key=[TOKEN]")]
    [InlineData("account: 123-45-678901", "account: [ACCOUNT]")]
    [InlineData("passport: M12345678", "passport: [PASSPORT]")]
    [InlineData("+82 10-1234-5678", "[PHONE]")]
    public void Redact_SinglePattern_IsReplaced(string input, string expected)
    {
        _sut.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_MultiplePatterns_AllReplaced()
    {
        var input = "phone: 010-1234-5678, ssn: 900101-1234567, email: a@b.com";

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
        _sut.Redact("normal text").Should().Be("normal text");
    }

    [Fact]
    public void Redact_PhoneInMiddleOfText_IsReplaced()
    {
        _sut.Redact("phone is 010-9876-5432")
            .Should().Be("phone is [PHONE]");
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
    public void Redact_LandlinePhoneFormat_IsReplaced()
    {
        _sut.Redact("02-1234-5678").Should().Be("[PHONE]");
    }
}
