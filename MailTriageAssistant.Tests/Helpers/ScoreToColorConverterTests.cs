using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using MailTriageAssistant.Helpers;
using Xunit;

namespace MailTriageAssistant.Tests.Helpers;

public sealed class ScoreToColorConverterTests
{
    private readonly ScoreToColorConverter _sut = new();

    [Theory]
    [InlineData(80, "DarkRed")]
    [InlineData(100, "DarkRed")]
    [InlineData(79, "DarkGoldenrod")]
    [InlineData(50, "DarkGoldenrod")]
    [InlineData(49, "DarkGreen")]
    [InlineData(30, "DarkGreen")]
    [InlineData(29, "DimGray")]
    [InlineData(0, "DimGray")]
    public void Convert_IntScore_ReturnsCorrectBrush(int score, string expectedBrushName)
    {
        var result = _sut.Convert(score, typeof(Brush), null, CultureInfo.InvariantCulture);
        var expected = typeof(Brushes).GetProperty(expectedBrushName)!.GetValue(null);

        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_StringInput_Parsed()
    {
        var result = _sut.Convert("75", typeof(Brush), null, CultureInfo.InvariantCulture);

        result.Should().Be(Brushes.DarkGoldenrod);
    }

    [Fact]
    public void Convert_NullInput_ReturnsDimGray()
    {
        var result = _sut.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture);

        result.Should().Be(Brushes.DimGray);
    }
}
