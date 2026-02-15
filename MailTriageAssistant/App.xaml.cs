using System.Windows;
using System.Windows.Threading;

namespace MailTriageAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Never surface raw exception messages that could contain email content.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            "예기치 않은 오류가 발생했습니다. Outlook 상태를 확인한 뒤 다시 시도해 주세요.",
            "MailTriageAssistant",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

