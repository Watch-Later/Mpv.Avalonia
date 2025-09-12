using CommunityToolkit.Mvvm.ComponentModel;

namespace Sandbox_gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    
}
