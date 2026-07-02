using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VmeMusic.Models;

namespace VmeMusic.Services;

public sealed class NavidromeClient
{
    private const string ApiVersion = "1.16.1";
    private const string ClientName = "vmemusic";
    private readonly HttpClient _httpClient;
    private NavidromeConnection? _connection;

    public NavidromeClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public bool IsConfigured => _connection is not null;

    public void Configure(NavidromeConnection connection)
    {
        _connection = connection;
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync("ping", null, cancellationToken);
        EnsureOk(document.RootElement.GetProperty("subsonic-response"));
    }

    public async Task<IReadOnlyList<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Song>();
        }

        using var document = await GetAsync(
            "search3",
            new Dictionary<string, string>
            {
                ["query"] = query,
                ["songCount"] = "50",
                ["albumCount"] = "0",
                ["artistCount"] = "0"
            },
            cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("searchResult3", out var result) ||
            !result.TryGetProperty("song", out var songs) ||
            songs.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Song>();
        }

        return songs.EnumerateArray().Select(ParseSong).ToArray();
    }

    public string GetStreamUrl(string songId)
    {
        return BuildUrl("stream", new Dictionary<string, string> { ["id"] = songId });
    }

    public string? GetCoverArtUrl(string? coverArtId, int size = 300)
    {
        if (string.IsNullOrWhiteSpace(coverArtId))
        {
            return null;
        }

        return BuildUrl(
            "getCoverArt",
            new Dictionary<string, string>
            {
                ["id"] = coverArtId,
                ["size"] = size.ToString()
            });
    }

    public async Task ScrobbleAsync(string songId, CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync(
            "scrobble",
            new Dictionary<string, string>
            {
                ["id"] = songId,
                ["submission"] = "true"
            },
            cancellationToken);

        EnsureOk(document.RootElement.GetProperty("subsonic-response"));
    }

    private async Task<JsonDocument> GetAsync(
        string method,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(method, parameters);
        await using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private string BuildUrl(string method, IReadOnlyDictionary<string, string>? parameters)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Navidrome connection has not been configured.");
        }

        var query = BuildAuthParameters(_connection.Username, _connection.Password);
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                query[key] = value;
            }
        }

        var encoded = string.Join(
            "&",
            query.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

        return $"{_connection.NormalizedBaseUrl}/rest/{method}.view?{encoded}";
    }

    private static Dictionary<string, string> BuildAuthParameters(string username, string password)
    {
        var salt = Guid.NewGuid().ToString("N")[..12];
        return new Dictionary<string, string>
        {
            ["u"] = username,
            ["s"] = salt,
            ["t"] = Md5Hex(password + salt),
            ["v"] = ApiVersion,
            ["c"] = ClientName,
            ["f"] = "json"
        };
    }

    private static string Md5Hex(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Song ParseSong(JsonElement element)
    {
        return new Song(
            GetString(element, "id"),
            GetString(element, "title", "Untitled"),
            GetString(element, "artist", "Unknown artist"),
            GetString(element, "album", "Unknown album"),
            GetNullableString(element, "coverArt"),
            GetNullableInt(element, "duration"));
    }

    private static void EnsureOk(JsonElement response)
    {
        if (response.TryGetProperty("status", out var status) && status.GetString() == "ok")
        {
            return;
        }

        if (response.TryGetProperty("error", out var error))
        {
            var message = GetString(error, "message", "Navidrome API error.");
            var code = GetNullableInt(error, "code");
            throw new SubsonicResponseException(message, code);
        }

        throw new SubsonicResponseException("Navidrome API returned a non-ok response.");
    }

    private static string GetString(JsonElement element, string name, string fallback = "")
    {
        return GetNullableString(element, name) ?? fallback;
    }

    private static string? GetNullableString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetNullableInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;
    }
}
