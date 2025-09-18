using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ToyBoxx.Controls;

public class PositionSlider : HtmlLikeSlider
{
    public double? LoopStart
    {
        get { return (double?)GetValue(LoopStartProperty); }
        set { SetValue(LoopStartProperty, value); }
    }

    public static readonly DependencyProperty LoopStartProperty =
        DependencyProperty.Register(nameof(LoopStart), typeof(double?), typeof(PositionSlider),
            new PropertyMetadata(0.0, OnLoopChanged));

    public double? LoopEnd
    {
        get { return (double?)GetValue(LoopEndProperty); }
        set { SetValue(LoopEndProperty, value); }
    }

    public static readonly DependencyProperty LoopEndProperty =
        DependencyProperty.Register(nameof(LoopEnd), typeof(double?), typeof(PositionSlider),
            new PropertyMetadata(0.0, OnLoopChanged));

    private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PositionSlider slider)
        {
            slider.InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (Template.FindName("PART_Track", this) is Track track)
        {
            double min = Minimum;
            double max = Maximum;
            double thumbWidth = track.Thumb.ActualWidth;
            double rangeWidth = track.ActualWidth - thumbWidth;

            if (LoopStart.HasValue && LoopStart >= min && LoopStart <= max)
            {
                double startX = thumbWidth / 2 + (LoopStart.Value - min) / (max - min) * rangeWidth;
                dc.DrawLine(new Pen(Brushes.White, 2), new Point(startX, 0), new Point(startX, ActualHeight));
            }

            if (LoopEnd.HasValue && LoopEnd >= min && LoopEnd <= max)
            {
                double endX = thumbWidth / 2 + (LoopEnd.Value - min) / (max - min) * rangeWidth;
                dc.DrawLine(new Pen(Brushes.White, 2), new Point(endX, 0), new Point(endX, ActualHeight));
            }
        }
    }
}
