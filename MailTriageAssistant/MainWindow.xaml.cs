using System;
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
    }
}
