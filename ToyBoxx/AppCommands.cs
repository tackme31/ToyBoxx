using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME;

namespace ToyBoxx;

public class AppCommands
{
    private readonly WindowStatus _previousWindowStatus = new();

    private DelegateCommand? _openCommand;
    public DelegateCommand Open => _openCommand ??= new(async arg =>
    {
        try
        {
            var uriString = arg as string;
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return;
            }

            var media = App.ViewModel.MediaElement.Value;
            var target = new Uri(uriString);
            await media.Open(new FileInputStream(target.LocalPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                $"Media Failed: {ex.GetType()}\r\n{ex.Message}",
                $"{nameof(MediaElement)} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }
    });

    private DelegateCommand? _closeCommand;
    public DelegateCommand Close => _closeCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Value.Close();
    });

    private DelegateCommand? _pauseCommand;

    public DelegateCommand Pause => _pauseCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Value.Pause();
    });

    private DelegateCommand? _playCommand;

    public DelegateCommand Play => _playCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Value.Play();
    });

    private DelegateCommand? _stopCommand;
    public DelegateCommand Stop => _stopCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Value.Stop();
        await App.ViewModel.MediaElement.Value.Seek(TimeSpan.Zero);
    });

    private DelegateCommand? _toggleFullScreenCommand;
    public DelegateCommand ToggleFullscreen => _toggleFullScreenCommand ??= new(o =>
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow.WindowStyle == WindowStyle.None)
        {
            _previousWindowStatus.ApplyState(mainWindow);
            WindowStatus.EnableDisplayTimeout();
        }
        else
        {
            _previousWindowStatus.CaptureState(mainWindow);
            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.Topmost = true;
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.WindowState = WindowState.Maximized;
            WindowStatus.DisableDisplayTimeout();
        }
    });
}
