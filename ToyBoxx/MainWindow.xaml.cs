using FFmpeg.AutoGen;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ToyBoxx.Foundation;
using ToyBoxx.ViewModels;
using Unosquare.FFME.Common;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private const double ZoomStep = 0.1;

    private DispatcherTimer? _mouseMoveTimer;
    private DateTime _lastMouseMoveTime;
    private Point _lastMousePosition;
    private bool _isControllerHideCompleted;
    private bool _isDragging = false;
    private Point _lastMousePos;
    private DateTime _lastControllerClick = DateTime.MinValue;
    private readonly WindowStatus _previousWindowStatus = new();

    public MainWindow()
    {
        ViewModel = App.ViewModel;
        ViewModel.RequestToggleFullScreen += ToggleFullScreen;

        InitializeComponent();
        InitializeMainWindow();
        InitializeMediaEvents();
    }

    public RootViewModel ViewModel { get; }

    private Storyboard HideControllerAnimation => FindResource("HideControlOpacity") as Storyboard ?? throw new Exception("Control 'HideControlOpacity' not found.");

    private Storyboard ShowControllerAnimation => FindResource("ShowControlOpacity") as Storyboard ?? throw new Exception("Control 'ShowControlOpacity' not found.");

    private void InitializeMainWindow()
    {
        PreviewKeyDown += OnWindowKeyDown;

        _lastMouseMoveTime = DateTime.UtcNow;

        Loaded += (s, e) =>
        {
            Storyboard.SetTarget(HideControllerAnimation, ControllerPanel);
            Storyboard.SetTarget(ShowControllerAnimation, ControllerPanel);

            HideControllerAnimation.Completed += (es, ee) =>
            {
                ControllerPanel.Visibility = Visibility.Hidden;
                _isControllerHideCompleted = true;
            };

            ShowControllerAnimation.Completed += (es, ee) =>
            {
                _isControllerHideCompleted = false;
            };
        };

        MouseMove += (s, e) =>
        {
            var currentPosition = e.GetPosition(Application.Current.MainWindow);
            if (Math.Abs(currentPosition.X - _lastMousePosition.X) > double.Epsilon ||
                Math.Abs(currentPosition.Y - _lastMousePosition.Y) > double.Epsilon)
                _lastMouseMoveTime = DateTime.UtcNow;

            _lastMousePosition = currentPosition;
        };

        MouseLeave += (s, e) =>
        {
            _lastMouseMoveTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10));
        };

        _mouseMoveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
            IsEnabled = true
        };

        _mouseMoveTimer.Tick += (s, e) =>
        {
            var elapsedSinceMouseMove = DateTime.UtcNow.Subtract(_lastMouseMoveTime);
            if (elapsedSinceMouseMove.TotalMilliseconds >= 3000 && Media.IsOpen && !ControllerPanel.IsMouseOver)
            {
                if (_isControllerHideCompleted)
                {
                    return;
                }

                Cursor = Cursors.None;
                HideControllerAnimation?.Begin();
                _isControllerHideCompleted = false;
            }
            else
            {
                Cursor = Cursors.Arrow;
                ControllerPanel.Visibility = Visibility.Visible;
                ShowControllerAnimation?.Begin();
            }
        };

        _mouseMoveTimer.Start();
    }

    private void InitializeMediaEvents()
    {
        PreviewMedia.RendererOptions.VideoImageType = VideoRendererImageType.InteropBitmap;

        Media.RendererOptions.UseLegacyAudioOut = true;
        Media.Loaded += (s, e) => ResetTransform();
        Media.MediaOpening += (s, e) =>
        {
            // Use hardware device if needed
            if (e.Options.VideoStream is StreamInfo videoStream)
            {
                var deviceCandidates = new[]
                {
                    AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2
                };

                // Hardware device selection
                if (videoStream.FPS <= 30)
                {
                    var devices = new List<HardwareDeviceInfo>(deviceCandidates.Length);
                    foreach (var deviceType in deviceCandidates)
                    {
                        var accelerator = videoStream.HardwareDevices.FirstOrDefault(d => d.DeviceType == deviceType);
                        if (accelerator == null) continue;

                        devices.Add(accelerator);
                    }

                    e.Options.VideoHardwareDevices = [.. devices];
                }
            }
        };
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Keep the key focus on the main window
        FocusManager.SetIsFocusScope(this, true);
        FocusManager.SetFocusedElement(this, this);

        switch (e.Key)
        {
            case Key.Space when Media.IsOpen && Media.IsPlaying:
                await ViewModel.Commands.Pause.ExecuteAsync(null);
                break;
            case Key.Space when Media.IsOpen && !Media.IsPlaying:
                await ViewModel.Commands.Play.ExecuteAsync(null);
                break;
            case Key.Right when Media.IsOpen && !Media.IsSeeking:
                await ViewModel.Commands.ShiftPosition.ExecuteAsync(TimeSpan.FromSeconds(5));
                break;
            case Key.Left when Media.IsOpen && !Media.IsSeeking:
                await ViewModel.Commands.ShiftPosition.ExecuteAsync(TimeSpan.FromSeconds(-5));
                break;
            case Key.S when Media.IsOpen:
                var savePath = GetCaptureSavePath();
                await ViewModel.Commands.SaveCapture.ExecuteAsync(savePath);
                break;
            case Key.R when Media.IsOpen:
                ViewModel.Angle += 90;
                break;
        }

        string GetCaptureSavePath()
        {
            var picturePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var uri = new Uri(Media.MediaInfo.MediaSource);
            var title = Path.GetFileNameWithoutExtension(uri.LocalPath);
            var fileName = $"{title}_{DateTime.Now:yyyyMMddhhmmssfff}.png";
            return Path.Combine(picturePath, fileName);
        }
    }

    private void FluentWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.Scale > 1.0)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            Mouse.Capture(this);
        }
    }

    private void FluentWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            Mouse.Capture(null);
        }
    }

    private void FluentWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && ViewModel.Scale > 1.0)
        {
            var pos = e.GetPosition(this);
            var delta = pos - _lastMousePos;

            ViewModel.TransformX += delta.X;
            ViewModel.TransformY += delta.Y;

            _lastMousePos = pos;
        }
    }

    private void FluentWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom when Ctrl+Wheel
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var scale = ViewModel.Scale;
            // calculate next scale
            if (e.Delta > 0)
            {
                scale += ZoomStep;
            }
            else
            {
                scale = Math.Max(ZoomStep, scale - ZoomStep);
            }

            ViewModel.ScaleCenterX = ViewModel.MediaElement.ActualWidth / 2;
            ViewModel.ScaleCenterY = ViewModel.MediaElement.ActualHeight / 2;

            if (scale < 1.0)
            {
                // Fix to center
                ViewModel.TransformX = 0;
                ViewModel.TransformY = 0;
            }

            ViewModel.Scale = scale;

            e.Handled = true;
        }
    }

    private void FluentWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Reset zoom&pan
        if (e.ChangedButton == MouseButton.Middle)
        {
            ResetTransform();

            e.Handled = true;
        }
    }

    private void LayoutPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        if ((now - _lastControllerClick).TotalMilliseconds <= System.Windows.Forms.SystemInformation.DoubleClickTime)
        {
            e.Handled = true;
        }
        _lastControllerClick = now;
    }

    private void ToggleFullScreen()
    {
        if (WindowState == WindowState.Maximized)
        {
            _previousWindowStatus.ApplyState(this);

            WindowStatus.EnableDisplayTimeout();
        }
        else
        {
            _previousWindowStatus.CaptureState(this);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;

            WindowStatus.DisableDisplayTimeout();
        }
    }

    private void ResetTransform()
    {
        ViewModel.Scale = 1.0;
        ViewModel.ScaleCenterX = ViewModel.MediaElement.ActualWidth / 2;
        ViewModel.ScaleCenterY = ViewModel.MediaElement.ActualHeight / 2;
        ViewModel.TransformX = 0;
        ViewModel.TransformY = 0;
        ViewModel.Angle = 0;
    }
}