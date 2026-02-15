using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Data;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging;

namespace MailTriageAssistant.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string OutlookNotSupportedMessage = "Classic Outlook이 필요합니다. New Outlook(olk.exe)은 지원되지 않습니다.";
    private const string OutlookUnavailableMessage = "Outlook과 연결할 수 없습니다. Classic Outlook 실행 및 상태를 확인해 주세요.";

    public sealed record CategoryFilterOption(string Label, EmailCategory? Category);

    private readonly IOutlookService _outlookService;
    private readonly IRedactionService _redactionService;
    private readonly ClipboardSecurityHelper _clipboardSecurityHelper;
    private readonly ITriageService _triageService;
    private readonly IDigestService _digestService;
    private readonly ITemplateService _templateService;
    private readonly IDialogService _dialogService;
    private readonly SessionStatsService _sessionStats;
    private readonly ILogger<MainViewModel> _logger;

    private AnalyzedItem? _selectedEmail;
    private ReplyTemplate? _selectedTemplate;
    private CategoryFilterOption _selectedCategoryFilter = null!;
    private string _statusMessage = "대기 중";
    private bool _isLoading;
    private string _teamsUserEmail = string.Empty;

    public RangeObservableCollection<AnalyzedItem> Emails { get; } = new();
    public ICollectionView EmailsView { get; }
    public List<CategoryFilterOption> CategoryFilterOptions { get; }
    public List<ReplyTemplate> Templates { get; }

    public AnalyzedItem? SelectedEmail
    {
        get => _selectedEmail;
        set
        {
            if (SetProperty(ref _selectedEmail, value))
            {
                CommandManager.InvalidateRequerySuggested();
                LoadSelectedEmailBodyAsync(value).SafeFireAndForget(_ =>
                {
                    StatusMessage = "본문을 불러오는 중 오류가 발생했습니다.";
                    _dialogService.ShowError(StatusMessage, "오류");
                });
            }
        }
    }

    public ReplyTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public CategoryFilterOption SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                EmailsView.Refresh();
            }
        }
    }

    public string TeamsUserEmail
    {
        get => _teamsUserEmail;
        set => SetProperty(ref _teamsUserEmail, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand LoadEmailsCommand { get; }
    public ICommand GenerateDigestCommand { get; }
    public ICommand ReplyCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand OpenInOutlookCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        IOutlookService outlookService,
        IRedactionService redactionService,
        ClipboardSecurityHelper clipboardSecurityHelper,
        ITriageService triageService,
        IDigestService digestService,
        ITemplateService templateService,
        IDialogService dialogService,
        SessionStatsService sessionStatsService,
        ILogger<MainViewModel> logger)
    {
        _outlookService = outlookService ?? throw new ArgumentNullException(nameof(outlookService));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _clipboardSecurityHelper = clipboardSecurityHelper ?? throw new ArgumentNullException(nameof(clipboardSecurityHelper));
        _triageService = triageService ?? throw new ArgumentNullException(nameof(triageService));
        _digestService = digestService ?? throw new ArgumentNullException(nameof(digestService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _sessionStats = sessionStatsService ?? throw new ArgumentNullException(nameof(sessionStatsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Templates = _templateService.GetTemplates();
        SelectedTemplate = Templates.FirstOrDefault();

        LoadEmailsCommand = new AsyncRelayCommand(LoadEmailsAsync, () => !IsLoading);
        GenerateDigestCommand = new AsyncRelayCommand(GenerateDigestAsync, () => !IsLoading && Emails.Count > 0);
        ReplyCommand = new AsyncRelayCommand(ReplyAsync, () => !IsLoading && SelectedEmail is not null && SelectedTemplate is not null);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEmail is not null);
        OpenInOutlookCommand = new AsyncRelayCommand(OpenInOutlookAsync, () => !IsLoading && SelectedEmail is not null);

        CategoryFilterOptions = new List<CategoryFilterOption>
        {
            new("전체", null),
            new("긴급(Action)", EmailCategory.Action),
            new("결재(Approval)", EmailCategory.Approval),
            new("VIP", EmailCategory.VIP),
            new("미팅(Meeting)", EmailCategory.Meeting),
            new("뉴스레터", EmailCategory.Newsletter),
            new("참고(FYI)", EmailCategory.FYI),
            new("기타", EmailCategory.Other),
        };
        _selectedCategoryFilter = CategoryFilterOptions[0];

        EmailsView = CollectionViewSource.GetDefaultView(Emails);
        EmailsView.Filter = FilterEmailByCategory;
    }

    private Task LoadEmailsAsync()
        => LoadEmailsAsync(CancellationToken.None);

    private async Task LoadEmailsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        _logger.LogInformation("LoadEmails started.");
        IsLoading = true;
        StatusMessage = "Outlook에서 메일 헤더를 불러오는 중...";

        Emails.Clear();
        SelectedEmail = null;

        try
        {
            var headers = await _outlookService.FetchInboxHeaders(ct).ConfigureAwait(true);
            _sessionStats.RecordHeadersLoaded(headers.Count);

            var analyzed = new List<AnalyzedItem>(headers.Count);
            foreach (var h in headers)
            {
                ct.ThrowIfCancellationRequested();
                var triage = _triageService.AnalyzeHeader(h.SenderEmail, h.Subject);
                _sessionStats.RecordTriage(triage.Category);

                analyzed.Add(new AnalyzedItem
                {
                    EntryId = h.EntryId,
                    Sender = h.SenderName,
                    SenderEmail = h.SenderEmail,
                    Subject = h.Subject,
                    ReceivedTime = h.ReceivedTime,
                    HasAttachments = h.HasAttachments,
                    Category = triage.Category,
                    Score = triage.Score,
                    ActionHint = triage.ActionHint,
                    Tags = triage.Tags,
                    RedactedSummary = "선택하면 본문을 로드하고 마스킹된 요약을 표시합니다.",
                    IsBodyLoaded = false,
                });
            }

            Emails.AddRange(analyzed.OrderByDescending(i => i.Score).ThenByDescending(i => i.ReceivedTime));
            EmailsView.Refresh();

            StatusMessage = Emails.Count == 0 ? "표시할 메일이 없습니다." : $"메일 {Emails.Count}개 로드 완료";
            _logger.LogInformation("LoadEmails completed: {Count} items.", Emails.Count);

            PrefetchTopBodiesAsync(ct).SafeFireAndForget();
        }
        catch (NotSupportedException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("LoadEmails blocked: Outlook not supported.");
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("LoadEmails failed: Outlook not available.");
            ShowOutlookUnavailable();
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("LoadEmails failed: {ExceptionType}.", ex.GetType().Name);
            StatusMessage = "메일을 불러오는 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            IsLoading = false;
#if DEBUG
            sw.Stop();
            MailTriageAssistant.Helpers.PerfEventSource.Log.Measure("LoadEmailsAsync", sw.ElapsedMilliseconds);
#endif
        }
    }

    private Task LoadSelectedEmailBodyAsync(AnalyzedItem? item)
        => LoadSelectedEmailBodyAsync(item, CancellationToken.None);

    private async Task LoadSelectedEmailBodyAsync(AnalyzedItem? item, CancellationToken ct)
    {
        if (item is null || item.IsBodyLoaded)
        {
            return;
        }

        var setLoading = !IsLoading;
        if (setLoading)
        {
            IsLoading = true;
        }

        try
        {
            StatusMessage = "본문을 불러오는 중...";

            var body = await _outlookService.GetBody(item.EntryId, ct).ConfigureAwait(true);

            var triage = _triageService.AnalyzeWithBody(item.SenderEmail, item.Subject, body);
            item.Category = triage.Category;
            item.Score = triage.Score;
            item.ActionHint = triage.ActionHint;
            item.Tags = triage.Tags;

            item.RedactedSummary = string.IsNullOrWhiteSpace(body)
                ? "(본문이 비어 있습니다.)"
                : _redactionService.Redact(body);
            item.IsBodyLoaded = true;

            StatusMessage = "본문 로드 완료";
            EmailsView.Refresh();
        }
        catch (NotSupportedException)
        {
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            ShowOutlookUnavailable();
        }
        catch
        {
            StatusMessage = "본문을 불러오는 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            if (setLoading)
            {
                IsLoading = false;
            }
        }
    }

    private Task PrefetchTopBodiesAsync()
        => PrefetchTopBodiesAsync(CancellationToken.None);

    private async Task PrefetchTopBodiesAsync(CancellationToken ct)
    {
        try
        {
            var top = Emails.Take(10).ToList();
            foreach (var item in top)
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsBodyLoaded)
                {
                    continue;
                }

                var body = await _outlookService.GetBody(item.EntryId, ct).ConfigureAwait(true);
                var triage = _triageService.AnalyzeWithBody(item.SenderEmail, item.Subject, body);

                item.Category = triage.Category;
                item.Score = triage.Score;
                item.ActionHint = triage.ActionHint;
                item.Tags = triage.Tags;

                item.RedactedSummary = string.IsNullOrWhiteSpace(body)
                    ? "(본문이 비어 있습니다.)"
                    : _redactionService.Redact(body);
                item.IsBodyLoaded = true;
            }

            EmailsView.Refresh();
        }
        catch
        {
            // Background optimization: ignore prefetch failures.
        }
    }

    private Task GenerateDigestAsync()
        => GenerateDigestAsync(CancellationToken.None);

    private async Task GenerateDigestAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        IsLoading = true;
        StatusMessage = "Digest 생성 중...";

        try
        {
            var top = Emails
                .OrderByDescending(e => e.Score)
                .ThenByDescending(e => e.ReceivedTime)
                .Take(10)
                .ToList();

            foreach (var item in top)
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsBodyLoaded)
                {
                    continue;
                }

                var body = await _outlookService.GetBody(item.EntryId, ct).ConfigureAwait(true);
                var triage = _triageService.AnalyzeWithBody(item.SenderEmail, item.Subject, body);

                item.Category = triage.Category;
                item.Score = triage.Score;
                item.ActionHint = triage.ActionHint;
                item.Tags = triage.Tags;

                item.RedactedSummary = string.IsNullOrWhiteSpace(body)
                    ? "(본문이 비어 있습니다.)"
                    : _redactionService.Redact(body);
                item.IsBodyLoaded = true;
            }

            EmailsView.Refresh();

            var digest = _digestService.GenerateDigest(top);
            _sessionStats.RecordDigestGenerated();
            _sessionStats.RecordDigestCopied();
            _sessionStats.RecordTeamsOpenAttempt();
            _digestService.OpenTeams(digest, TeamsUserEmail);

            StatusMessage = "클립보드에 복사 완료. Teams를 여는 중...";

            _dialogService.ShowInfo(
                "클립보드에 Digest를 복사했습니다. Teams를 여는 중입니다.\nTeams에서 Copilot에 붙여넣어 주세요.",
                "Digest 준비 완료");
        }
        catch (NotSupportedException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("GenerateDigest blocked: Outlook not supported.");
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("GenerateDigest failed: Outlook not available.");
            ShowOutlookUnavailable();
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("GenerateDigest failed: {ExceptionType}.", ex.GetType().Name);
            StatusMessage = "Digest 생성 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            IsLoading = false;
#if DEBUG
            sw.Stop();
            MailTriageAssistant.Helpers.PerfEventSource.Log.Measure("GenerateDigestAsync", sw.ElapsedMilliseconds);
#endif
        }
    }

    private Task ReplyAsync()
        => ReplyAsync(CancellationToken.None);

    private async Task ReplyAsync(CancellationToken ct)
    {
        if (SelectedEmail is null || SelectedTemplate is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedEmail.SenderEmail))
        {
            _dialogService.ShowInfo(
                "수신자 이메일 주소를 확인할 수 없습니다.",
                "템플릿 답장");
            return;
        }

        try
        {
            IsLoading = true;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetDate"] = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd"),
                ["ItemName"] = SelectedEmail.Subject,
            };

            var body = _templateService.FillTemplate(SelectedTemplate.BodyContent, values);

            var subject = SelectedEmail.Subject;
            if (!subject.TrimStart().StartsWith("RE:", StringComparison.OrdinalIgnoreCase))
            {
                subject = $"RE: {subject}";
            }

            await _outlookService.CreateDraft(SelectedEmail.SenderEmail, subject, body, ct).ConfigureAwait(true);
            StatusMessage = "Outlook 초안이 열렸습니다.";
        }
        catch (NotSupportedException)
        {
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            ShowOutlookUnavailable();
        }
        catch
        {
            StatusMessage = "초안 생성 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task OpenInOutlookAsync()
        => OpenInOutlookAsync(CancellationToken.None);

    private async Task OpenInOutlookAsync(CancellationToken ct)
    {
        if (SelectedEmail is null)
        {
            return;
        }

        var setLoading = !IsLoading;
        if (setLoading)
        {
            IsLoading = true;
        }

        try
        {
            await _outlookService.OpenItem(SelectedEmail.EntryId, ct).ConfigureAwait(true);
            StatusMessage = "Outlook에서 메일을 열었습니다.";
        }
        catch (NotSupportedException)
        {
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            ShowOutlookUnavailable();
        }
        catch
        {
            StatusMessage = "Outlook에서 메일을 여는 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            if (setLoading)
            {
                IsLoading = false;
            }
        }
    }

    private void CopySelected()
    {
        if (SelectedEmail is null)
        {
            return;
        }

        try
        {
            var text = SelectedEmail.RedactedSummary ?? string.Empty;
            _clipboardSecurityHelper.SecureCopy(text);
            StatusMessage = "선택한 요약을 클립보드에 복사했습니다. (30초 후 자동 삭제)";
        }
        catch
        {
            StatusMessage = "클립보드 복사 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
    }

    private void ShowOutlookNotSupported()
    {
        StatusMessage = OutlookNotSupportedMessage;
        _dialogService.ShowWarning(OutlookNotSupportedMessage, "Outlook");
    }

    private void ShowOutlookUnavailable()
    {
        StatusMessage = OutlookUnavailableMessage;
        _dialogService.ShowInfo(OutlookUnavailableMessage, "Outlook");
    }

    private bool FilterEmailByCategory(object obj)
    {
        if (obj is not AnalyzedItem item)
        {
            return false;
        }

        var category = SelectedCategoryFilter.Category;
        return category is null || item.Category == category.Value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
