using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace VmeMusic.Converters;

public sealed class CoverArtConverter : IValueConverter
{
    private readonly HttpClient _httpClient = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string url && !string.IsNullOrWhiteSpace(url)
            ? LoadBitmapAsync(url)
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    private async Task<Bitmap?> LoadBitmapAsync(string url)
    {
        try
        {
            await using var stream = await _httpClient.GetStreamAsync(url);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
