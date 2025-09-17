using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ToyBoxx.ViewModels;

namespace ToyBoxx.Controls
{
    /// <summary>
    /// ControllerPanelControl.xaml の相互作用ロジック
    /// </summary>
    public partial class ControllerPanelControl : UserControl
    {
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
    }
}
