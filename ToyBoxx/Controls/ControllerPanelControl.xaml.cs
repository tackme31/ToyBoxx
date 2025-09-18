using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ToyBoxx.ViewModels;

namespace ToyBoxx.Controls
{
    /// <summary>
    /// ControllerPanelControl.xaml の相互作用ロジック
    /// </summary>
    public partial class ControllerPanelControl : UserControl
    {
        private DispatcherTimer _idleTimer;
        private System.Windows.Point _lastMousePos;

        public ControllerPanelControl()
        {
            InitializeComponent();

            // Directly handle MouseLeftButtonDown to support rapid repeated clicks
            // (Click event would otherwise suppress fast consecutive presses)
            StepForwardButton.AddHandler(
                MouseLeftButtonDownEvent,
                new MouseButtonEventHandler((s, e) =>
                {
                    var vm = (RootViewModel)DataContext!;
                    vm.Commands.StepForwardCommand.Execute(null);
                }),
                handledEventsToo: true);

            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };

            _idleTimer.Tick += IdleTimer_Tick;
        }

        private void ToggleButton_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;

            var toggle = (ToggleButton)sender!;
            if (toggle.DataContext is RootViewModel vm)
            {
                if (vm.Commands.SetSegmentLoop.CanExecute(null))
                    vm.Commands.SetSegmentLoop.Execute(null);
            }
        }

        private void PlaybackSpeedButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PlaybackSpeedPopup.IsOpen = false;
            PlaybackSpeedButton.IsChecked = false;
        }

        private void PositionSlider_MouseMove(object sender, MouseEventArgs e)
        {
            // Ignore repeated MouseMove events on the right side of the thumb during playback.
            var pos = e.GetPosition(PositionSlider);
            if (pos == _lastMousePos)
            {
                return;
            }

            _lastMousePos = pos;

            _idleTimer.Stop();
            _idleTimer.Start();

            // Clear thumbnail
            var vm = (RootViewModel)DataContext!;
            vm.Controller.Thumbnail = null;

            // Move preview area to mouse point
            var posInSlider = e.GetPosition(PositionSlider);
            var posInCanvas = PositionSlider.TranslatePoint(posInSlider, PreviewImageCanvas);
            Canvas.SetLeft(PreviewImageArea, posInCanvas.X - PreviewImageArea.Width / 2);
        }

        private void PositionSlider_MouseEnter(object sender, MouseEventArgs e)
        {
            // Show preview area at enter point
            var posInSlider = e.GetPosition(PositionSlider);
            var posInCanvas = PositionSlider.TranslatePoint(posInSlider, PreviewImageCanvas);
            Canvas.SetTop(PreviewImageArea, -PreviewImageArea.Height);
            Canvas.SetLeft(PreviewImageArea, posInCanvas.X - PreviewImageArea.Width / 2);
            PreviewImageCanvas.Visibility = System.Windows.Visibility.Visible;
        }

        private void PositionSlider_MouseLeave(object sender, MouseEventArgs e)
        {
            // Hide preview area
            PreviewImageCanvas.Visibility = System.Windows.Visibility.Collapsed;

            _idleTimer.Stop();
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            _idleTimer.Stop();

            var mousePosition = Mouse.GetPosition(PositionSlider);

            // Position (sec) at mouse
            var mediaPosition = PositionSlider.Minimum + (mousePosition.X / PositionSlider.ActualWidth) * (PositionSlider.Maximum - PositionSlider.Minimum);

            var vm = (RootViewModel)DataContext!;
            vm.Commands.CaptureThumbnail.Execute(mediaPosition);
        }
    }
}
