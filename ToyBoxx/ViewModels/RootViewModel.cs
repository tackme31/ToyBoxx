using System.IO;
using System.Windows;
using Unosquare.FFME;
using Unosquare.FFME.Common;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace ToyBoxx.ViewModels;

public class RootViewModel : ViewModelBase
{
    public RootViewModel()
    {
        Controller = new ControllerViewModel(this);
    }

    public ControllerViewModel Controller { get; }

    internal void OnApplicationLoaded()
    {
        if (IsApplicationLoaded)
        {
            return;
        }

        Controller.OnApplicationLoaded();

        IsApplicationLoaded = true;
    }

    public AppCommands Commands { get; } = new AppCommands();

    private MediaElement? _mediaElement;
    public MediaElement MediaElement => _mediaElement ??= (Application.Current.MainWindow as MainWindow)?.Media ?? throw new Exception("Media element not found.");

    private bool _isApplicationLoaded;
    public bool IsApplicationLoaded
    {
        get => _isApplicationLoaded;
        set => SetProperty(ref _isApplicationLoaded, value);
    }
}
