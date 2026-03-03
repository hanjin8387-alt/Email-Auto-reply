using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MailTriageAssistant.Models;

public sealed class ReplyTemplateFieldInput : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public ReplyTemplateFieldInput(
        string key,
        string label,
        bool isRequired,
        string placeholder = "",
        string value = "")
    {
        Key = key ?? string.Empty;
        Label = string.IsNullOrWhiteSpace(label) ? Key : label;
        IsRequired = isRequired;
        Placeholder = placeholder ?? string.Empty;
        _value = value ?? string.Empty;
    }

    public string Key { get; }
    public string Label { get; }
    public bool IsRequired { get; }
    public string Placeholder { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value ?? string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
