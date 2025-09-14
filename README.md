Media Player for Avalonia based on MPV + OpenGL.
See the sandbox project for a usage example.


https://github.com/user-attachments/assets/7f532897-75b2-41d1-abfd-50b80c4f3a5c


# Benefits
1. Does not use NativeControlHost, draws with OpenGl instead so more flexible. Works in both Windows and usercontrols with ease and can be drawn over.
2. MPV is lighter than VLC. Smaller app distribution.

# Usage
Basic usage is as follows:
In Xaml
```
<Window
  ...
  xmlns:mv="using:AvaloniaMpv"
  ..>
  <mv:MediaControl
    Height="400"
    Width="1000"
    MpvPlayer="{Binding MpvPlayer}"
    />
    ...
```
And in your viewmodel:
```
public partial class MainViewModel {
  public MpvPlayer Player {get;} = new()
}
```
It's important to make sure the binding is provided as the control is created inorder to properly initialise the OpenGL stuff.

After this it's just a matter of playing some media:

```
public void Play() {
  Player.StartPlayback("/path/to/media.mp4")
}
```
# Controlling
Controlling is done through mpv commands. A safe wrapper is provided:
```
//toggle pause
string[] command = { "seek", $"{ms}", "absolute" };
Player.MpvCommand(command)

//seek
string[] command = {"seek" , "10" , "absolute"};
Player.MpvCommand(command)
```
Consult the MPV documentation for more commands

# Getting state
You can request state using mpv properties. A wrapper is provided for this to:
```
//get paused
int paused = Player.MpvGetProperty<int>("paused",MpvFormat.MPV_FORMAT_FLAG)

//get time stamp
double timestamp = Player.MpvGetProperty<double>("time-pos",MpvFormat.MPV_FORMAT_DOUBLE)
```
Consult the MPV documentation for more properties.

# Polling properties
You can also poll properties and recieve notifications when they change:
```
//poll duration
Player.RegisterEvent<double>("duration", MpvFormat.MPV_FORMAT_DOUBLE);
//poll time stamp
Player.RegisterEvent<double>("time-pos",MpvFormat.MPV_FORMAT_DOUBLE);

//Listen for changes
Player.GetEvent<double>("duration").Raised += (s,e)=>{
  Console.WriteLine($"Duration is {e.Value}");
}
Player.GetEvent<double>("time-pos").Raised += (s,e)=>{
  Console.WriteLine($"Playback at {e.Value}");
}
```
