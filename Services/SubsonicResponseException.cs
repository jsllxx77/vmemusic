namespace VmeMusic.Services;

public sealed class SubsonicResponseException : Exception
{
    public SubsonicResponseException(string message, int? code = null)
        : base(message)
    {
        Code = code;
    }

    public int? Code { get; }
}
