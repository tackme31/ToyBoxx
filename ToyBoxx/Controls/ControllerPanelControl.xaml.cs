using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Unosquare.FFME.Common;

namespace ToyBoxx.Controls
{
    /// <summary>
    /// ControllerPanelControl.xaml の相互作用ロジック
    /// </summary>
    public partial class ControllerPanelControl : UserControl
    {
        private MediaPlaybackState _mediaStateBeforeDraggingStart;
        private bool _isPositionSliderDragging = false;

        public ControllerPanelControl()
        {
            InitializeComponent();
        }

        private void PositionSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Custom Slider behavior:
            // - Click on the track moves the thumb immediately to the clicked position
            // - The thumb can be dragged immediately after clicking without releasing the mouse
            // - Slider value updates in real-time while dragging
            // - Mouse release finalizes the value

            var slider = (Slider)sender;
            if (!slider.IsEnabled)
            {
                return;
            }

            if (VisualTreeHelper.GetChild(slider, 0) is FrameworkElement root &&
                root.FindName("PART_Track") is Track track)
            {
                // Save media state before dragging
                _mediaStateBeforeDraggingStart = App.ViewModel.MediaElement.MediaState;

                // Pause when dragging
                App.ViewModel.Commands.Pause.Execute(null);

                // Convert mouse position to Track's coordinate system
                Point posOnTrack = e.GetPosition(track);

                // Immediately set Value to the clicked position
                slider.Value = track.ValueFromPoint(posOnTrack);

                // Force layout update to sync Thumb's visual position
                slider.UpdateLayout();

                // Start custom dragging (without relying on Thumb's internal drag)
                _isPositionSliderDragging = true;
                slider.CaptureMouse();

                // Start dragging
                e.Handled = true;
            }
        }

        private void PositionSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPositionSliderDragging)
            {
                return;
            }

            var slider = (Slider)sender;
            var pos = e.GetPosition(slider);

            if (VisualTreeHelper.GetChild(slider, 0) is FrameworkElement root &&
                root.FindName("PART_Track") is Track track)
            {
                // Update Value based on current mouse position on Track
                slider.Value = track.ValueFromPoint(pos);
            }
        }

        private void PositionSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPositionSliderDragging)
            {
                return;
            }

            var slider = (Slider)sender;
            _isPositionSliderDragging = false;
            slider.ReleaseMouseCapture();

            if (_mediaStateBeforeDraggingStart == MediaPlaybackState.Manual ||
                _mediaStateBeforeDraggingStart == MediaPlaybackState.Play)
            {
                App.ViewModel.Commands.Play.Execute(null);
            }
        }
    }
}
