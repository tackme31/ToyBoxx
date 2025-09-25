using System.Windows.Controls;
using System.Windows.Input;

namespace ToyBoxx.Controls;

public class HtmlLikeSlider : Slider
{
    public event EventHandler? DragStarted;
    public event EventHandler? DragCompleted;

    public bool IsDraggingThumb { get; private set; }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        var pos = e.GetPosition(this);
        double relativePos = Orientation == Orientation.Horizontal
            ? pos.X / ActualWidth
            : 1.0 - (pos.Y / ActualHeight); // 縦方向は逆

        Value = Minimum + (Maximum - Minimum) * relativePos;

        IsDraggingThumb = true;
        CaptureMouse();
        e.Handled = true;

        DragStarted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (IsDraggingThumb && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(this);
            double relativePos = Orientation == Orientation.Horizontal
                ? pos.X / ActualWidth
                : 1.0 - (pos.Y / ActualHeight);

            Value = Minimum + (Maximum - Minimum) * relativePos;
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (IsDraggingThumb)
        {
            IsDraggingThumb = false;
            ReleaseMouseCapture();
            e.Handled = true;

            DragCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
