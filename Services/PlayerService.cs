using LibVLCSharp.Shared;

namespace VmeMusic.Services;

public sealed class PlayerService : IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;

    public bool IsPlaying => _mediaPlayer?.IsPlaying == true;

    public void PlayUrl(string url)
    {
        var (libVlc, mediaPlayer) = EnsurePlayer();
        using var media = new Media(libVlc, new Uri(url));
        mediaPlayer.Play(media);
    }

    public void Pause()
    {
        _mediaPlayer?.Pause();
    }

    public void Stop()
    {
        _mediaPlayer?.Stop();
    }

    public void Dispose()
    {
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }

    private (LibVLC LibVlc, MediaPlayer MediaPlayer) EnsurePlayer()
    {
        if (_libVlc is not null && _mediaPlayer is not null)
        {
            return (_libVlc, _mediaPlayer);
        }

        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        return (_libVlc, _mediaPlayer);
    }
}
