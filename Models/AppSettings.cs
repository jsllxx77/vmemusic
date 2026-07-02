namespace VmeMusic.Models;

public sealed class AppSettings
{
    public string ActiveServerId { get; set; } = "";

    public string CoverArtCacheDirectory { get; set; } = "";

    public List<SavedServerProfile> Servers { get; set; } = [];

    public string ServerUrl { get; set; } = "";

    public string Username { get; set; } = "";

    public string ProtectedPassword { get; set; } = "";
}
