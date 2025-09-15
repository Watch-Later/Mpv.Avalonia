using System;
using System.IO;
using AvaloniaMpv;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Sandbox_gui.ViewModels;
public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    public MpvPlayer Player { get; } = new();
    private bool _updatingPos = false;
    private double _currentPos = 0;
    private bool _registeredEvents = false;

    [ObservableProperty]
    public double duration = 0;

    public double CurrentPos
    {
        get => _currentPos;
        set
        {
            if (!_updatingPos)
            {
                Player.SeekTo(value);
            }
            else
            {
                SetProperty(ref _currentPos, value);
            }
        }
    }

    public void TogglePlayPause() => Player.TogglePlayPause();
    public void Start()
    {
        if (!_registeredEvents)
        {
            Player.RegisterEvent<double>("duration", MpvFormat.MPV_FORMAT_DOUBLE);
            Player.RegisterEvent<double>("time-pos", MpvFormat.MPV_FORMAT_DOUBLE);
            _registeredEvents = true;
        }
        // Player.StartPlayback(Path.Join(AppContext.BaseDirectory, "stock-video.mp4"));
        Player.StartPlayback("/home/noble/Downloads/JUJUTSU KAISEN Opening ｜ Kaikai Kitan by Eve.webm"); //requires yt-dlp
        Player.GetEvent<double>("duration").Raised += (s, e) =>
        {
            Duration = e.Value;
        };
        Player.GetEvent<double>("time-pos").Raised += (s, e) =>
        {
            _updatingPos = true;
            CurrentPos = e.Value;
            _updatingPos = false;
        };
    }
}
