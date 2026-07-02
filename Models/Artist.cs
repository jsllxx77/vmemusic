namespace VmeMusic.Models;

public sealed record Artist(
    string Id,
    string Name,
    string? CoverArt,
    string? CoverArtUrl,
    int? AlbumCount);
