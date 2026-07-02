using System.Globalization;
using Avalonia.Data.Converters;

namespace VmeMusic.Converters;

public sealed class SongMatchConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var left = values.Count > 0 ? values[0] as string : null;
        var right = values.Count > 1 ? values[1] as string : null;
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.Ordinal);
    }
}
