using System;
using System.Threading.Tasks;

namespace MailTriageAssistant.Helpers;

public static class TaskExtensions
{
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            onException?.Invoke(ex);
        }
    }
}

