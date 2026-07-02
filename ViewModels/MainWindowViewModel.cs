using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VmeMusic.Models;
using VmeMusic.Services;

namespace VmeMusic.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly NavidromeClient _navidromeClient;
    private readonly PlayerService _playerService;
    private readonly AppSettingsService _settingsService;
    private readonly DispatcherTimer _positionTimer;
    private bool _isUpdatingPlayerPosition;
    private int _currentQueueIndex = -1;

    [ObservableProperty]
    private string serverUrl = "";

    [ObservableProperty]
    private string username = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string searchQuery = "";

    [ObservableProperty]
    private Song? selectedSong;

    [ObservableProperty]
    private Album? selectedAlbum;

    [ObservableProperty]
    private Playlist? selectedPlaylist;

    [ObservableProperty]
    private Song? currentSong;

    [ObservableProperty]
    private LibraryViewKind currentView = LibraryViewKind.Songs;

    [ObservableProperty]
    private string contentTitle = "Songs";

    [ObservableProperty]
    private string statusMessage = "Configure your Navidrome server to start.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private double playbackPosition;

    [ObservableProperty]
    private double playbackDuration;

    [ObservableProperty]
    private string playbackPositionText = "0:00";

    [ObservableProperty]
    private string playbackDurationText = "0:00";

    [ObservableProperty]
    private int volume = 80;

    public MainWindowViewModel()
        : this(new NavidromeClient(), new PlayerService(), new AppSettingsService())
    {
    }

    public MainWindowViewModel(
        NavidromeClient navidromeClient,
        PlayerService playerService,
        AppSettingsService settingsService)
    {
        _navidromeClient = navidromeClient;
        _playerService = playerService;
        _settingsService = settingsService;
        _playerService.EndReached += OnPlayerEndReached;
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionTimer.Tick += (_, _) => UpdatePlaybackPosition();
        _positionTimer.Start();
        _ = LoadSavedConnectionAsync();
    }

    public ObservableCollection<Song> SearchResults { get; } = [];

    public ObservableCollection<Album> Albums { get; } = [];

    public ObservableCollection<Playlist> Playlists { get; } = [];

    public ObservableCollection<Song> PlaybackQueue { get; } = [];

    public bool IsSongsView => CurrentView == LibraryViewKind.Songs;

    public bool IsAlbumsView => CurrentView == LibraryViewKind.Albums;

    public bool IsPlaylistsView => CurrentView == LibraryViewKind.Playlists;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _navidromeClient.PingAsync();
            IsConnected = true;
            await _settingsService.SaveConnectionAsync(CreateConnection());
            StatusMessage = "Connected to Navidrome.";
        });
    }

    [RelayCommand]
    private async Task ClearSavedConnectionAsync()
    {
        await _settingsService.ClearAsync();
        ServerUrl = "";
        Username = "";
        Password = "";
        IsConnected = false;
        SearchResults.Clear();
        Albums.Clear();
        Playlists.Clear();
        PlaybackQueue.Clear();
        CurrentSong = null;
        SelectedSong = null;
        SelectedAlbum = null;
        SelectedPlaylist = null;
        CurrentView = LibraryViewKind.Songs;
        ContentTitle = "Songs";
        StatusMessage = "Saved connection cleared.";
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Songs;
            ContentTitle = "Search results";
            SearchResults.Clear();
            var songs = await _navidromeClient.SearchSongsAsync(SearchQuery);
            ReplaceSongs(songs);
            StatusMessage = songs.Count == 0
                ? "No songs found."
                : $"Found {songs.Count} songs.";
        });
    }

    [RelayCommand]
    private async Task LoadNewestAlbumsAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Albums;
            ContentTitle = "Newest albums";
            Albums.Clear();
            var albums = await _navidromeClient.GetNewestAlbumsAsync();
            foreach (var album in albums)
            {
                Albums.Add(album);
            }

            SelectedAlbum = Albums.FirstOrDefault();
            StatusMessage = albums.Count == 0
                ? "No albums found."
                : $"Loaded {albums.Count} albums.";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedAlbumAsync()
    {
        if (SelectedAlbum is null)
        {
            StatusMessage = "Select an album first.";
            return;
        }

        if (!ConfigureClient())
        {
            return;
        }

        var album = SelectedAlbum;
        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Songs;
            ContentTitle = album.Name;
            var songs = await _navidromeClient.GetAlbumSongsAsync(album.Id);
            ReplaceSongs(songs);
            StatusMessage = songs.Count == 0
                ? "Album has no songs."
                : $"Loaded {songs.Count} songs from {album.Name}.";
        });
    }

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Playlists;
            ContentTitle = "Playlists";
            Playlists.Clear();
            var playlists = await _navidromeClient.GetPlaylistsAsync();
            foreach (var playlist in playlists)
            {
                Playlists.Add(playlist);
            }

            SelectedPlaylist = Playlists.FirstOrDefault();
            StatusMessage = playlists.Count == 0
                ? "No playlists found."
                : $"Loaded {playlists.Count} playlists.";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusMessage = "Select a playlist first.";
            return;
        }

        if (!ConfigureClient())
        {
            return;
        }

        var playlist = SelectedPlaylist;
        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Songs;
            ContentTitle = playlist.Name;
            var songs = await _navidromeClient.GetPlaylistSongsAsync(playlist.Id);
            ReplaceSongs(songs);
            StatusMessage = songs.Count == 0
                ? "Playlist has no songs."
                : $"Loaded {songs.Count} songs from {playlist.Name}.";
        });
    }

    [RelayCommand]
    private async Task PlaySelectedAsync()
    {
        if (SelectedSong is null)
        {
            StatusMessage = "Select a song first.";
            return;
        }

        if (!ConfigureClient())
        {
            return;
        }

        await PlaySongAsync(SelectedSong);
    }

    [RelayCommand]
    private async Task PlayNextAsync()
    {
        if (PlaybackQueue.Count == 0)
        {
            StatusMessage = "Playback queue is empty.";
            return;
        }

        var nextIndex = _currentQueueIndex < 0
            ? 0
            : (_currentQueueIndex + 1) % PlaybackQueue.Count;

        await PlaySongAsync(PlaybackQueue[nextIndex]);
    }

    [RelayCommand]
    private async Task PlayPreviousAsync()
    {
        if (PlaybackQueue.Count == 0)
        {
            StatusMessage = "Playback queue is empty.";
            return;
        }

        var previousIndex = _currentQueueIndex <= 0
            ? PlaybackQueue.Count - 1
            : _currentQueueIndex - 1;

        await PlaySongAsync(PlaybackQueue[previousIndex]);
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        _playerService.Pause();
        IsPlaying = _playerService.IsPlaying;
        StatusMessage = IsPlaying ? "Playback resumed." : "Playback paused.";
    }

    [RelayCommand]
    private void Stop()
    {
        _playerService.Stop();
        IsPlaying = false;
        PlaybackPosition = 0;
        PlaybackDuration = 0;
        PlaybackPositionText = "0:00";
        PlaybackDurationText = "0:00";
        StatusMessage = "Playback stopped.";
    }

    public string? GetCoverArtUrl(Song song)
    {
        return _navidromeClient.GetCoverArtUrl(song.CoverArt);
    }

    public string? GetAlbumCoverArtUrl(Album album)
    {
        return _navidromeClient.GetCoverArtUrl(album.CoverArt);
    }

    public string? GetPlaylistCoverArtUrl(Playlist playlist)
    {
        return _navidromeClient.GetCoverArtUrl(playlist.CoverArt);
    }

    private bool ConfigureClient()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Server URL, username, and password are required.";
            return false;
        }

        _navidromeClient.Configure(CreateConnection());
        return true;
    }

    private NavidromeConnection CreateConnection()
    {
        return new NavidromeConnection(ServerUrl, Username, Password);
    }

    partial void OnCurrentViewChanged(LibraryViewKind value)
    {
        OnPropertyChanged(nameof(IsSongsView));
        OnPropertyChanged(nameof(IsAlbumsView));
        OnPropertyChanged(nameof(IsPlaylistsView));
    }

    private void ReplaceSongs(IReadOnlyList<Song> songs)
    {
        SearchResults.Clear();
        foreach (var song in songs)
        {
            SearchResults.Add(song);
        }

        PlaybackQueue.Clear();
        foreach (var song in songs)
        {
            PlaybackQueue.Add(song);
        }

        _currentQueueIndex = PlaybackQueue.Count > 0 ? 0 : -1;
        SelectedSong = PlaybackQueue.FirstOrDefault();
    }

    partial void OnVolumeChanged(int value)
    {
        _playerService.Volume = value;
    }

    partial void OnPlaybackPositionChanged(double value)
    {
        if (_isUpdatingPlayerPosition)
        {
            return;
        }

        PlaybackPositionText = FormatDuration(TimeSpan.FromSeconds(value));
    }

    private async Task PlaySongAsync(Song song)
    {
        if (!ConfigureClient())
        {
            return;
        }

        if (PlaybackQueue.Count == 0)
        {
            PlaybackQueue.Add(song);
        }

        _currentQueueIndex = PlaybackQueue.IndexOf(song);
        if (_currentQueueIndex < 0)
        {
            PlaybackQueue.Add(song);
            _currentQueueIndex = PlaybackQueue.Count - 1;
        }

        SelectedSong = song;
        _playerService.Volume = Volume;
        _playerService.PlayUrl(_navidromeClient.GetStreamUrl(song.Id));
        CurrentSong = song;
        IsPlaying = true;
        PlaybackPosition = 0;
        PlaybackDuration = song.DurationSeconds ?? 0;
        PlaybackPositionText = "0:00";
        PlaybackDurationText = FormatDuration(TimeSpan.FromSeconds(PlaybackDuration));
        StatusMessage = $"Playing {song.Title}.";

        try
        {
            await _navidromeClient.ScrobbleAsync(song.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playing {song.Title}. Scrobble failed: {ex.Message}";
        }
    }

    private async void OnPlayerEndReached(object? sender, EventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            IsPlaying = false;
            await PlayNextAsync();
        });
    }

    private void UpdatePlaybackPosition()
    {
        if (!IsPlaying)
        {
            return;
        }

        var position = _playerService.Position;
        var duration = _playerService.Duration;

        _isUpdatingPlayerPosition = true;
        PlaybackPosition = position.TotalSeconds;
        if (duration.TotalSeconds > 0)
        {
            PlaybackDuration = duration.TotalSeconds;
            PlaybackDurationText = FormatDuration(duration);
        }

        PlaybackPositionText = FormatDuration(position);
        _isUpdatingPlayerPosition = false;
        IsPlaying = _playerService.IsPlaying;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        return $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }

    private async Task LoadSavedConnectionAsync()
    {
        try
        {
            var connection = await _settingsService.LoadConnectionAsync();
            if (connection is null)
            {
                return;
            }

            ServerUrl = connection.BaseUrl;
            Username = connection.Username;
            Password = connection.Password;
            _navidromeClient.Configure(connection);
            StatusMessage = "Loaded saved Navidrome connection.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load saved settings: {ex.Message}";
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _playerService.EndReached -= OnPlayerEndReached;
        _playerService.Dispose();
    }
}
