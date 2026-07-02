using System.Text.Json;
using VmeMusic.Models;

namespace VmeMusic.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly PasswordProtector _passwordProtector;
    private readonly string _settingsPath;

    public AppSettingsService(PasswordProtector? passwordProtector = null, string? settingsPath = null)
    {
        _passwordProtector = passwordProtector ?? new PasswordProtector();
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public async Task<NavidromeConnection?> LoadConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken);
        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.ServerUrl) ||
            string.IsNullOrWhiteSpace(settings.Username))
        {
            return null;
        }

        var password = _passwordProtector.Unprotect(settings.ProtectedPassword);
        return new NavidromeConnection(settings.ServerUrl, settings.Username, password);
    }

    public async Task SaveConnectionAsync(NavidromeConnection connection, CancellationToken cancellationToken = default)
    {
        var settings = new AppSettings
        {
            ServerUrl = connection.NormalizedBaseUrl,
            Username = connection.Username,
            ProtectedPassword = _passwordProtector.Protect(connection.Password)
        };

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }

        return Task.CompletedTask;
    }

    private static string GetDefaultSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "VmeMusic", "settings.json");
    }
}
