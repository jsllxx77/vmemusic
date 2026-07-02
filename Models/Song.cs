namespace VmeMusic.Models;

public sealed record Song(
    string Id,
    string Title,
    string Artist,
    string Album,
    string? CoverArt,
    int? DurationSeconds);
