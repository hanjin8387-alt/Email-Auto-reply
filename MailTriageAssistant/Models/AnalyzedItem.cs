using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MailTriageAssistant.Models;

public sealed class AnalyzedItem : INotifyPropertyChanged
{
    private EmailCategory _category;
    private int _score;
    private string _redactedSender = string.Empty;
    private string _redactedSubject = string.Empty;
    private string _redactedSummary = string.Empty;
    private string _actionHint = string.Empty;
    private string[] _tags = Array.Empty<string>();
    private bool _isBodyLoaded;

    public string EntryId { get; init; } = string.Empty;

    public string Sender { get; init; } = string.Empty;
    public string SenderEmail { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;
    public DateTime ReceivedTime { get; init; }
    public bool HasAttachments { get; init; }

    public string RedactedSender
    {
        get => _redactedSender;
        set => SetProperty(ref _redactedSender, value);
    }

    public string RedactedSubject
    {
        get => _redactedSubject;
        set => SetProperty(ref _redactedSubject, value);
    }

    public EmailCategory Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public string RedactedSummary
    {
        get => _redactedSummary;
        set => SetProperty(ref _redactedSummary, value);
    }

    public string ActionHint
    {
        get => _actionHint;
        set => SetProperty(ref _actionHint, value);
    }

    public string[] Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value ?? Array.Empty<string>());
    }

    public bool IsBodyLoaded
    {
        get => _isBodyLoaded;
        set => SetProperty(ref _isBodyLoaded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
