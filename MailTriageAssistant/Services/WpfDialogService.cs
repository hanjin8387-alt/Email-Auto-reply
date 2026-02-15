using System.Windows;

namespace MailTriageAssistant.Services;

public sealed class WpfDialogService : IDialogService
{
    public void ShowInfo(string message, string title)
        => MessageBox.Show(message ?? string.Empty, title ?? string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title)
        => MessageBox.Show(message ?? string.Empty, title ?? string.Empty, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title)
        => MessageBox.Show(message ?? string.Empty, title ?? string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
}

