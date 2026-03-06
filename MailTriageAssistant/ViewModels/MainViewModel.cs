using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging;

namespace MailTriageAssistant.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public sealed record CategoryFilterOption(string Label, EmailCategory? Category);

    private readonly ClipboardSecurityHelper _clipboardSecurityHelper;
    private readonly IDialogService _dialogService;
    private readonly ITemplateService _templateService;
    private readonly CreateReplyDraftWorkflow _createReplyDraftWorkflow;
    private readonly EmailListProjectionService _projectionService;
    private readonly IMainViewModelWorkflow _workflow;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IAutoRefreshController _autoRefreshController;

    private AnalyzedItem? _selectedEmail;
    private ReplyTemplate? _selectedTemplate;
    private CategoryFilterOption _selectedCategoryFilter = null!;
    private string _statusMessage = LocalizedStrings.Get("Str.Status.Idle", "Idle");
    private string _teamsUserEmail = string.Empty;
    private bool _autoRefreshPaused;
    private DateTimeOffset? _nextAutoRefreshAt;
    private string _autoRefreshStatusText = string.Empty;
    private Task _prefetchTask = Task.CompletedTask;
    private bool _isLiveSortingEnabled;
    private bool _isInboxRefreshInProgress;
    private bool _isSelectedBodyLoadInProgress;
    private bool _isDigestGenerationInProgress;
    private bool _isReplyDraftCreationInProgress;
    private bool _isOpenInOutlookInProgress;
    private CancellationTokenSource? _selectedBodyLoadCts;
    private long _selectedBodyLoadRequestId;

    public MainViewModel(
        ClipboardSecurityHelper clipboardSecurityHelper,
        IDialogService dialogService,
        ITemplateService templateService,
        CreateReplyDraftWorkflow createReplyDraftWorkflow,
        EmailListProjectionService projectionService,
        IMainViewModelWorkflow workflow,
        IAutoRefreshControllerFactory autoRefreshControllerFactory,
        ILogger<MainViewModel> logger)
    {
        _clipboardSecurityHelper = clipboardSecurityHelper ?? throw new ArgumentNullException(nameof(clipboardSecurityHelper));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _createReplyDraftWorkflow = createReplyDraftWorkflow ?? throw new ArgumentNullException(nameof(createReplyDraftWorkflow));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Templates = _templateService.GetTemplates();
        SelectedTemplate = Templates.FirstOrDefault();

        LoadEmailsCommand = new AsyncRelayCommand(() => LoadEmailsAsync(), () => !IsLoading, HandleUnexpectedCommandException);
        GenerateDigestCommand = new AsyncRelayCommand(() => GenerateDigestAsync(), () => !IsLoading && Emails.Count > 0, HandleUnexpectedCommandException);
        ReplyCommand = new AsyncRelayCommand(() => ReplyAsync(), () => !IsLoading && SelectedEmail is not null && SelectedTemplate is not null, HandleUnexpectedCommandException);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEmail is not null);
        OpenInOutlookCommand = new AsyncRelayCommand(() => OpenInOutlookAsync(), () => !IsLoading && SelectedEmail is not null, HandleUnexpectedCommandException);

        CategoryFilterOptions = new List<CategoryFilterOption>
        {
            new(LocalizedStrings.Get("Str.Filter.Category.All", "All"), null),
            new(LocalizedStrings.Get("Str.Filter.Category.Action", "Action"), EmailCategory.Action),
            new(LocalizedStrings.Get("Str.Filter.Category.Approval", "Approval"), EmailCategory.Approval),
            new(LocalizedStrings.Get("Str.Filter.Category.Vip", "VIP"), EmailCategory.VIP),
            new(LocalizedStrings.Get("Str.Filter.Category.Meeting", "Meeting"), EmailCategory.Meeting),
            new(LocalizedStrings.Get("Str.Filter.Category.Newsletter", "Newsletter"), EmailCategory.Newsletter),
            new(LocalizedStrings.Get("Str.Filter.Category.Fyi", "FYI"), EmailCategory.FYI),
            new(LocalizedStrings.Get("Str.Filter.Category.Other", "Other"), EmailCategory.Other),
        };
        _selectedCategoryFilter = CategoryFilterOptions[0];

        EmailsView = CollectionViewSource.GetDefaultView(Emails);
        EmailsView.Filter = FilterEmailByCategory;
        EmailsView.SortDescriptions.Clear();
        EmailsView.SortDescriptions.Add(new SortDescription(nameof(AnalyzedItem.Score), ListSortDirection.Descending));
        EmailsView.SortDescriptions.Add(new SortDescription(nameof(AnalyzedItem.ReceivedTime), ListSortDirection.Descending));

        try
        {
            if (EmailsView is ICollectionViewLiveShaping live)
            {
                if (!live.LiveFilteringProperties.Contains(nameof(AnalyzedItem.Category)))
                {
                    live.LiveFilteringProperties.Add(nameof(AnalyzedItem.Category));
                }

                live.IsLiveFiltering = true;

                if (!live.LiveSortingProperties.Contains(nameof(AnalyzedItem.Score)))
                {
                    live.LiveSortingProperties.Add(nameof(AnalyzedItem.Score));
                }

                if (!live.LiveSortingProperties.Contains(nameof(AnalyzedItem.ReceivedTime)))
                {
                    live.LiveSortingProperties.Add(nameof(AnalyzedItem.ReceivedTime));
                }

                live.IsLiveSorting = true;
                _isLiveSortingEnabled = live.IsLiveSorting == true;
            }
        }
        catch
        {
            _isLiveSortingEnabled = false;
        }

        _autoRefreshController = (autoRefreshControllerFactory ?? throw new ArgumentNullException(nameof(autoRefreshControllerFactory))).Create(
            refreshOperation: token => TryLoadEmailsAsync(token, showDialogs: false),
            isLoading: () => IsLoading,
            setStatusMessage: message => StatusMessage = message);
        _autoRefreshController.StateChanged += OnAutoRefreshStateChanged;
        SynchronizeAutoRefreshState();

        RebuildTemplateFieldInputs();
    }

    public RangeObservableCollection<AnalyzedItem> Emails { get; } = new();
    public ICollectionView EmailsView { get; }
    public List<CategoryFilterOption> CategoryFilterOptions { get; }
    public List<ReplyTemplate> Templates { get; }
    public ObservableCollection<ReplyTemplateFieldInput> TemplateFieldInputs { get; } = new();

    public AnalyzedItem? SelectedEmail
    {
        get => _selectedEmail;
        set
        {
            if (SetProperty(ref _selectedEmail, value))
            {
                CommandManager.InvalidateRequerySuggested();
                RebuildTemplateFieldInputs();
                StartSelectedBodyLoad(value);
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
                RebuildTemplateFieldInputs();
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

    public bool IsInboxRefreshInProgress
    {
        get => _isInboxRefreshInProgress;
        private set => SetBusyFlag(ref _isInboxRefreshInProgress, value);
    }

    public bool IsSelectedBodyLoadInProgress
    {
        get => _isSelectedBodyLoadInProgress;
        private set => SetBusyFlag(ref _isSelectedBodyLoadInProgress, value);
    }

    public bool IsDigestGenerationInProgress
    {
        get => _isDigestGenerationInProgress;
        private set => SetBusyFlag(ref _isDigestGenerationInProgress, value);
    }

    public bool IsReplyDraftCreationInProgress
    {
        get => _isReplyDraftCreationInProgress;
        private set => SetBusyFlag(ref _isReplyDraftCreationInProgress, value);
    }

    public bool IsLoading
        => IsInboxRefreshInProgress
           || IsSelectedBodyLoadInProgress
           || IsDigestGenerationInProgress
           || IsReplyDraftCreationInProgress
           || _isOpenInOutlookInProgress;

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

    public ICommand LoadEmailsCommand { get; }
    public ICommand GenerateDigestCommand { get; }
    public ICommand ReplyCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand OpenInOutlookCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        CancelSelectedBodyLoad();
        _autoRefreshController.StateChanged -= OnAutoRefreshStateChanged;
        _autoRefreshController.Dispose();
    }

    private async Task LoadEmailsAsync(CancellationToken ct = default)
    {
        var outcome = await TryLoadEmailsAsync(ct, showDialogs: true).ConfigureAwait(true);
        if (outcome == InboxRefreshOutcome.Success)
        {
            _autoRefreshController.NotifyManualRunSucceeded();
        }
    }

    private async Task<InboxRefreshOutcome> TryLoadEmailsAsync(CancellationToken ct, bool showDialogs)
    {
        IsInboxRefreshInProgress = true;
        StatusMessage = LocalizedStrings.Get("Str.Status.LoadingHeaders", "Loading emails from Outlook.");
        var selectedEntryId = SelectedEmail?.EntryId;

        try
        {
            var result = await _workflow
                .LoadEmailsAsync(Emails.ToList(), selectedEntryId, showDialogs, ct)
                .ConfigureAwait(true);

            if (result.Outcome == InboxRefreshOutcome.Success)
            {
                using (EmailsView.DeferRefresh())
                {
                    _projectionService.ApplyDiff(Emails, result.SortedItems);
                }

                SelectedEmail = EmailListProjectionService.RestoreSelection(Emails, result.RestoredSelectionEntryId);
                if (SelectedEmail is null && Emails.Count == 0)
                {
                    SelectedEmail = null;
                }
            }

            StatusMessage = result.StatusMessage;
            _prefetchTask = result.PrefetchTask;
            return result.Outcome;
        }
        finally
        {
            IsInboxRefreshInProgress = false;
        }
    }

    private void StartSelectedBodyLoad(AnalyzedItem? item)
    {
        Interlocked.Increment(ref _selectedBodyLoadRequestId);
        CancelSelectedBodyLoad();

        if (item is null || item.IsBodyLoaded)
        {
            IsSelectedBodyLoadInProgress = false;
            return;
        }

        var requestId = Volatile.Read(ref _selectedBodyLoadRequestId);
        _selectedBodyLoadCts = new CancellationTokenSource();
        _ = ObserveSelectedBodyLoadAsync(item, requestId, _selectedBodyLoadCts.Token);
    }

    private async Task ObserveSelectedBodyLoadAsync(AnalyzedItem item, long requestId, CancellationToken ct)
    {
        try
        {
            await LoadSelectedEmailBodyAsync(item, requestId, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Selected body load task failed: {ExceptionType}.", ex.GetType().Name);
            HandleUnexpectedCommandException(ex);
        }
    }

    private async Task LoadSelectedEmailBodyAsync(AnalyzedItem item, long requestId, CancellationToken ct)
    {
        if (!IsCurrentSelectedBodyLoad(requestId, item))
        {
            return;
        }

        IsSelectedBodyLoadInProgress = true;
        StatusMessage = LocalizedStrings.Get("Str.Status.LoadingBody", "Loading body.");

        try
        {
            var result = await _workflow.LoadSelectedBodyAsync(item, ct).ConfigureAwait(true);
            if (!IsCurrentSelectedBodyLoad(requestId, item))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                StatusMessage = result.StatusMessage;
            }

            if (result.Outcome == WorkflowActionOutcome.Success && !_isLiveSortingEnabled)
            {
                EmailsView.Refresh();
            }
        }
        finally
        {
            if (IsCurrentSelectedBodyLoad(requestId, item))
            {
                IsSelectedBodyLoadInProgress = false;
            }
        }
    }

    private async Task GenerateDigestAsync(CancellationToken ct = default)
    {
        IsDigestGenerationInProgress = true;
        StatusMessage = LocalizedStrings.Get("Str.Status.DigestGenerating", "Generating digest.");

        try
        {
            var result = await _workflow
                .GenerateDigestAsync(Emails.ToList(), _prefetchTask, TeamsUserEmail, ct)
                .ConfigureAwait(true);
            UpdateStatusFromOutcome(result.Outcome, result.StatusMessage);
        }
        finally
        {
            IsDigestGenerationInProgress = false;
        }
    }

    private async Task ReplyAsync(CancellationToken ct = default)
    {
        IsReplyDraftCreationInProgress = true;
        try
        {
            var result = await _workflow
                .ReplyAsync(SelectedEmail, SelectedTemplate, TemplateFieldInputs.ToList(), ct)
                .ConfigureAwait(true);
            UpdateStatusFromOutcome(result.Outcome, result.StatusMessage);
        }
        finally
        {
            IsReplyDraftCreationInProgress = false;
        }
    }

    private async Task OpenInOutlookAsync(CancellationToken ct = default)
    {
        SetOpenInOutlookInProgress(true);

        try
        {
            var result = await _workflow.OpenInOutlookAsync(SelectedEmail, ct).ConfigureAwait(true);
            UpdateStatusFromOutcome(result.Outcome, result.StatusMessage);
        }
        finally
        {
            SetOpenInOutlookInProgress(false);
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
            _clipboardSecurityHelper.SecureCopy(SelectedEmail.RedactedContent.Summary);
            StatusMessage = LocalizedStrings.Get(
                "Str.Status.CopiedSummary",
                "Redacted summary copied to clipboard (auto-clear in 30s).");
        }
        catch
        {
            StatusMessage = LocalizedStrings.Get(
                "Str.Status.CopyFailed",
                "Clipboard copy failed.");
            _dialogService.ShowError(
                StatusMessage,
                LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
        }
    }

    private void RebuildTemplateFieldInputs()
    {
        TemplateFieldInputs.Clear();
        if (SelectedTemplate is null)
        {
            return;
        }

        var inputs = _createReplyDraftWorkflow.BuildTemplateFieldInputs(SelectedTemplate, SelectedEmail);
        foreach (var input in inputs)
        {
            TemplateFieldInputs.Add(input);
        }
    }

    private void OnAutoRefreshStateChanged(object? sender, EventArgs e)
        => SynchronizeAutoRefreshState();

    private void SynchronizeAutoRefreshState()
    {
        AutoRefreshPaused = _autoRefreshController.IsPaused;
        NextAutoRefreshAt = _autoRefreshController.NextRunAt;
        AutoRefreshStatusText = _autoRefreshController.StatusText;
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

    private void HandleUnexpectedCommandException(Exception ex)
    {
        _logger.LogError("MainViewModel command failed unexpectedly: {ExceptionType}.", ex.GetType().Name);

        if (ex is OperationCanceledException)
        {
            StatusMessage = LocalizedStrings.Get("Str.Status.OperationCanceled", "Operation canceled.");
            return;
        }

        var message = LocalizedStrings.Get(
            "Str.Dialog.UnhandledExceptionMessage",
            "Unexpected error occurred. Check Outlook status and retry.");
        StatusMessage = message;
        _dialogService.ShowError(
            message,
            LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
    }

    private void UpdateStatusFromOutcome(WorkflowActionOutcome outcome, string? statusMessage)
    {
        if (outcome == WorkflowActionOutcome.Skipped || string.IsNullOrWhiteSpace(statusMessage))
        {
            return;
        }

        StatusMessage = statusMessage;
    }

    private bool IsCurrentSelectedBodyLoad(long requestId, AnalyzedItem item)
        => requestId == Volatile.Read(ref _selectedBodyLoadRequestId) && ReferenceEquals(SelectedEmail, item);

    private void CancelSelectedBodyLoad()
    {
        var cts = _selectedBodyLoadCts;
        _selectedBodyLoadCts = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
            // Ignore cancellation races.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void SetOpenInOutlookInProgress(bool value)
    {
        if (_isOpenInOutlookInProgress == value)
        {
            return;
        }

        _isOpenInOutlookInProgress = value;
        OnPropertyChanged(nameof(IsLoading));
        CommandManager.InvalidateRequerySuggested();
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

    private bool SetBusyFlag(ref bool storage, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref storage, value, propertyName))
        {
            return false;
        }

        OnPropertyChanged(nameof(IsLoading));
        CommandManager.InvalidateRequerySuggested();
        return true;
    }
}
