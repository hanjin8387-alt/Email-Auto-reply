using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;

namespace MailTriageAssistant.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string OutlookNotSupportedMessage = "Classic Outlook이 필요합니다. New Outlook(olk.exe)은 지원되지 않습니다.";
    private const string OutlookUnavailableMessage = "Outlook과 연결할 수 없습니다. Classic Outlook 실행 및 상태를 확인해 주세요.";

    private readonly IOutlookService _outlookService;
    private readonly RedactionService _redactionService;
    private readonly ClipboardSecurityHelper _clipboardSecurityHelper;
    private readonly TriageService _triageService;
    private readonly DigestService _digestService;
    private readonly TemplateService _templateService;
    private readonly IDialogService _dialogService;

    private AnalyzedItem? _selectedEmail;
    private ReplyTemplate? _selectedTemplate;
    private string _statusMessage = "대기 중";
    private bool _isLoading;
    private string _teamsUserEmail = string.Empty;

    public RangeObservableCollection<AnalyzedItem> Emails { get; } = new();
    public List<ReplyTemplate> Templates { get; }

    public AnalyzedItem? SelectedEmail
    {
        get => _selectedEmail;
        set
        {
            if (SetProperty(ref _selectedEmail, value))
            {
                CommandManager.InvalidateRequerySuggested();
                _ = LoadSelectedEmailBodyAsync(value);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        IOutlookService outlookService,
        RedactionService redactionService,
        ClipboardSecurityHelper clipboardSecurityHelper,
        TriageService triageService,
        DigestService digestService,
        TemplateService templateService,
        IDialogService dialogService)
    {
        _outlookService = outlookService ?? throw new ArgumentNullException(nameof(outlookService));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _clipboardSecurityHelper = clipboardSecurityHelper ?? throw new ArgumentNullException(nameof(clipboardSecurityHelper));
        _triageService = triageService ?? throw new ArgumentNullException(nameof(triageService));
        _digestService = digestService ?? throw new ArgumentNullException(nameof(digestService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        Templates = _templateService.GetTemplates();
        SelectedTemplate = Templates.FirstOrDefault();

        LoadEmailsCommand = new AsyncRelayCommand(LoadEmailsAsync, () => !IsLoading);
        GenerateDigestCommand = new AsyncRelayCommand(GenerateDigestAsync, () => !IsLoading && Emails.Count > 0);
        ReplyCommand = new AsyncRelayCommand(ReplyAsync, () => !IsLoading && SelectedEmail is not null && SelectedTemplate is not null);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEmail is not null);
    }

    private async Task LoadEmailsAsync()
    {
        IsLoading = true;
        StatusMessage = "Outlook에서 메일 헤더를 불러오는 중...";

        Emails.Clear();
        SelectedEmail = null;

        try
        {
            var headers = await _outlookService.FetchInboxHeaders().ConfigureAwait(true);

            var analyzed = new List<AnalyzedItem>(headers.Count);
            foreach (var h in headers)
            {
                var triage = _triageService.AnalyzeHeader(h.SenderEmail, h.Subject);

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

            StatusMessage = Emails.Count == 0 ? "표시할 메일이 없습니다." : $"메일 {Emails.Count}개 로드 완료";
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
            StatusMessage = "메일을 불러오는 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSelectedEmailBodyAsync(AnalyzedItem? item)
    {
        if (item is null || item.IsBodyLoaded)
        {
            return;
        }

        try
        {
            StatusMessage = "본문을 불러오는 중...";

            var body = await _outlookService.GetBody(item.EntryId).ConfigureAwait(true);

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
    }

    private async Task GenerateDigestAsync()
    {
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
                if (item.IsBodyLoaded)
                {
                    continue;
                }

                var body = await _outlookService.GetBody(item.EntryId).ConfigureAwait(true);
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

            var digest = _digestService.GenerateDigest(top);
            _digestService.OpenTeams(digest, TeamsUserEmail);

            StatusMessage = "클립보드에 복사 완료. Teams를 여는 중...";

            _dialogService.ShowInfo(
                "클립보드에 Digest를 복사했습니다. Teams를 여는 중입니다.\nTeams에서 Copilot에 붙여넣어 주세요.",
                "Digest 준비 완료");
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
            StatusMessage = "Digest 생성 중 오류가 발생했습니다.";
            _dialogService.ShowError(StatusMessage, "오류");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReplyAsync()
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

            await _outlookService.CreateDraft(SelectedEmail.SenderEmail, subject, body).ConfigureAwait(true);
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
