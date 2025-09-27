using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using ToyBoxx.Foundation;
using ToyBoxx.Services;
using Unosquare.FFME;

namespace ToyBoxx.ViewModels;

public partial class RootViewModel : ObservableObject
{
    private readonly IMediaElementProvider _mediaElementProvider;
    public RootViewModel(IMediaElementProvider mediaElementProvider)
    {
        _mediaElementProvider = mediaElementProvider;
        Controller = new ControllerViewModel(this);
        Commands = new AppCommands(this);
    }

    public ControllerViewModel Controller { get; }

    public AppCommands Commands { get; }

    public event Action? RequestToggleFullScreen;

    private MediaElement? _mediaElement;
    public MediaElement MediaElement => _mediaElement ??= _mediaElementProvider.GetMediaElement("main");

    private MediaElement? _previewMediaElement;
    public MediaElement PreviewMediaElement => _previewMediaElement ??= _mediaElementProvider.GetMediaElement("preview");

    [ObservableProperty]
    private bool _isApplicationLoaded;

    [ObservableProperty]
    private string? _windowTitle;

    [ObservableProperty]
    private double _scale = 1.0;

    [ObservableProperty]
    private double _scaleCenterX;

    [ObservableProperty]
    private double _scaleCenterY;

    [ObservableProperty]
    private double _transformX = 0.0;

    [ObservableProperty]
    private double _transformY = 0.0;

    [ObservableProperty]
    private double _angle = 0.0;

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
            var title = Path.GetFileNameWithoutExtension(uri.LocalPath);
            if (title.Length > 64)
            {
                title = string.Concat(title.AsSpan(0, 64), "...");
            }

            titleBuilder.Append(title);
            titleBuilder.Append(" - ");
        }

        titleBuilder.Append("ToyBoxx");

        WindowTitle = titleBuilder.ToString();
    }

    private DelegateCommand? _openFromDropCommand;
    public DelegateCommand OpenFileCommand => _openFromDropCommand ??= new(async param =>
    {
        var filePath = param switch
        {
            DragEventArgs args => GetDragEventFile(args),
            RoutedEventArgs args => GetRoutedEventFile(),
            _ => null
        };

        if (filePath is not null && File.Exists(filePath))
        {
            await Commands.Open.ExecuteAsync(filePath);
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

    private DelegateCommand? _toggleFullScreenCommand;
    public DelegateCommand ToggleFullScreen => _toggleFullScreenCommand ??= new(_ =>
    {
        RequestToggleFullScreen?.Invoke();
        return Task.CompletedTask;
    });
}
