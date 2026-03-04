using System.Runtime.InteropServices;
using System.Windows;

namespace MailTriageAssistant.Services;

public sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        var dataObj = new DataObject();
        dataObj.SetData(DataFormats.UnicodeText, text);
        dataObj.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
        Clipboard.SetDataObject(dataObj, false);
    }

    public bool ContainsText() => Clipboard.ContainsText();

    public string GetText() => Clipboard.GetText();

    public void Clear() => Clipboard.Clear();

    public uint GetSequenceNumber() => GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
