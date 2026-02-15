using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class DigestServiceTests
{
    private readonly RedactionService _redaction = new();

    private DigestService CreateSut()
    {
        // NOTE: We never call SecureCopy/OpenTeams in these tests to avoid clipboard dependencies.
        var clipboard = new ClipboardSecurityHelper(_redaction);
        return new DigestService(clipboard, _redaction);
    }

    private static AnalyzedItem Item(
        int score,
        string sender = "Sender",
        string senderEmail = "",
        string subject = "Subject",
        string summary = "Summary")
        => new()
        {
            EntryId = Guid.NewGuid().ToString("N"),
            Sender = sender,
            SenderEmail = senderEmail,
            Subject = subject,
            ReceivedTime = DateTime.Now,
            HasAttachments = false,
            Score = score,
            RedactedSummary = summary,
        };

    [Fact]
    public void GenerateDigest_EmptyList_ContainsHeaderOnly()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(Array.Empty<AnalyzedItem>());

        digest.Should().Contain("| Priority | Sender | Subject | Summary (Redacted) |");

        var dataLines = digest
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("| ", StringComparison.Ordinal))
            .Where(l => !l.StartsWith("| Priority |", StringComparison.Ordinal))
            .Where(l => !l.StartsWith("|---|", StringComparison.Ordinal))
            .ToList();

        dataLines.Should().BeEmpty();
    }

    [Fact]
    public void GenerateDigest_SingleItem_ContainsOneRow()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(80, sender: "A", subject: "S1", summary: "Hi"),
        });

        digest.Should().Contain("| 80 높음 | A | S1 | Hi |");
    }

    [Fact]
    public void GenerateDigest_MultipleItems_OrderedByScoreDesc()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(30, subject: "S30"),
            Item(90, subject: "S90"),
            Item(50, subject: "S50"),
        });

        var i90 = digest.IndexOf("| 90 ", StringComparison.Ordinal);
        var i50 = digest.IndexOf("| 50 ", StringComparison.Ordinal);
        var i30 = digest.IndexOf("| 30 ", StringComparison.Ordinal);

        i90.Should().BeGreaterThanOrEqualTo(0);
        i50.Should().BeGreaterThanOrEqualTo(0);
        i30.Should().BeGreaterThanOrEqualTo(0);

        i90.Should().BeLessThan(i50);
        i50.Should().BeLessThan(i30);
    }

    [Fact]
    public void GenerateDigest_ContainsSystemPrompt()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem> { Item(50) });

        digest.Should().Contain("SYSTEM PROMPT");
    }

    [Fact]
    public void GenerateDigest_ContainsTaskListFooter()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem> { Item(50) });

        digest.Should().Contain("Tasks:")
            .And.Contain("Identify the top 3 critical items requiring immediate action.");
    }

    [Fact]
    public void GenerateDigest_RedactionApplied()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(50, sender: "A", senderEmail: "user@test.com", subject: "S"),
        });

        digest.Should().Contain("\\[EMAIL\\]");
    }

    [Fact]
    public void GenerateDigest_PipeInText_Escaped()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(50, subject: "A|B"),
        });

        digest.Should().Contain("A\\|B");
    }

    [Fact]
    public void GenerateDigest_MarkdownSpecialChars_Escaped()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(50, subject: "[x](y)!<z>"),
        });

        digest.Should().Contain("\\[x\\]\\(y\\)\\!\\<z\\>");
    }

    [Fact]
    public void GenerateDigest_PriorityLabel_High()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem> { Item(80) });

        digest.Should().Contain("| 80 높음 |");
    }

    [Fact]
    public void GenerateDigest_PriorityLabel_Medium()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem> { Item(50) });

        digest.Should().Contain("| 50 중간 |");
    }

    [Fact]
    public void GenerateDigest_PriorityLabel_Low()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem> { Item(29) });

        digest.Should().Contain("| 29 낮음 |");
    }

    [Fact]
    public void GenerateDigest_NewlineReplaced()
    {
        var sut = CreateSut();

        var digest = sut.GenerateDigest(new List<AnalyzedItem>
        {
            Item(50, subject: "line1\nline2"),
        });

        digest.Should().Contain("line1 line2");
        digest.Should().NotContain("line1\nline2");
    }
}
