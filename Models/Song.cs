namespace VmeMusic.Models;

public sealed record Song(
    string Id,
    string Title,
    string Artist,
    string Album,
    string? CoverArt,
    string? CoverArtUrl,
    int? DurationSeconds)
{
    public string DurationText => DurationFormatter.FormatSeconds(DurationSeconds);
}
