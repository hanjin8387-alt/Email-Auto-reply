using System.Windows;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;

namespace MailTriageAssistant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var redaction = new RedactionService();
        var clipboard = new ClipboardSecurityHelper(redaction);

        var outlook = new OutlookService();
        var triage = new TriageService();
        var template = new TemplateService();
        var digest = new DigestService(clipboard, redaction);

        DataContext = new MainViewModel(
            outlook,
            redaction,
            clipboard,
            triage,
            digest,
            template);
    }
}

