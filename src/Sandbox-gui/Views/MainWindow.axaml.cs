using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaMpv;

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

    private void TogglePlayPause(object? sender, RoutedEventArgs e)
    {
        mediaCtrl.TogglePlayPause();
    }

    private void GetPause(object? sender, RoutedEventArgs e)
    {
        //LSP is borked which is why i'm doing this
        var mediaCtrlS = (MediaControl)mediaCtrl;
        var paused = mediaCtrlS.MpvGetProperty<int>("pause", MpvFormat.MPV_FORMAT_FLAG);
        Console.WriteLine(paused);
    }


    private void Seek(object? sender, RoutedEventArgs e)
    {
        //LSP is borked which is why i'm doing this
        var mediaCtrlS = (MediaControl)mediaCtrl;
        mediaCtrlS.SeekTo(900);
    }
}
