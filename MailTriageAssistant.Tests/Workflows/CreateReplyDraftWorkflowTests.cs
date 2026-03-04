using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.Workflows;

public sealed class CreateReplyDraftWorkflowTests
{
    [Fact]
    public void Validate_ShouldFailWhenRequiredFieldMissing()
    {
        var templateService = new Mock<ITemplateService>();
        templateService
            .Setup(s => s.ExtractPlaceholders(It.IsAny<string>()))
            .Returns(new[] { "MissingInfo" });
        templateService
            .Setup(s => s.FillTemplate(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(string.Empty);

        var workflow = new CreateReplyDraftWorkflow(templateService.Object, Mock.Of<IOutlookMailGateway>());
        var email = new AnalyzedItem { EntryId = "A" };
        email.UpdateRawContent(new RawEmailContent("Sender", "sender@company.com", "subject", ""));
        email.UpdateRedactedContent(new RedactedEmailContent("Sender", "subject", "summary"));

        var template = new ReplyTemplate
        {
            Id = "T1",
            Title = "Template",
            BodyContent = "{MissingInfo}",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "MissingInfo", Label = "Missing Info", IsRequired = true },
            },
        };

        var result = workflow.Validate(email, template, new List<ReplyTemplateFieldInput>());

        result.IsValid.Should().BeFalse();
        result.MissingRequiredFields.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCallOutlookGateway()
    {
        var templateService = new Mock<ITemplateService>();
        templateService
            .Setup(s => s.ExtractPlaceholders(It.IsAny<string>()))
            .Returns(new[] { "TaskName" });
        templateService
            .Setup(s => s.FillTemplate(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns("filled body");

        var gateway = new Mock<IOutlookMailGateway>();
        gateway
            .Setup(g => g.CreateDraftAsync(It.IsAny<ReplyDraftRequest>(), default))
            .Returns(Task.CompletedTask);

        var workflow = new CreateReplyDraftWorkflow(templateService.Object, gateway.Object);
        var email = new AnalyzedItem { EntryId = "A" };
        email.UpdateRawContent(new RawEmailContent("Sender", "sender@company.com", "Raw Subject", ""));
        email.UpdateRedactedContent(new RedactedEmailContent("Sender", "Redacted Subject", "summary"));

        var template = new ReplyTemplate
        {
            Id = "T2",
            Title = "Template",
            BodyContent = "{TaskName}",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "TaskName", Label = "Task", IsRequired = true },
            },
        };

        await workflow.CreateDraftAsync(
            email,
            template,
            new[]
            {
                new ReplyTemplateFieldInput("TaskName", "Task", true, value: "Review"),
            });

        gateway.Verify(g => g.CreateDraftAsync(
            It.Is<ReplyDraftRequest>(r => r.To == "sender@company.com" && r.Subject.StartsWith("RE:")),
            default), Times.Once);
    }
}
