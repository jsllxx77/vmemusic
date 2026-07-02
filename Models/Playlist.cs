namespace VmeMusic.Models;

public sealed record Playlist(
    string Id,
    string Name,
    string? CoverArt,
    string? CoverArtUrl,
    int? SongCount,
    int? DurationSeconds)
{
    public string DurationText => DurationFormatter.FormatSeconds(DurationSeconds);
}
