using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
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
    private readonly IOptionsMonitor<TriageSettings> _settingsMonitor;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IDisposable? _settingsChangeSubscription;

    private readonly DispatcherTimer _autoRefreshTimer;
    private readonly DispatcherTimer _autoRefreshStatusTimer;
    private CancellationTokenSource? _autoRefreshCts;
    private int _autoRefreshFailureStreak;
    private Task _prefetchTask = Task.CompletedTask;

    private AnalyzedItem? _selectedEmail;
    private ReplyTemplate? _selectedTemplate;
    private CategoryFilterOption _selectedCategoryFilter = null!;
    private string _statusMessage = "대기 중";
    private bool _isLoading;
    private string _teamsUserEmail = string.Empty;
    private bool _autoRefreshPaused;
    private DateTimeOffset? _nextAutoRefreshAt;
    private string _autoRefreshStatusText = string.Empty;

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
                LoadSelectedEmailBodyAsync(value).SafeFireAndForget();
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

    public bool AutoRefreshPaused
    {
        get => _autoRefreshPaused;
        private set => SetProperty(ref _autoRefreshPaused, value);
    }

    public DateTimeOffset? NextAutoRefreshAt
    {
        get => _nextAutoRefreshAt;
        private set => SetProperty(ref _nextAutoRefreshAt, value);
    }

    public string AutoRefreshStatusText
    {
        get => _autoRefreshStatusText;
        private set => SetProperty(ref _autoRefreshStatusText, value);
    }

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
        IOptionsMonitor<TriageSettings> settingsMonitor,
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
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _autoRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            IsEnabled = false,
        };
        _autoRefreshTimer.Tick += OnAutoRefreshTimerTick;

        _autoRefreshStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1),
            IsEnabled = false,
        };
        _autoRefreshStatusTimer.Tick += (_, _) => UpdateAutoRefreshStatusText();

        Templates = _templateService.GetTemplates();
        SelectedTemplate = Templates.FirstOrDefault();

        LoadEmailsCommand = new AsyncRelayCommand(() => LoadEmailsAsync(), () => !IsLoading);
        GenerateDigestCommand = new AsyncRelayCommand(() => GenerateDigestAsync(), () => !IsLoading && Emails.Count > 0);
        ReplyCommand = new AsyncRelayCommand(() => ReplyAsync(), () => !IsLoading && SelectedEmail is not null && SelectedTemplate is not null);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEmail is not null);
        OpenInOutlookCommand = new AsyncRelayCommand(() => OpenInOutlookAsync(), () => !IsLoading && SelectedEmail is not null);

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
        try
        {
            // Enables automatic re-filtering when item.Category changes (avoids manual EmailsView.Refresh()).
            if (EmailsView is ICollectionViewLiveShaping live)
            {
                if (!live.LiveFilteringProperties.Contains(nameof(AnalyzedItem.Category)))
                {
                    live.LiveFilteringProperties.Add(nameof(AnalyzedItem.Category));
                }

                live.IsLiveFiltering = true;
            }
        }
        catch
        {
            // Live shaping is best-effort; fall back to manual refresh when not available.
        }

        ApplyAutoRefreshSettings(_settingsMonitor.CurrentValue);
        _settingsChangeSubscription = _settingsMonitor.OnChange((settings, _) => ApplyAutoRefreshSettings(settings));
    }

    public void Dispose()
    {
        try
        {
            _autoRefreshTimer.Stop();
        }
        catch
        {
            // Ignore timer stop failures.
        }

        try
        {
            _autoRefreshStatusTimer.Stop();
        }
        catch
        {
            // Ignore timer stop failures.
        }

        try
        {
            _settingsChangeSubscription?.Dispose();
        }
        catch
        {
            // Ignore subscription disposal failures.
        }

        try
        {
            _autoRefreshCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation failures.
        }

        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
    }

    private enum LoadEmailsOutcome
    {
        Success,
        Failure,
        Cancelled,
    }

    private async Task LoadEmailsAsync(CancellationToken ct = default)
    {
        await TryLoadEmailsAsync(ct, showDialogs: true).ConfigureAwait(true);
        ResetAutoRefreshAfterManualRun();
    }

    private async Task<LoadEmailsOutcome> TryLoadEmailsAsync(CancellationToken ct, bool showDialogs)
    {
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        using var perf = PerfScope.Start("header_load_ms", _logger);
        _logger.LogInformation("LoadEmails started (showDialogs={ShowDialogs}).", showDialogs);
        IsLoading = true;
        StatusMessage = "Outlook에서 메일 헤더를 불러오는 중...";

        var selectedEntryId = SelectedEmail?.EntryId;

        try
        {
            var headers = await _outlookService.FetchInboxHeaders(ct).ConfigureAwait(true);
            _sessionStats.RecordHeadersLoaded(headers.Count);

            var existingById = new Dictionary<string, AnalyzedItem>(StringComparer.Ordinal);
            foreach (var existing in Emails)
            {
                if (!string.IsNullOrWhiteSpace(existing.EntryId) && !existingById.ContainsKey(existing.EntryId))
                {
                    existingById.Add(existing.EntryId, existing);
                }
            }

            var analyzed = new List<AnalyzedItem>(headers.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in headers)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(h.EntryId) || !seen.Add(h.EntryId))
                {
                    continue;
                }

                if (existingById.TryGetValue(h.EntryId, out var existing))
                {
                    if (string.IsNullOrEmpty(existing.RedactedSender))
                    {
                        existing.RedactedSender = _redactionService.Redact(existing.Sender);
                    }

                    if (string.IsNullOrEmpty(existing.RedactedSubject))
                    {
                        existing.RedactedSubject = _redactionService.Redact(existing.Subject);
                    }

                    if (existing.IsBodyLoaded)
                    {
                        _sessionStats.RecordTriage(existing.Category);
                    }
                    else
                    {
                        var triage = _triageService.AnalyzeHeader(h.SenderEmail, h.Subject);
                        _sessionStats.RecordTriage(triage.Category);

                        existing.Category = triage.Category;
                        existing.Score = triage.Score;
                        existing.ActionHint = triage.ActionHint;
                        existing.Tags = triage.Tags;
                        existing.RedactedSummary = "선택하면 본문을 로드하고 마스킹된 요약을 표시합니다.";
                    }

                    analyzed.Add(existing);
                    continue;
                }

                var newTriage = _triageService.AnalyzeHeader(h.SenderEmail, h.Subject);
                _sessionStats.RecordTriage(newTriage.Category);

                analyzed.Add(new AnalyzedItem
                {
                    EntryId = h.EntryId,
                    Sender = h.SenderName,
                    SenderEmail = h.SenderEmail,
                    Subject = h.Subject,
                    ReceivedTime = h.ReceivedTime,
                    HasAttachments = h.HasAttachments,
                    RedactedSender = _redactionService.Redact(h.SenderName),
                    RedactedSubject = _redactionService.Redact(h.Subject),
                    Category = newTriage.Category,
                    Score = newTriage.Score,
                    ActionHint = newTriage.ActionHint,
                    Tags = newTriage.Tags,
                    RedactedSummary = "선택하면 본문을 로드하고 마스킹된 요약을 표시합니다.",
                    IsBodyLoaded = false,
                });
            }

            var sorted = analyzed
                .OrderByDescending(i => i.Score)
                .ThenByDescending(i => i.ReceivedTime)
                .ToList();

            using (EmailsView.DeferRefresh())
            {
                if (Emails.Count == 0)
                {
                    Emails.AddRange(sorted);
                }
                else
                {
                    var currentIndexById = new Dictionary<string, int>(Emails.Count, StringComparer.Ordinal);

                    void RefreshIndexRange(int start, int end)
                    {
                        for (var idx = start; idx <= end && idx < Emails.Count; idx++)
                        {
                            var entryId = Emails[idx].EntryId;
                            if (!string.IsNullOrWhiteSpace(entryId))
                            {
                                currentIndexById[entryId] = idx;
                            }
                        }
                    }

                    RefreshIndexRange(0, Emails.Count - 1);

                    for (var i = 0; i < sorted.Count; i++)
                    {
                        var desiredItem = sorted[i];
                        var desiredEntryId = desiredItem.EntryId;

                        if (i >= Emails.Count)
                        {
                            Emails.Add(desiredItem);

                            if (!string.IsNullOrWhiteSpace(desiredEntryId))
                            {
                                currentIndexById[desiredEntryId] = Emails.Count - 1;
                            }

                            continue;
                        }

                        if (string.Equals(Emails[i].EntryId, desiredEntryId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var existingIndex = !string.IsNullOrWhiteSpace(desiredEntryId)
                            && currentIndexById.TryGetValue(desiredEntryId, out var idx2)
                            && idx2 > i
                            ? idx2
                            : -1;

                        if (existingIndex >= 0)
                        {
                            Emails.Move(existingIndex, i);
                            RefreshIndexRange(i, existingIndex);
                        }
                        else
                        {
                            Emails.Insert(i, desiredItem);
                            RefreshIndexRange(i, Emails.Count - 1);
                        }
                    }

                    while (Emails.Count > sorted.Count)
                    {
                        Emails.RemoveAt(Emails.Count - 1);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedEntryId))
            {
                var nextSelected = Emails.FirstOrDefault(i => string.Equals(i.EntryId, selectedEntryId, StringComparison.Ordinal));
                if (!ReferenceEquals(nextSelected, SelectedEmail))
                {
                    SelectedEmail = nextSelected;
                }
            }
            else if (Emails.Count == 0)
            {
                SelectedEmail = null;
            }

            StatusMessage = Emails.Count == 0 ? "표시할 메일이 없습니다." : $"메일 {Emails.Count}개 로드 완료";
            _logger.LogInformation("LoadEmails completed: {Count} items.", Emails.Count);

            _prefetchTask = PrefetchTopBodiesAsync(ct);
            return LoadEmailsOutcome.Success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LoadEmails cancelled.");
            return LoadEmailsOutcome.Cancelled;
        }
        catch (NotSupportedException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("LoadEmails blocked: Outlook not supported.");
            if (showDialogs)
            {
                ShowOutlookNotSupported();
            }
            else
            {
                StatusMessage = OutlookNotSupportedMessage;
            }

            return LoadEmailsOutcome.Failure;
        }
        catch (InvalidOperationException)
        {
            _sessionStats.RecordError();
            _logger.LogWarning("LoadEmails failed: Outlook not available.");
            if (showDialogs)
            {
                ShowOutlookUnavailable();
            }
            else
            {
                StatusMessage = OutlookUnavailableMessage;
            }

            return LoadEmailsOutcome.Failure;
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("LoadEmails failed: {ExceptionType}.", ex.GetType().Name);
            StatusMessage = "메일을 불러오는 중 오류가 발생했습니다.";
            if (showDialogs)
            {
                _dialogService.ShowError(StatusMessage, "오류");
            }

            return LoadEmailsOutcome.Failure;
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

    private void ApplyAutoRefreshSettings(TriageSettings settings)
    {
        var minutes = GetAutoRefreshMinutes(settings);
        if (minutes <= 0)
        {
            _autoRefreshStatusTimer.Stop();
            StopAutoRefresh();
            return;
        }

        _autoRefreshStatusTimer.Start();
        _autoRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);

        if (AutoRefreshPaused)
        {
            // Keep paused until a manual run or interval is disabled/enabled.
            _autoRefreshTimer.Stop();
            NextAutoRefreshAt = null;
            UpdateAutoRefreshStatusText();
            return;
        }

        ResetAutoRefreshTimer();
    }

    private void ResetAutoRefreshAfterManualRun()
    {
        if (AutoRefreshPaused)
        {
            AutoRefreshPaused = false;
            _autoRefreshFailureStreak = 0;
        }

        ResetAutoRefreshTimer();
    }

    private static int GetAutoRefreshMinutes(TriageSettings settings)
        => Math.Max(0, settings.AutoRefreshIntervalMinutes);

    private int GetAutoRefreshMinutes()
        => GetAutoRefreshMinutes(_settingsMonitor.CurrentValue);
    private void ResetAutoRefreshTimer()
    {
        var minutes = GetAutoRefreshMinutes();
        if (minutes <= 0 || AutoRefreshPaused)
        {
            _autoRefreshTimer.Stop();
            NextAutoRefreshAt = null;
            UpdateAutoRefreshStatusText();
            return;
        }

        _autoRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Start();
        NextAutoRefreshAt = DateTimeOffset.Now.AddMinutes(minutes);
        UpdateAutoRefreshStatusText();
    }

    private void StopAutoRefresh()
    {
        _autoRefreshTimer.Stop();
        _autoRefreshStatusTimer.Stop();
        NextAutoRefreshAt = null;

        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;

        _autoRefreshFailureStreak = 0;
        AutoRefreshPaused = false;
        UpdateAutoRefreshStatusText();
    }

    private void OnAutoRefreshTimerTick(object? sender, EventArgs e)
    {
        _autoRefreshTimer.Stop();

        AutoRefreshTickAsync().SafeFireAndForget(ex =>
        {
            _logger.LogError("AutoRefresh failed: {ExceptionType}.", ex.GetType().Name);
        });
    }

    private async Task AutoRefreshTickAsync()
    {
        if (AutoRefreshPaused)
        {
            return;
        }

        var minutes = GetAutoRefreshMinutes();
        if (minutes <= 0)
        {
            StopAutoRefresh();
            return;
        }

        if (IsLoading)
        {
            ResetAutoRefreshTimer();
            return;
        }

        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = new CancellationTokenSource();

        var outcome = await TryLoadEmailsAsync(_autoRefreshCts.Token, showDialogs: false).ConfigureAwait(true);

        if (outcome == LoadEmailsOutcome.Success)
        {
            _autoRefreshFailureStreak = 0;
        }
        else if (outcome == LoadEmailsOutcome.Failure)
        {
            _autoRefreshFailureStreak++;
            if (_autoRefreshFailureStreak >= 3)
            {
                AutoRefreshPaused = true;
                NextAutoRefreshAt = null;
                StatusMessage = "자동 분류가 3회 연속 실패하여 일시 중지되었습니다.";
                _dialogService.ShowWarning(StatusMessage, "자동 분류");
                UpdateAutoRefreshStatusText();
                return;
            }
        }

        ResetAutoRefreshTimer();
    }

    private void UpdateAutoRefreshStatusText()
    {
        var configuredMinutes = GetAutoRefreshMinutes();
        if (configuredMinutes <= 0)
        {
            AutoRefreshStatusText = string.Empty;
            return;
        }

        if (AutoRefreshPaused)
        {
            AutoRefreshStatusText = "자동 분류: 일시 중지";
            return;
        }

        if (NextAutoRefreshAt is null)
        {
            AutoRefreshStatusText = $"다음 분류: {configuredMinutes}분 후";
            return;
        }

        var remaining = NextAutoRefreshAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            AutoRefreshStatusText = "다음 분류: 곧";
            return;
        }

        // Rounded to minutes: the status timer ticks every minute to avoid noisy UI updates.
        var remainingMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
        AutoRefreshStatusText = $"다음 분류: {remainingMinutes}분 후";
    }

    private async Task LoadSelectedEmailBodyAsync(AnalyzedItem? item, CancellationToken ct = default)
    {
        if (item is null || item.IsBodyLoaded)
        {
            return;
        }

        using var perf = PerfScope.Start("body_load_ms", _logger);

        var setLoading = !IsLoading;
        if (setLoading)
        {
            IsLoading = true;
        }

        try
        {
            await ExecuteOutlookOperationAsync(
                async () =>
                {
                    StatusMessage = "본문을 불러오는 중...";

                    var body = await _outlookService.GetBody(item.EntryId, ct).ConfigureAwait(true);
                    ApplyBodyAnalysis(item, body);

                    StatusMessage = "본문 로드 완료";
                    EmailsView.Refresh();
                },
                "본문을 불러오는 중 오류가 발생했습니다.").ConfigureAwait(true);
        }
        finally
        {
            if (setLoading)
            {
                IsLoading = false;
            }
        }
    }

    private async Task PrefetchTopBodiesAsync(CancellationToken ct)
    {
        using var _ = PerfScope.Start("prefetch_ms", _logger);

        try
        {
            var prefetchCount = Math.Clamp(_settingsMonitor.CurrentValue.PrefetchCount, 0, 50);
            if (prefetchCount <= 0)
            {
                return;
            }

            var targets = Emails
                .Take(prefetchCount)
                .Where(i => !i.IsBodyLoaded && !string.IsNullOrWhiteSpace(i.EntryId))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            var entryIds = targets
                .Select(i => i.EntryId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var loadCompleteStatus = Emails.Count == 0 ? "표시할 메일이 없습니다." : $"메일 {Emails.Count}개 로드 완료";
            var prefetchStatus = $"본문 {entryIds.Count}건 미리 불러오는 중...";
            var statusWasSet = false;

            var bodiesTask = _outlookService.GetBodies(entryIds, ct);
            var delayTask = Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            if (ReferenceEquals(await Task.WhenAny(bodiesTask, delayTask).ConfigureAwait(true), delayTask)
                && string.Equals(StatusMessage, loadCompleteStatus, StringComparison.Ordinal))
            {
                StatusMessage = prefetchStatus;
                statusWasSet = true;
            }

            var bodies = await bodiesTask.ConfigureAwait(true);
            foreach (var item in targets)
            {
                ct.ThrowIfCancellationRequested();
                if (!bodies.TryGetValue(item.EntryId, out var body))
                {
                    continue;
                }

                ApplyBodyAnalysis(item, body);
            }

            if (statusWasSet && string.Equals(StatusMessage, prefetchStatus, StringComparison.Ordinal))
            {
                StatusMessage = loadCompleteStatus;
            }
        }
        catch
        {
            // Background optimization: ignore prefetch failures.
        }
    }

    private async Task GenerateDigestAsync(CancellationToken ct = default)
    {
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        IsLoading = true;
        StatusMessage = "Digest 생성 중...";

        try
        {
            await ExecuteOutlookOperationAsync(
                async () =>
                {
                    string digest;
                    using (PerfScope.Start("digest_ms", _logger))
                    {
                        var top = Emails
                            .OrderByDescending(e => e.Score)
                            .ThenByDescending(e => e.ReceivedTime)
                            .Take(10)
                            .ToList();

                        var prefetchTask = _prefetchTask;
                        try
                        {
                            await prefetchTask.ConfigureAwait(true);
                        }
                        catch
                        {
                            // Ignore prefetch failures.
                        }

                        var missing = top
                            .Where(i => !i.IsBodyLoaded && !string.IsNullOrWhiteSpace(i.EntryId))
                            .ToList();
                        if (missing.Count > 0)
                        {
                            var entryIds = missing
                                .Select(i => i.EntryId)
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

                            var bodies = await _outlookService.GetBodies(entryIds, ct).ConfigureAwait(true);

                            foreach (var item in missing)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (!bodies.TryGetValue(item.EntryId, out var body))
                                {
                                    continue;
                                }

                                ApplyBodyAnalysis(item, body);
                            }
                        }

                        digest = _digestService.GenerateDigest(top);
                    }

                    _sessionStats.RecordDigestGenerated();
                    _digestService.OpenTeams(digest, TeamsUserEmail);
                    _sessionStats.RecordDigestCopied();
                    _sessionStats.RecordTeamsOpenAttempt();

                    StatusMessage = "클립보드에 복사 완료. Teams를 여는 중...";

                    _dialogService.ShowInfo(
                        "클립보드에 Digest를 복사했습니다. Teams를 여는 중입니다.\nTeams에서 Copilot에 붙여넣어 주세요.",
                        "Digest 준비 완료");
                },
                "Digest 생성 중 오류가 발생했습니다.").ConfigureAwait(true);
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

    private async Task ReplyAsync(CancellationToken ct = default)
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
            await ExecuteOutlookOperationAsync(
                async () =>
                {
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
                },
                "초안 생성 중 오류가 발생했습니다.").ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OpenInOutlookAsync(CancellationToken ct = default)
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
            await ExecuteOutlookOperationAsync(
                async () =>
                {
                    await _outlookService.OpenItem(SelectedEmail.EntryId, ct).ConfigureAwait(true);
                    StatusMessage = "Outlook에서 메일을 열었습니다.";
                },
                "Outlook에서 메일을 여는 중 오류가 발생했습니다.").ConfigureAwait(true);
        }
        finally
        {
            if (setLoading)
            {
                IsLoading = false;
            }
        }
    }

    private void ApplyBodyAnalysis(AnalyzedItem item, string body)
    {
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

    private async Task ExecuteOutlookOperationAsync(Func<Task> operation, string errorMessage)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (NotSupportedException)
        {
            _sessionStats.RecordError();
            ShowOutlookNotSupported();
        }
        catch (InvalidOperationException)
        {
            _sessionStats.RecordError();
            ShowOutlookUnavailable();
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("{ErrorMessage}: {ExceptionType}.", errorMessage, ex.GetType().Name);
            StatusMessage = errorMessage;
            _dialogService.ShowError(StatusMessage, "오류");
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
