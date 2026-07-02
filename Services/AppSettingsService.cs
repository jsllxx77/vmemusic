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

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken);
        if (settings is null)
        {
            return CreateDefaultSettings();
        }

        return MigrateLegacySettings(settings);
    }

    public async Task<NavidromeConnection?> LoadConnectionAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        var activeServer = settings.Servers.FirstOrDefault(server => server.Id == settings.ActiveServerId)
            ?? settings.Servers.FirstOrDefault();

        if (activeServer is null)
        {
            return null;
        }

        return new NavidromeConnection(
            activeServer.ServerUrl,
            activeServer.Username,
            _passwordProtector.Unprotect(activeServer.ProtectedPassword));
    }

    public async Task<IReadOnlyList<SavedServerProfile>> LoadServerProfilesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        return settings.Servers;
    }

    public async Task SaveServerAsync(
        string? serverId,
        string displayName,
        NavidromeConnection connection,
        bool setActive,
        CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        var normalizedUrl = connection.NormalizedBaseUrl;
        var existing = !string.IsNullOrWhiteSpace(serverId)
            ? settings.Servers.FirstOrDefault(server => server.Id == serverId)
            : settings.Servers.FirstOrDefault(server =>
                string.Equals(server.ServerUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(server.Username, connection.Username, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new SavedServerProfile();
            settings.Servers.Add(existing);
        }

        existing.DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? $"{connection.Username}@{normalizedUrl}"
            : displayName.Trim();
        existing.ServerUrl = normalizedUrl;
        existing.Username = connection.Username;
        existing.ProtectedPassword = _passwordProtector.Protect(connection.Password);

        if (setActive)
        {
            settings.ActiveServerId = existing.Id;
        }

        await SaveSettingsAsync(settings, cancellationToken);
    }

    public async Task RemoveServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        settings.Servers.RemoveAll(server => server.Id == serverId);
        if (settings.ActiveServerId == serverId)
        {
            settings.ActiveServerId = settings.Servers.FirstOrDefault()?.Id ?? "";
        }

        await SaveSettingsAsync(settings, cancellationToken);
    }

    public async Task SetActiveServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings.Servers.Any(server => server.Id == serverId))
        {
            settings.ActiveServerId = serverId;
            await SaveSettingsAsync(settings, cancellationToken);
        }
    }

    public async Task SaveCoverArtCacheDirectoryAsync(string cacheDirectory, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        settings.CoverArtCacheDirectory = cacheDirectory;
        await SaveSettingsAsync(settings, cancellationToken);
    }

    public async Task SaveConnectionAsync(NavidromeConnection connection, CancellationToken cancellationToken = default)
    {
        await SaveServerAsync(null, "", connection, true, cancellationToken);
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings = MigrateLegacySettings(settings);

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

    public string GetDefaultCoverArtCacheDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VmeMusic",
            "cover-art");
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

    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            CoverArtCacheDirectory = GetDefaultCoverArtCacheDirectory()
        };
    }

    private AppSettings MigrateLegacySettings(AppSettings settings)
    {
        settings.CoverArtCacheDirectory = string.IsNullOrWhiteSpace(settings.CoverArtCacheDirectory)
            ? GetDefaultCoverArtCacheDirectory()
            : settings.CoverArtCacheDirectory;

        if (settings.Servers.Count == 0 &&
            !string.IsNullOrWhiteSpace(settings.ServerUrl) &&
            !string.IsNullOrWhiteSpace(settings.Username))
        {
            var legacyServer = new SavedServerProfile
            {
                DisplayName = $"{settings.Username}@{settings.ServerUrl}",
                ServerUrl = settings.ServerUrl,
                Username = settings.Username,
                ProtectedPassword = settings.ProtectedPassword
            };

            settings.Servers.Add(legacyServer);
            settings.ActiveServerId = legacyServer.Id;
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveServerId) && settings.Servers.Count > 0)
        {
            settings.ActiveServerId = settings.Servers[0].Id;
        }

        return settings;
    }
}
