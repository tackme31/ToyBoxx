using System.Text;
using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME;
using Unosquare.FFME.Common;

namespace ToyBoxx.ViewModels;

public class RootViewModel : ViewModelBase
{
    public RootViewModel()
    {
        Controller = new ControllerViewModel(this);
    }

    public ControllerViewModel Controller { get; }

    public AppCommands Commands { get; } = new AppCommands();

    private MediaElement? _mediaElement;
    public MediaElement MediaElement => _mediaElement ??= (Application.Current.MainWindow as MainWindow)?.Media ?? throw new Exception("Media element not found.");

    private bool _isApplicationLoaded;
    public bool IsApplicationLoaded
    {
        get => _isApplicationLoaded;
        set => SetProperty(ref _isApplicationLoaded, value);
    }

    private string _windowTitle;
    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    internal void OnApplicationLoaded()
    {
        if (IsApplicationLoaded)
        {
            return;
        }

        Controller.OnApplicationLoaded();

        var m = MediaElement;
        MediaElement.WhenChanged(UpdateWindowTitle,
            nameof(m.IsOpen),
            nameof(m.IsOpening),
            nameof(m.Source));

        IsApplicationLoaded = true;
    }

    private void UpdateWindowTitle()
    {
        var titleBuilder = new StringBuilder();
        if (MediaElement.IsOpen)
        {
            var source = MediaElement.MediaInfo.MediaSource;
            var uri = new Uri(source);
            var fileName = uri.Segments.LastOrDefault();

            titleBuilder.Append(fileName ?? "No title");
            titleBuilder.Append(" - ");
        }

        titleBuilder.Append("ToyBoxx");

        WindowTitle = titleBuilder.ToString();
    }
}
