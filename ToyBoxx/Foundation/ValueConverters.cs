using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ToyBoxx.Foundation;

/// <inheritdoc />
[ValueConversion(typeof(bool), typeof(bool))]
internal class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType != typeof(bool) && targetType != typeof(bool?))
        {
            throw new InvalidOperationException("The target must be a boolean or a nullable boolean");
        }

        if (value is bool?)
        {
            var nullableBool = (bool?)value;
            return !nullableBool.HasValue || !nullableBool.Value;
        }

        if (value is bool)
        {
            return !(bool)value;
        }

        return true;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return true;
        }

        if (value is bool?)
        {
            var nullableBool = (bool?)value;
            return !nullableBool.HasValue || !nullableBool.Value;
        }

        if (value is bool)
        {
            return !(bool)value;
        }

        return true;
    }
}

internal class TimeSpanToSecondsConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TimeSpan span => span.TotalSeconds,
            Duration duration => duration.HasTimeSpan ? duration.TimeSpan.TotalSeconds : 0d,
            _ => (object)0d,
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double == false)
        {
            return 0d;
        }

        var result = TimeSpan.FromTicks(System.Convert.ToInt64(TimeSpan.TicksPerSecond * (double)value));

        // Do the conversion from visibility to bool
        if (targetType == typeof(TimeSpan))
        {
            return result;
        }

        return targetType == typeof(Duration)
            ? new Duration(result)
            : Activator.CreateInstance(targetType);
    }
}

internal class SpeedRatioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int or double or float)
        {
            return $"x {value}";
        }

        return "x 1";
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var ratio = value?.ToString()?[2..];
        return double.TryParse(ratio, out var result)
            ? result
            : 1.0;
    }
}