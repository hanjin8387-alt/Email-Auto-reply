using System.Globalization;
using FluentAssertions;
using MailTriageAssistant.Helpers;
using Xunit;

namespace MailTriageAssistant.Tests.Helpers;

public sealed class ScoreToLabelConverterTests
{
    private readonly ScoreToLabelConverter _sut = new();

    [Theory]
    [InlineData(100, "긴급")]
    [InlineData(80, "긴급")]
    [InlineData(79, "중요")]
    [InlineData(50, "중요")]
    [InlineData(49, "보통")]
    [InlineData(30, "보통")]
    [InlineData(29, "참고")]
    [InlineData(0, "참고")]
    public void Convert_IntScore_ReturnsLabel(int score, string expectedLabel)
    {
        var result = _sut.Convert(score, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(expectedLabel);
    }

    [Fact]
    public void Convert_StringInput_Parsed()
    {
        var result = _sut.Convert("75", typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("중요");
    }

    [Fact]
    public void Convert_NullInput_ReturnsDefault()
    {
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("참고");
    }
}

