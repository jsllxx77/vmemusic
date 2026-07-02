namespace VmeMusic.Models;

public sealed record Album(
    string Id,
    string Name,
    string Artist,
    string? CoverArt,
    string? CoverArtUrl,
    int? SongCount,
    int? Year);
