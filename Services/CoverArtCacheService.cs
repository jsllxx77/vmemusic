using System.Security.Cryptography;
using System.Text;

namespace VmeMusic.Services;

public sealed class CoverArtCacheService
{
    private string _cacheDirectory;

    public CoverArtCacheService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? GetDefaultCacheDirectory();
        Directory.CreateDirectory(_cacheDirectory);
    }

    public string CacheDirectory => _cacheDirectory;

    public void Configure(string? cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            return;
        }

        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public string GetCachePath(string url)
    {
        var extension = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".img";
        }

        return Path.Combine(_cacheDirectory, ComputeSha256(url) + extension);
    }

    public async Task<Stream?> OpenReadOrDownloadAsync(string url, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(url);
        if (File.Exists(path))
        {
            return File.OpenRead(path);
        }

        try
        {
            var bytes = await httpClient.GetByteArrayAsync(url, cancellationToken);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            return new MemoryStream(bytes, writable: false);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_cacheDirectory))
        {
            File.Delete(file);
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDefaultCacheDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "cache");
        }

        return Path.Combine(root, "VmeMusic", "cover-art");
    }
}
