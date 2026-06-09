using System.Windows;
using ElectroScroll.Models;
using ElectroScroll.Services;
using ElectroScroll.ViewModels;

namespace ElectroScroll;

public partial class MainWindow : Window
{
    public MainWindow(
        AppSettings settings,
        ScrollController controller,
        SettingsStore store,
        PerformanceModeStatus performanceModeStatus)
    {
        InitializeComponent();
        DataContext = new MainViewModel(settings, controller, store, performanceModeStatus);
    }

    public bool AllowClose { get; set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
