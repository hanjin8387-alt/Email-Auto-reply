namespace MailTriageAssistant.Services;

public interface IDigestDeliveryService
{
    void CopyDigestToClipboard(string digest);

    bool TryOpenTeams(string? userEmail = null);
}
