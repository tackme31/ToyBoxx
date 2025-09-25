using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using ToyBoxx.Foundation;
using ToyBoxx.ViewModels;
using Unosquare.FFME;

namespace ToyBoxx;

public class AppCommands(RootViewModel viewModel)
{
    private DelegateCommand? _openCommand;

    private readonly Lock _captureSyncLock = new();
    private bool _isCaptureInProgress;

    public DelegateCommand Open => _openCommand ??= new(async param =>
    {
        try
        {
            var uriString = param as string;
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return;
            }

            var media = viewModel.MediaElement;
            if (media.IsOpen)
            {
                await media.Close();
            }

            var previewMedia = viewModel.PreviewMediaElement;
            if (previewMedia.IsOpen)
            {
                await previewMedia.Close();
            }

            var target = new Uri(uriString);
            await media.Open(target);

            // Pire-and-forget (preview loading is non-blocking)
            InitPreviewAsync(viewModel.PreviewMediaElement, target);
        }
        catch (Exception ex)
        {
            App.ShowSnackbar($"{nameof(MediaElement)} Error", $"Media Failed: {ex.GetType()}\r\n{ex.Message}");
        }
    });

    private async void InitPreviewAsync(MediaElement preview, Uri target)
    {
        try
        {
            await preview.Open(target);
            await preview.Stop();
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private DelegateCommand? _closeCommand;
    public DelegateCommand Close => _closeCommand ??= new(async _ =>
    {
        await viewModel.MediaElement.Close();
        await viewModel.PreviewMediaElement.Close();
    });

    private DelegateCommand? _pauseCommand;

    public DelegateCommand Pause => _pauseCommand ??= new(async _ =>
    {
        await viewModel.MediaElement.Pause();
    });

    private DelegateCommand? _playCommand;

    public DelegateCommand Play => _playCommand ??= new(async _ =>
    {
        if (viewModel.MediaElement.HasMediaEnded)
        {
            await viewModel.MediaElement.Seek(TimeSpan.Zero);
        }

        await viewModel.MediaElement.Play();
    });

    private DelegateCommand? _stopCommand;
    public DelegateCommand Stop => _stopCommand ??= new(async _ =>
    {
        await viewModel.MediaElement.Stop();
        await viewModel.MediaElement.Seek(TimeSpan.Zero);
    });

    private DelegateCommand? _stepForwardCommand;
    public DelegateCommand StepForwardCommand => _stepForwardCommand ??= new(async _ =>
    {
        await viewModel.MediaElement.Pause();
        await viewModel.MediaElement.StepForward();
    });

    private DelegateCommand? _shiftPositionCommand;
    public DelegateCommand ShiftPosition => _shiftPositionCommand ??= new(async param =>
    {
        if (param is not TimeSpan diff)
        {
            return;
        }

        var position = viewModel.MediaElement.Position + diff;
        await viewModel.MediaElement.Seek(position);
    });

    private DelegateCommand? _setSegmentLoop;
    public DelegateCommand SetSegmentLoop => _setSegmentLoop ??= new(_ =>
    {
        var controller = viewModel.Controller;

        if (controller.IsSegmentLoopEnabled &&
            controller.SegmentLoopFrom is not null &&
            controller.SegmentLoopTo is not null)
        {
            controller.IsSegmentLoopEnabled = false;
            controller.SegmentLoopFrom = null;
            controller.SegmentLoopTo = null;
            return Task.CompletedTask;
        }

        var currentPosition = viewModel.MediaElement.Position;
        if (controller.SegmentLoopFrom is null)
        {
            controller.IsSegmentLoopEnabled = false;
            controller.SegmentLoopFrom = currentPosition;
            controller.SegmentLoopTo = null;
            return Task.CompletedTask;
        }

        if (controller.SegmentLoopFrom >= currentPosition)
        {
            return Task.CompletedTask;
        }

        controller.SegmentLoopTo = currentPosition;
        controller.IsSegmentLoopEnabled = true;
        return Task.CompletedTask;
    });

    private DelegateCommand? _changeSpeedRatioComand;
    public DelegateCommand ChangeSpeedRatio => _changeSpeedRatioComand ??= new(param =>
    {
        if (double.TryParse(param?.ToString(), out var ratio) && ratio <= 0)
        {
            return Task.CompletedTask;
        }

        viewModel.MediaElement.SpeedRatio = ratio;

        return Task.CompletedTask;
    });

    private DelegateCommand? _captureThumbnail;
    public DelegateCommand CaptureThumbnail => _captureThumbnail ??= new(async param =>
    {
        if (param is not TimeSpan position)
        {
            return;
        }

        if (!viewModel.PreviewMediaElement.IsOpen)
        {
            return;
        }

        await viewModel.PreviewMediaElement.Seek(position);

        // Capture thumbnail
        var bitmap = await viewModel.PreviewMediaElement.CaptureBitmapAsync();
        if (bitmap is null)
        {
            viewModel.Controller.Thumbnail = null;
            return;
        }

        // Convert into BitmapImage
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = memory;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        viewModel.Controller.Thumbnail = bitmapImage;
    });

    private DelegateCommand? _saveCaptureCommand;
    public DelegateCommand SaveCapture => _saveCaptureCommand ??= new(param =>
    {
        if (param is not string path)
        {
            return Task.CompletedTask;
        }

        lock (_captureSyncLock)
        {
            if (_isCaptureInProgress)
            {
                return Task.CompletedTask;
            }

            _isCaptureInProgress = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var bitmap = viewModel.MediaElement.CaptureBitmapAsync().GetAwaiter().GetResult();
                if (bitmap is null)
                {
                    return;
                }

                bitmap.Save(path, ImageFormat.Png);
            }
            finally
            {
                lock (_captureSyncLock)
                {
                    _isCaptureInProgress = false;
                }
            }
        });

        return Task.CompletedTask;
    });
}
