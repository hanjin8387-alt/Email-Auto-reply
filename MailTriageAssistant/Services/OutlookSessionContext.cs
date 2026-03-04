using Outlook = Microsoft.Office.Interop.Outlook;

namespace MailTriageAssistant.Services;

public sealed class OutlookSessionContext
{
    public OutlookSessionContext(Outlook.Application app, Outlook.NameSpace session)
    {
        App = app;
        Session = session;
    }

    public Outlook.Application App { get; }

    public Outlook.NameSpace Session { get; }
}
