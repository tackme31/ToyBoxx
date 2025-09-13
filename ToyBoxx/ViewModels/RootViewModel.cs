using System.Windows;
using Unosquare.FFME;
using Unosquare.FFME.Common;

namespace ToyBoxx.ViewModels;

public class RootViewModel : ViewModelBase
{
    internal void OnApplicationLoaded()
    {
        if (IsApplicationLoaded)
        {
            return;
        }

        IsApplicationLoaded = true;
    }

    public AppCommands Commands { get; } = new AppCommands();

    public Lazy<MediaElement> MediaElement => new(() => (Application.Current.MainWindow as MainWindow)?.Media ?? throw new Exception("Media element does not found."));

    private bool _isApplicationLoaded;
    public bool IsApplicationLoaded
    {
        get => _isApplicationLoaded;
        set => SetProperty(ref _isApplicationLoaded, value);
    }
}
