using System.Globalization;
using System.Windows.Data;

namespace ToyBoxx.Foundation;

/// <inheritdoc />
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBooleanConverter : IValueConverter
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