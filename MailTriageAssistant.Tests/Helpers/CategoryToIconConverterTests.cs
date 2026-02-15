using System.Globalization;
using System.Windows.Data;
using FluentAssertions;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Xunit;

namespace MailTriageAssistant.Tests.Helpers;

public sealed class CategoryToIconConverterTests
{
    private readonly CategoryToIconConverter _sut = new();

    [Theory]
    [InlineData(EmailCategory.Action, "!")]
    [InlineData(EmailCategory.Approval, "✓")]
    [InlineData(EmailCategory.VIP, "★")]
    [InlineData(EmailCategory.Meeting, "⌚")]
    [InlineData(EmailCategory.Newsletter, "✉")]
    [InlineData(EmailCategory.FYI, "i")]
    [InlineData(EmailCategory.Other, "•")]
    public void Convert_Category_ReturnsExpectedIcon(EmailCategory category, string expected)
    {
        var result = _sut.Convert(category, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_StringCategory_ParsedCaseInsensitive()
    {
        var result = _sut.Convert("vIp", typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("★");
    }

    [Fact]
    public void Convert_Null_ReturnsDefault()
    {
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("•");
    }

    [Fact]
    public void Convert_UnknownString_ReturnsDefault()
    {
        var result = _sut.Convert("unknown", typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("•");
    }

    [Fact]
    public void ConvertBack_ReturnsDoNothing()
    {
        var result = _sut.ConvertBack("!", typeof(EmailCategory), null, CultureInfo.InvariantCulture);

        result.Should().BeSameAs(Binding.DoNothing);
    }
}

