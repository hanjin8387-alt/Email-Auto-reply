using System;
using System.ComponentModel;
using System.Windows;
using MailTriageAssistant.ViewModels;

namespace MailTriageAssistant;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        Closing += OnClosingToTray;
    }

    private void OnClosingToTray(object? sender, CancelEventArgs e)
    {
        if (App.IsExitRequested || !App.IsSystemTrayEnabled)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
