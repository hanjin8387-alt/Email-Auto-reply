using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MailTriageAssistant.Services;
using System;
using System.Windows.Threading;

namespace MailTriageAssistant;

public static class AppExceptionPolicy
{
    public static void HandleDispatcherUnhandledException(
        IServiceProvider? services,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        try
        {
            var logger = services?.GetService<ILogger<App>>();
            var ex = e.Exception;
            if (logger is not null && ex is not null)
            {
                logger.LogError(
                    "Unhandled exception: {ExceptionType} (HResult={HResult}).",
                    ex.GetType().Name,
                    ex.HResult);
            }
        }
        catch
        {
            // Ignore logging failures inside exception handler.
        }

        var dialog = services?.GetService<IDialogService>() ?? new WpfDialogService();
        dialog.ShowError(
            AppLocalizationManager.GetResourceString("Str.Dialog.UnhandledExceptionMessage"),
            AppLocalizationManager.GetResourceString("Str.MainWindow.Title"));
    }
}
