using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME;

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

    [ObservableProperty]
    private double _scaleX = 1.0;

    [ObservableProperty]
    private double _scaleY = 1.0;

    [ObservableProperty]
    private double _scaleCenterX;

    [ObservableProperty]
    private double _scaleCenterY;

    [ObservableProperty]
    private double _transformX = 0.0;

    [ObservableProperty]
    private double _transformY = 0.0;

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
            var fileName = uri.Segments.LastOrDefault() ?? "No title";

            if (fileName.Length > 64)
            {
                fileName = fileName.Substring(0, 64) + "...";
            }

            titleBuilder.Append(fileName);
            titleBuilder.Append(" - ");
        }

        titleBuilder.Append("ToyBoxx");

        WindowTitle = titleBuilder.ToString();
    }

    private DelegateCommand? _openFromDropCommand;
    public DelegateCommand OpenFileCommand => _openFromDropCommand ??= new (param =>
    {
        var filePath = param switch
        {
            DragEventArgs args => GetDragEventFile(args),
            RoutedEventArgs args => GetRoutedEventFile(),
            _ => null
        };

        if (filePath is not null && File.Exists(filePath))
        {
            App.ViewModel.Commands.Open.Execute(filePath);
        }

        string? GetDragEventFile(DragEventArgs args)
        {
            if (!args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            var files = (string[])args.Data.GetData(DataFormats.FileDrop);
            return files is [] ? null : files[0];
        }

        string? GetRoutedEventFile()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Length < 2 ? null : args[1].Trim();
        }
    });
}
