using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ToyBoxx.ViewModels;

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
    private double _currentScale = 1.0;
    private bool _isDragging = false;
    private Point _lastMousePos;

    public MainWindow()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
        InitializeMainWindow();
    }

    public RootViewModel ViewModel { get; }

    private Storyboard HideControllerAnimation => FindResource("HideControlOpacity") as Storyboard ?? throw new Exception("Control 'HideControlOpacity' not found.");

    private Storyboard ShowControllerAnimation => FindResource("ShowControlOpacity") as Storyboard ?? throw new Exception("Control 'ShowControlOpacity' not found.");

    private void InitializeMainWindow()
    {
        Loaded += OnWindowLoaded;
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

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;

        // Open a file if it is specified in the arguments
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            App.ViewModel.Commands.Open.Execute(args[1].Trim());
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Keep the key focus on the main window
        FocusManager.SetIsFocusScope(this, true);
        FocusManager.SetFocusedElement(this, this);

        switch (e.Key)
        {
            case Key.Space when Media.IsPlaying:
                App.ViewModel.Commands.Pause.Execute(null);
                break;
            case Key.Space when !Media.IsPlaying:
                App.ViewModel.Commands.Play.Execute(null);
                break;
        }
    }

    private void Media_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        App.ViewModel.Commands.ToggleFullScreen.Execute(null);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files is [])
        {
            return;
        }

        App.ViewModel.Commands.Open.Execute(files[0]);
    }

    private void FluentWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom when Ctrl+Wheel
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // calculate next scale
            if (e.Delta > 0)
            {
                _currentScale += ZoomStep;
            }
            else
            {
                _currentScale = Math.Max(ZoomStep, _currentScale - ZoomStep);
            }

            // Set center position
            if (_currentScale >= 1.0)
            {
                // Use mouse pos when 100%+ zoom
                var pos = e.GetPosition(Media);
                scaleTransform.CenterX = pos.X;
                scaleTransform.CenterY = pos.Y;
            }
            else
            {
                scaleTransform.CenterX = Media.ActualWidth / 2;
                scaleTransform.CenterY = Media.ActualHeight / 2;

                translateTransform.X = 0;
                translateTransform.Y = 0;
            }

            scaleTransform.ScaleX = _currentScale;
            scaleTransform.ScaleY = _currentScale;

            e.Handled = true;
        }
    }

    private void FluentWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentScale > 1.0)
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
        if (_isDragging && _currentScale > 1.0)
        {
            var pos = e.GetPosition(this);
            var delta = pos - _lastMousePos;

            translateTransform.X += delta.X;
            translateTransform.Y += delta.Y;

            _lastMousePos = pos;
        }
    }
}