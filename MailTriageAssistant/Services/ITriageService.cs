namespace MailTriageAssistant.Services;

public interface ITriageService
{
    TriageService.TriageResult AnalyzeHeader(string sender, string subject);

    TriageService.TriageResult AnalyzeWithBody(string sender, string subject, string body);
}

