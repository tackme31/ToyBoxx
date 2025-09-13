using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ToyBoxx.ViewModels;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DispatcherTimer? _mouseMoveTimer;
    private DateTime _lastMouseMoveTime;
    private Point _lastMousePosition;
    private bool _isControllerHideCompleted;

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
        App.ViewModel.Commands.ToggleFullscreen.Execute(null);
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
}