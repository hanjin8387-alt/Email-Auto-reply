namespace MailTriageAssistant.Services;

public interface IOutlookCapabilityDetector
{
    OutlookCapabilitySnapshot GetSnapshot();

    void Invalidate();
}
