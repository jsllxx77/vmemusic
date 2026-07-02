namespace VmeMusic.Models;

public sealed record NavidromeConnection(string BaseUrl, string Username, string Password)
{
    public string NormalizedBaseUrl => BaseUrl.Trim().TrimEnd('/');
}
