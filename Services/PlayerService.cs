using LibVLCSharp.Shared;

namespace VmeMusic.Services;

public sealed class PlayerService : IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;

    public bool IsPlaying => _mediaPlayer?.IsPlaying == true;

    public bool HasMedia => _mediaPlayer?.Media is not null;

    public bool IsMuted
    {
        get => _mediaPlayer?.Mute ?? false;
        set => EnsurePlayer().MediaPlayer.Mute = value;
    }

    public TimeSpan Position => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer?.Time ?? 0));

    public TimeSpan Duration => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer?.Length ?? 0));

    public int Volume
    {
        get => _mediaPlayer?.Volume ?? 80;
        set => EnsurePlayer().MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public event EventHandler? EndReached;

    public void PlayUrl(string url)
    {
        var (libVlc, mediaPlayer) = EnsurePlayer();
        using var media = new Media(libVlc, new Uri(url));
        mediaPlayer.Play(media);
    }

    public void Pause()
    {
        _mediaPlayer?.SetPause(true);
    }

    public void Resume()
    {
        var (_, mediaPlayer) = EnsurePlayer();
        if (mediaPlayer.Media is null)
        {
            return;
        }

        mediaPlayer.SetPause(false);
        mediaPlayer.Play();
    }

    public void Stop()
    {
        _mediaPlayer?.Stop();
    }

    public void Seek(TimeSpan position)
    {
        if (_mediaPlayer is null || _mediaPlayer.Length <= 0)
        {
            return;
        }

        var clamped = Math.Clamp(position.TotalMilliseconds, 0, _mediaPlayer.Length);
        _mediaPlayer.Time = (long)clamped;
    }

    public void ToggleMute()
    {
        EnsurePlayer().MediaPlayer.ToggleMute();
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
        _mediaPlayer.Volume = 80;
        _mediaPlayer.EndReached += (_, _) => EndReached?.Invoke(this, EventArgs.Empty);
        return (_libVlc, _mediaPlayer);
    }
}
