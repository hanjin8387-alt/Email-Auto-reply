using System.Diagnostics;

namespace MailTriageAssistant.Services;

public sealed class ShellExternalLauncher : IExternalLauncher
{
    public bool TryLaunch(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
