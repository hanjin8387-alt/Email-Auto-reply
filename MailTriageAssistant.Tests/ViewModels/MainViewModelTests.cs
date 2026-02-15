using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MailTriageAssistant.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task LoadEmailsAsync_PopulatesEmails_AndSetsStatus()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var headers = Enumerable.Range(1, 3)
                .Select(i => new RawEmailHeader
                {
                    EntryId = $"id-{i}",
                    SenderName = $"Sender{i}",
                    SenderEmail = $"sender{i}@example.com",
                    Subject = $"Subject{i}",
                    ReceivedTime = DateTime.Now.AddMinutes(-i),
                    HasAttachments = false,
                })
                .ToList();

            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            outlook.Setup(s => s.FetchInboxHeaders(It.IsAny<CancellationToken>()))
                .ReturnsAsync(headers);
            outlook.Setup(s => s.GetBody(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("body");

            var triage = new Mock<ITriageService>(MockBehavior.Strict);
            triage.Setup(s => s.AnalyzeHeader(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new TriageService.TriageResult(EmailCategory.Other, 10, "hint", Array.Empty<string>()));
            triage.Setup(s => s.AnalyzeWithBody(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new TriageService.TriageResult(EmailCategory.Other, 10, "hint", Array.Empty<string>()));

            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            digest.Setup(s => s.GenerateDigest(It.IsAny<IReadOnlyList<AnalyzedItem>>()))
                .Returns("DIGEST");
            digest.Setup(s => s.OpenTeams(It.IsAny<string>(), It.IsAny<string?>()));

            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>
            {
                new ReplyTemplate { Id = "T1", Title = "t", BodyContent = "b" },
            });
            template.Setup(s => s.FillTemplate(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
                .Returns("FILLED");

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Loose);

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            await InvokePrivateAsync(vm, "LoadEmailsAsync");

            vm.Emails.Should().HaveCount(3);
            vm.StatusMessage.Should().Be("메일 3개 로드 완료");
        });
    }

    [Fact]
    public async Task LoadEmailsAsync_WhenOutlookNotSupported_ShowsWarning()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            outlook.Setup(s => s.FetchInboxHeaders(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException());

            var triage = new Mock<ITriageService>(MockBehavior.Strict);
            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>());

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Strict);
            dialog.Setup(s => s.ShowWarning(It.IsAny<string>(), It.IsAny<string>()));

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            await InvokePrivateAsync(vm, "LoadEmailsAsync");

            vm.StatusMessage.Should().Be("Classic Outlook이 필요합니다. New Outlook(olk.exe)은 지원되지 않습니다.");
            dialog.Verify(s => s.ShowWarning(
                "Classic Outlook이 필요합니다. New Outlook(olk.exe)은 지원되지 않습니다.",
                "Outlook"),
                Times.Once);
        });
    }

    [Fact]
    public async Task LoadEmailsAsync_WhenOutlookUnavailable_ShowsInfo()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            outlook.Setup(s => s.FetchInboxHeaders(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());

            var triage = new Mock<ITriageService>(MockBehavior.Strict);
            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>());

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Strict);
            dialog.Setup(s => s.ShowInfo(It.IsAny<string>(), It.IsAny<string>()));

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            await InvokePrivateAsync(vm, "LoadEmailsAsync");

            vm.StatusMessage.Should().Be("Outlook과 연결할 수 없습니다. Classic Outlook 실행 및 상태를 확인해 주세요.");
            dialog.Verify(s => s.ShowInfo(
                "Outlook과 연결할 수 없습니다. Classic Outlook 실행 및 상태를 확인해 주세요.",
                "Outlook"),
                Times.Once);
        });
    }

    [Fact]
    public async Task GenerateDigestAsync_CallsDigestServiceAndShowsDialog()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            var triage = new Mock<ITriageService>(MockBehavior.Strict);

            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            digest.Setup(s => s.GenerateDigest(It.IsAny<IReadOnlyList<AnalyzedItem>>()))
                .Returns("DIGEST");
            digest.Setup(s => s.OpenTeams("DIGEST", "user@example.com"));

            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>());

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Strict);
            dialog.Setup(s => s.ShowInfo(It.IsAny<string>(), It.IsAny<string>()));

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            vm.TeamsUserEmail = "user@example.com";
            vm.Emails.AddRange(new[]
            {
                new AnalyzedItem
                {
                    EntryId = "id-1",
                    Sender = "S",
                    SenderEmail = "s@example.com",
                    Subject = "Sub",
                    ReceivedTime = DateTime.Now,
                    Score = 90,
                    RedactedSummary = "sum",
                    IsBodyLoaded = true,
                }
            });

            await InvokePrivateAsync(vm, "GenerateDigestAsync");

            digest.VerifyAll();
            dialog.Verify(s => s.ShowInfo(It.IsAny<string>(), "Digest 준비 완료"), Times.Once);
            vm.StatusMessage.Should().Be("클립보드에 복사 완료. Teams를 여는 중...");
        });
    }

    [Fact]
    public async Task ReplyAsync_CallsCreateDraft_WithRePrefix()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            outlook.Setup(s => s.CreateDraft(
                    "to@example.com",
                    "RE: Hello",
                    "FILLED",
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var triage = new Mock<ITriageService>(MockBehavior.Strict);
            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>
            {
                new ReplyTemplate { Id = "T1", Title = "t", BodyContent = "body {TargetDate}" },
            });
            template.Setup(s => s.FillTemplate(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
                .Returns("FILLED");

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Loose);

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            vm.SelectedEmail = new AnalyzedItem
            {
                EntryId = "id-1",
                Sender = "Sender",
                SenderEmail = "to@example.com",
                Subject = "Hello",
                ReceivedTime = DateTime.Now,
                Score = 10,
                RedactedSummary = "sum",
                IsBodyLoaded = true,
            };
            vm.SelectedTemplate = vm.Templates.First();

            await InvokePrivateAsync(vm, "ReplyAsync");

            outlook.VerifyAll();
        });
    }

    [Fact]
    public async Task GenerateDigestAsync_WhenDigestServiceThrows_ShowsError()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var outlook = new Mock<IOutlookService>(MockBehavior.Strict);
            var triage = new Mock<ITriageService>(MockBehavior.Strict);

            var digest = new Mock<IDigestService>(MockBehavior.Strict);
            digest.Setup(s => s.GenerateDigest(It.IsAny<IReadOnlyList<AnalyzedItem>>()))
                .Throws(new Exception("boom"));

            var template = new Mock<ITemplateService>(MockBehavior.Strict);
            template.Setup(s => s.GetTemplates()).Returns(new List<ReplyTemplate>());

            var redaction = new Mock<IRedactionService>(MockBehavior.Strict);
            redaction.Setup(s => s.Redact(It.IsAny<string>())).Returns("[REDACTED]");

            var dialog = new Mock<IDialogService>(MockBehavior.Strict);
            dialog.Setup(s => s.ShowError("Digest 생성 중 오류가 발생했습니다.", "오류"));

            var vm = CreateSut(
                outlook.Object,
                redaction.Object,
                triage.Object,
                digest.Object,
                template.Object,
                dialog.Object);

            vm.Emails.AddRange(new[]
            {
                new AnalyzedItem
                {
                    EntryId = "id-1",
                    Sender = "S",
                    SenderEmail = "s@example.com",
                    Subject = "Sub",
                    ReceivedTime = DateTime.Now,
                    Score = 90,
                    RedactedSummary = "sum",
                    IsBodyLoaded = true,
                }
            });

            await InvokePrivateAsync(vm, "GenerateDigestAsync");

            dialog.VerifyAll();
            vm.StatusMessage.Should().Be("Digest 생성 중 오류가 발생했습니다.");
        });
    }

    private static MainViewModel CreateSut(
        IOutlookService outlookService,
        IRedactionService redactionService,
        ITriageService triageService,
        IDigestService digestService,
        ITemplateService templateService,
        IDialogService dialogService)
    {
        // NOTE: ClipboardSecurityHelper is injected but not exercised here to avoid clipboard dependencies.
        var clipboard = new ClipboardSecurityHelper(new RedactionService());
        var settings = new TriageSettings { AutoRefreshIntervalMinutes = 0 };
        var options = new Mock<IOptionsMonitor<TriageSettings>>(MockBehavior.Strict);
        options.Setup(o => o.CurrentValue).Returns(settings);
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(settings);
        options.Setup(o => o.OnChange(It.IsAny<Action<TriageSettings, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        return new MainViewModel(
            outlookService,
            redactionService,
            clipboard,
            triageService,
            digestService,
            templateService,
            dialogService,
            new SessionStatsService(),
            options.Object,
            NullLogger<MainViewModel>.Instance);
    }

    private static async Task InvokePrivateAsync(MainViewModel vm, string name)
    {
        var method = typeof(MainViewModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, name, StringComparison.Ordinal) && m.GetParameters().Length == 0);

        var result = method.Invoke(vm, null);
        result.Should().BeAssignableTo<Task>();
        await ((Task)result!).ConfigureAwait(false);
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                action().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception is not null)
                    {
                        tcs.TrySetException(t.Exception.InnerExceptions);
                    }
                    else if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }

                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }, TaskScheduler.Default);

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
