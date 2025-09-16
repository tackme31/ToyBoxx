using System.Text;
using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ToyBoxx.ViewModels;

public partial class RootViewModel : ObservableObject
{
    public RootViewModel()
    {
        Controller = new ControllerViewModel(this);
    }

    public ControllerViewModel Controller { get; }

    public AppCommands Commands { get; } = new AppCommands();

    private MediaElement? _mediaElement;
    public MediaElement MediaElement => _mediaElement ??= (Application.Current.MainWindow as MainWindow)?.Media ?? throw new Exception("Media element not found.");

    [ObservableProperty]
    private bool _isApplicationLoaded;

    [ObservableProperty]
    private string? _windowTitle;

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
