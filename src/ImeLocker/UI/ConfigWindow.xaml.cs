namespace ImeLocker.UI;

using System.ComponentModel;
using System.Windows;
using ImeLocker.Config;

/// <summary>
/// Code-behind for ConfigWindow. Sets up ViewModel and handles window lifecycle.
/// </summary>
public partial class ConfigWindow : Window
{
    public ConfigWindow(ConfigManager configManager)
    {
        InitializeComponent();
        var vm = new ConfigViewModel(configManager);
        vm.SaveCompleted += Hide;
        DataContext = vm;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Hide instead of close so it can be re-shown from tray
        e.Cancel = true;
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
