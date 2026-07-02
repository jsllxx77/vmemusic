namespace VmeMusic.Models;

public sealed class SavedServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = "";

    public string ServerUrl { get; set; } = "";

    public string Username { get; set; } = "";

    public string ProtectedPassword { get; set; } = "";
}
