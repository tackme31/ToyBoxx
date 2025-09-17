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

            var media = App.ViewModel.MediaElement;
            if (media.IsOpen)
            {
                await media.Close();
            }

            var previewMedia = App.ViewModel.PreviewMediaElement;
            if (previewMedia.IsOpen)
            {
                await previewMedia.Close();
            }

            var target = new Uri(uriString);
            await media.Open(new FileInputStream(target.LocalPath));

            await previewMedia.Open(new FileInputStream(@target.LocalPath));
            await previewMedia.Stop();
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
        await App.ViewModel.MediaElement.Close();
        await App.ViewModel.PreviewMediaElement.Close();
    });

    private DelegateCommand? _pauseCommand;

    public DelegateCommand Pause => _pauseCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Pause();
    });

    private DelegateCommand? _playCommand;

    public DelegateCommand Play => _playCommand ??= new(async o =>
    {
        if (App.ViewModel.MediaElement.HasMediaEnded)
        {
            await App.ViewModel.MediaElement.Seek(TimeSpan.Zero);
        }

        await App.ViewModel.MediaElement.Play();
    });

    private DelegateCommand? _stopCommand;
    public DelegateCommand Stop => _stopCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Stop();
        await App.ViewModel.MediaElement.Seek(TimeSpan.Zero);
    });

    private DelegateCommand? _stepForwardCommand;
    public DelegateCommand StepForwardCommand => _stepForwardCommand ??= new(async o =>
    {
        await App.ViewModel.MediaElement.Pause();
        await App.ViewModel.MediaElement.StepForward();
    });

    private DelegateCommand? _toggleFullScreenCommand;
    public DelegateCommand ToggleFullScreen => _toggleFullScreenCommand ??= new(o =>
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow.WindowState == WindowState.Maximized)
        {
            _previousWindowStatus.ApplyState(mainWindow);
            WindowStatus.EnableDisplayTimeout();
        }
        else
        {
            _previousWindowStatus.CaptureState(mainWindow);
            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.WindowState = WindowState.Maximized;

            WindowStatus.DisableDisplayTimeout();
        }
    });

    private DelegateCommand? _setSegmentLoop;
    public DelegateCommand SetSegmentLoop => _setSegmentLoop ??= new(o =>
    {
        var controller = App.ViewModel.Controller;

        if (controller.IsSegmentLoopEnabled &&
            controller.SegmentLoopFrom is not null &&
            controller.SegmentLoopTo is not null)
        {
            controller.IsSegmentLoopEnabled = false;
            controller.SegmentLoopFrom = null;
            controller.SegmentLoopTo = null;
            return;
        }

        var currentPosition = App.ViewModel.MediaElement.Position;
        if (controller.SegmentLoopFrom is null)
        {
            controller.IsSegmentLoopEnabled = false;
            controller.SegmentLoopFrom = currentPosition;
            controller.SegmentLoopTo = null;
            return;
        }

        if (controller.SegmentLoopFrom >= currentPosition)
        {
            return;
        }

        controller.SegmentLoopTo = currentPosition;
        controller.IsSegmentLoopEnabled = true;
    });

    private DelegateCommand? _changeSpeedRatioComand;
    public DelegateCommand ChangeSpeedRatio => _changeSpeedRatioComand ??= new(o =>
    {
        if (double.TryParse(o?.ToString(), out var ratio) && ratio <= 0)
        {
            return;
        }

        App.ViewModel.MediaElement.SpeedRatio = ratio;
    });
}
