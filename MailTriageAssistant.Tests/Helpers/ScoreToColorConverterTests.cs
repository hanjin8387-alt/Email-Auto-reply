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
    [InlineData(80, "IndianRed")]
    [InlineData(100, "IndianRed")]
    [InlineData(79, "DarkOrange")]
    [InlineData(50, "DarkOrange")]
    [InlineData(49, "SeaGreen")]
    [InlineData(30, "SeaGreen")]
    [InlineData(29, "Gray")]
    [InlineData(0, "Gray")]
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

        result.Should().Be(Brushes.DarkOrange);
    }

    [Fact]
    public void Convert_NullInput_ReturnsGray()
    {
        var result = _sut.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture);

        result.Should().Be(Brushes.Gray);
    }
}

