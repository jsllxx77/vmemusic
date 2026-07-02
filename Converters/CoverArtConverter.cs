using System.Globalization;
using System.Collections.Concurrent;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace VmeMusic.Converters;

public sealed class CoverArtConverter : IValueConverter
{
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _cache = new();
    private readonly HttpClient _httpClient = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string url && !string.IsNullOrWhiteSpace(url)
            ? _cache.GetOrAdd(url, static (key, state) =>
                new Lazy<Task<Bitmap?>>(() => state.LoadBitmapAsync(key)), this).Value
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
