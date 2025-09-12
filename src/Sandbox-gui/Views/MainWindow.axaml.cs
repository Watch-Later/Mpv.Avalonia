using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sandbox_gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void LoadVideo(object? sender, RoutedEventArgs e)
    {
        mediaCtrl.Source = "/home/noble/Videos/Superman.2025.1080p.WebDl.English.Msubs.MoviesMod.cafe.mkv";
    }

    private void TogglePlayPause(object? sender, RoutedEventArgs e) {
        mediaCtrl.TogglePlayPause();
    }
}
