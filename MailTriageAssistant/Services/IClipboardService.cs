namespace MailTriageAssistant.Services;

public interface IClipboardService
{
    void SetText(string text);
    bool ContainsText();
    string GetText();
    void Clear();
    uint GetSequenceNumber();
}
