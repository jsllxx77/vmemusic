using System.Globalization;
using System.Collections.Concurrent;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using VmeMusic.Services;

namespace VmeMusic.Converters;

public sealed class CoverArtConverter : IValueConverter
{
    public static CoverArtCacheService CacheService { get; set; } = new();

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
            await using var stream = await CacheService.OpenReadOrDownloadAsync(url, _httpClient);
            if (stream is null)
            {
                return null;
            }

            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
