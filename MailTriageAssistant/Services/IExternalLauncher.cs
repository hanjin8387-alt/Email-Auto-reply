namespace MailTriageAssistant.Services;

public interface IExternalLauncher
{
    bool TryLaunch(string uri);
}
