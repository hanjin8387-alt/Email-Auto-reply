namespace MailTriageAssistant.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title);
    void ShowWarning(string message, string title);
    void ShowError(string message, string title);
}

