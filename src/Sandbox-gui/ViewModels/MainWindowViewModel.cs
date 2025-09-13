using System;
using AvaloniaMpv;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sandbox_gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MpvPlayer Player { get; set; } = new();

    public MainWindowViewModel()
    {
    }

    public void Load()
    {
        Player.RegisterEvent<double>("time-pos", MpvFormat.MPV_FORMAT_DOUBLE);
        Player.RegisterEvent<int>("pause", MpvFormat.MPV_FORMAT_FLAG);
        Player.GetEvent<double>("time-pos").Raised += (s, e) =>
        {
            Console.WriteLine($"Playback at {e.Value}");
        };
        Player.GetEvent<int>("pause").Raised += (s, e) =>
        {
            Console.WriteLine($"Pause statuc chaneg to {e.Value}");
        };
        Player.StartPlayback("/home/noble/Videos/Superman.2025.1080p.WebDl.English.Msubs.MoviesMod.cafe.mkv");
    }

    public void Seek()
    {
        Player.SeekTo(1000);
    }

    public void TogglePlayPause()
    {
        Player.TogglePlayPause();
    }
    public void Stop()
    {
        Player.Dispose();
        Player = new();
    }
}
