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

        return songs.EnumerateArray().Select(ParseSong).Select(WithCoverArtUrl).ToArray();
    }

    public async Task<IReadOnlyList<Album>> GetNewestAlbumsAsync(
        int size = 40,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync(
            "getAlbumList2",
            new Dictionary<string, string>
            {
                ["type"] = "newest",
                ["size"] = size.ToString()
            },
            cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("albumList2", out var albumList) ||
            !albumList.TryGetProperty("album", out var albums) ||
            albums.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Album>();
        }

        return albums.EnumerateArray().Select(ParseAlbum).Select(WithCoverArtUrl).ToArray();
    }

    public async Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync("getArtists", null, cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("artists", out var artistsRoot) ||
            !artistsRoot.TryGetProperty("index", out var indexes) ||
            indexes.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Artist>();
        }

        return indexes
            .EnumerateArray()
            .Where(index => index.TryGetProperty("artist", out var artists) && artists.ValueKind == JsonValueKind.Array)
            .SelectMany(index => index.GetProperty("artist").EnumerateArray())
            .Select(ParseArtist)
            .Select(WithCoverArtUrl)
            .OrderBy(artist => artist.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<Album>> GetArtistAlbumsAsync(
        string artistId,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync(
            "getArtist",
            new Dictionary<string, string> { ["id"] = artistId },
            cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("artist", out var artist) ||
            !artist.TryGetProperty("album", out var albums) ||
            albums.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Album>();
        }

        return albums.EnumerateArray().Select(ParseAlbum).Select(WithCoverArtUrl).ToArray();
    }

    public async Task<IReadOnlyList<Song>> GetAlbumSongsAsync(
        string albumId,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync(
            "getAlbum",
            new Dictionary<string, string> { ["id"] = albumId },
            cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("album", out var album) ||
            !album.TryGetProperty("song", out var songs) ||
            songs.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Song>();
        }

        return songs.EnumerateArray().Select(ParseSong).Select(WithCoverArtUrl).ToArray();
    }

    public async Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync("getPlaylists", null, cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("playlists", out var playlistList) ||
            !playlistList.TryGetProperty("playlist", out var playlists) ||
            playlists.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Playlist>();
        }

        return playlists.EnumerateArray().Select(ParsePlaylist).Select(WithCoverArtUrl).ToArray();
    }

    public async Task<IReadOnlyList<Song>> GetPlaylistSongsAsync(
        string playlistId,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync(
            "getPlaylist",
            new Dictionary<string, string> { ["id"] = playlistId },
            cancellationToken);

        var response = document.RootElement.GetProperty("subsonic-response");
        EnsureOk(response);

        if (!response.TryGetProperty("playlist", out var playlist) ||
            !playlist.TryGetProperty("entry", out var songs) ||
            songs.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Song>();
        }

        return songs.EnumerateArray().Select(ParseSong).Select(WithCoverArtUrl).ToArray();
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
            null,
            GetNullableInt(element, "duration"));
    }

    private static Album ParseAlbum(JsonElement element)
    {
        return new Album(
            GetString(element, "id"),
            GetString(element, "name", "Untitled album"),
            GetString(element, "artist", "Unknown artist"),
            GetNullableString(element, "coverArt"),
            null,
            GetNullableInt(element, "songCount"),
            GetNullableInt(element, "year"));
    }

    private static Artist ParseArtist(JsonElement element)
    {
        return new Artist(
            GetString(element, "id"),
            GetString(element, "name", "Unknown artist"),
            GetNullableString(element, "coverArt"),
            null,
            GetNullableInt(element, "albumCount"));
    }

    private static Playlist ParsePlaylist(JsonElement element)
    {
        return new Playlist(
            GetString(element, "id"),
            GetString(element, "name", "Untitled playlist"),
            GetNullableString(element, "coverArt"),
            null,
            GetNullableInt(element, "songCount"),
            GetNullableInt(element, "duration"));
    }

    private Song WithCoverArtUrl(Song song)
    {
        return song with { CoverArtUrl = GetCoverArtUrl(song.CoverArt, 128) };
    }

    private Album WithCoverArtUrl(Album album)
    {
        return album with { CoverArtUrl = GetCoverArtUrl(album.CoverArt, 160) };
    }

    private Artist WithCoverArtUrl(Artist artist)
    {
        return artist with { CoverArtUrl = GetCoverArtUrl(artist.CoverArt, 160) };
    }

    private Playlist WithCoverArtUrl(Playlist playlist)
    {
        return playlist with { CoverArtUrl = GetCoverArtUrl(playlist.CoverArt, 160) };
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
