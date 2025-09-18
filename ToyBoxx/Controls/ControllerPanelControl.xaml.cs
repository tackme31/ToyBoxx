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

            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(1);
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
            _idleTimer.Stop();
            _idleTimer.Start();

            // スライダー内のマウス座標を取得
            var posInSlider = e.GetPosition(PositionSlider);

            // スライダー基準の座標をCanvas基準に変換
            var posInCanvas = PositionSlider.TranslatePoint(posInSlider, PreviewImageCanvas);

            // 表示位置を設定
            Canvas.SetLeft(PreviewImageArea, posInCanvas.X - PreviewImageArea.Width / 2);
        }

        private void PositionSlider_MouseEnter(object sender, MouseEventArgs e)
        {
            // スライダー内のマウス座標を取得
            var posInSlider = e.GetPosition(PositionSlider);

            // スライダー基準の座標をCanvas基準に変換
            var posInCanvas = PositionSlider.TranslatePoint(posInSlider, PreviewImageCanvas);

            // 表示位置を設定
            Canvas.SetTop(PreviewImageArea, -PreviewImageArea.Height);
            Canvas.SetLeft(PreviewImageArea, posInCanvas.X - PreviewImageArea.Width / 2);

            PreviewImageCanvas.Visibility = System.Windows.Visibility.Visible;
        }

        private void PositionSlider_MouseLeave(object sender, MouseEventArgs e)
        {
            PreviewImageCanvas.Visibility = System.Windows.Visibility.Collapsed;

            _idleTimer.Stop();
        }

        private async void IdleTimer_Tick(object? sender, EventArgs e)
        {
            _idleTimer.Stop();

            var pos = Mouse.GetPosition(PositionSlider);


            // Position (sec) at mouse
            var value = PositionSlider.Minimum + (pos.X / PositionSlider.ActualWidth) * (PositionSlider.Maximum - PositionSlider.Minimum);

            //App.ViewModel.PreviewMediaElement.Position = TimeSpan.FromSeconds(value);
            //var bitmap = await App.ViewModel.PreviewMediaElement.CaptureBitmapAsync();

            Console.WriteLine($"Mouse over slider at value: {value}");
        }
    }
}
