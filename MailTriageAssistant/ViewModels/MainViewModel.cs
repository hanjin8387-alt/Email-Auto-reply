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
    private bool _isLoading;
    private string _teamsUserEmail = string.Empty;
    private bool _autoRefreshPaused;
    private DateTimeOffset? _nextAutoRefreshAt;
    private string _autoRefreshStatusText = string.Empty;
    private Task _prefetchTask = Task.CompletedTask;
    private bool _isLiveSortingEnabled;

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

        LoadEmailsCommand = new AsyncRelayCommand(() => LoadEmailsAsync(), () => !IsLoading);
        GenerateDigestCommand = new AsyncRelayCommand(() => GenerateDigestAsync(), () => !IsLoading && Emails.Count > 0);
        ReplyCommand = new AsyncRelayCommand(() => ReplyAsync(), () => !IsLoading && SelectedEmail is not null && SelectedTemplate is not null);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEmail is not null);
        OpenInOutlookCommand = new AsyncRelayCommand(() => OpenInOutlookAsync(), () => !IsLoading && SelectedEmail is not null);

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
                LoadSelectedEmailBodyAsync(value).SafeFireAndForget(ex =>
                {
                    _logger.LogDebug("Selected body load skipped due to {ExceptionType}.", ex.GetType().Name);
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
        IsLoading = true;
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
            IsLoading = false;
        }
    }

    private async Task LoadSelectedEmailBodyAsync(AnalyzedItem? item, CancellationToken ct = default)
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
            StatusMessage = LocalizedStrings.Get("Str.Status.LoadingBody", "Loading body.");
            var result = await _workflow.LoadSelectedBodyAsync(item, ct).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                StatusMessage = result.StatusMessage;
            }

            if (result.Loaded && !_isLiveSortingEnabled)
            {
                EmailsView.Refresh();
            }
        }
        finally
        {
            if (setLoading)
            {
                IsLoading = false;
            }
        }
    }

    private async Task GenerateDigestAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        StatusMessage = LocalizedStrings.Get("Str.Status.DigestGenerating", "Generating digest.");

        try
        {
            var result = await _workflow
                .GenerateDigestAsync(Emails.ToList(), _prefetchTask, TeamsUserEmail, ct)
                .ConfigureAwait(true);
            if (result.Performed && !string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                StatusMessage = result.StatusMessage;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReplyAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var result = await _workflow
                .ReplyAsync(SelectedEmail, SelectedTemplate, TemplateFieldInputs.ToList(), ct)
                .ConfigureAwait(true);
            if (result.Performed && !string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                StatusMessage = result.StatusMessage;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OpenInOutlookAsync(CancellationToken ct = default)
    {
        var setLoading = !IsLoading;
        if (setLoading)
        {
            IsLoading = true;
        }

        try
        {
            var result = await _workflow.OpenInOutlookAsync(SelectedEmail, ct).ConfigureAwait(true);
            if (result.Performed && !string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                StatusMessage = result.StatusMessage;
            }
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
