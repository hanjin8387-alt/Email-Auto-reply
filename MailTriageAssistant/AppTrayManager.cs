using Hardcodet.Wpf.TaskbarNotification;
using MailTriageAssistant.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MailTriageAssistant;

public sealed class AppTrayManager : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private PropertyChangedEventHandler? _statusChangedHandler;

    public bool IsInitialized => _trayIcon is not null;

    public bool TryInitialize(MainWindow mainWindow, MainViewModel viewModel, bool enabled, Action exitApplication)
    {
        if (!enabled || _trayIcon is not null)
        {
            return _trayIcon is not null;
        }

        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(exitApplication);

        var statusItem = new MenuItem { IsEnabled = false };
        void UpdateStatus()
        {
            statusItem.Header = AppLocalizationManager.GetResourceString(viewModel.IsLoading
                ? "Str.Tray.StatusProcessing"
                : "Str.Tray.StatusIdle");
        }

        UpdateStatus();
        _statusChangedHandler = (_, args) =>
        {
            if (!string.Equals(args.PropertyName, nameof(MainViewModel.IsLoading), StringComparison.Ordinal))
            {
                return;
            }

            mainWindow.Dispatcher.Invoke(UpdateStatus);
        };

        viewModel.PropertyChanged += _statusChangedHandler;
        _viewModel = viewModel;

        var menu = new ContextMenu();
        menu.Items.Add(statusItem);
        menu.Items.Add(new Separator());

        var runTriage = new MenuItem { Header = AppLocalizationManager.GetResourceString("Str.Button.LoadEmails") };
        runTriage.Click += (_, _) => TryExecuteCommand(viewModel.LoadEmailsCommand);

        var copyDigest = new MenuItem { Header = AppLocalizationManager.GetResourceString("Str.Button.GenerateDigestTeams") };
        copyDigest.Click += (_, _) => TryExecuteCommand(viewModel.GenerateDigestCommand);

        var open = new MenuItem { Header = AppLocalizationManager.GetResourceString("Str.Tray.OpenDashboard") };
        open.Click += (_, _) => ShowMainWindow(mainWindow);

        var exit = new MenuItem { Header = AppLocalizationManager.GetResourceString("Str.Tray.Exit") };
        exit.Click += (_, _) => exitApplication();

        menu.Items.Add(runTriage);
        menu.Items.Add(copyDigest);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _trayIcon = new TaskbarIcon
        {
            Icon = SystemIcons.Application,
            ToolTipText = AppLocalizationManager.GetResourceString("Str.MainWindow.Title"),
            ContextMenu = menu,
            Visibility = Visibility.Visible,
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow(mainWindow);
        return true;
    }

    public void Dispose()
    {
        if (_viewModel is not null && _statusChangedHandler is not null)
        {
            _viewModel.PropertyChanged -= _statusChangedHandler;
        }

        _statusChangedHandler = null;
        _viewModel = null;

        try
        {
            _trayIcon?.Dispose();
        }
        catch
        {
            // Ignore tray disposal issues.
        }

        _trayIcon = null;
    }

    private static void TryExecuteCommand(ICommand command)
    {
        try
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
        catch
        {
            // Ignore command failures from tray.
        }
    }

    private static void ShowMainWindow(MainWindow mainWindow)
    {
        try
        {
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }

            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Activate();
        }
        catch
        {
            // Ignore window show failures.
        }
    }
}
