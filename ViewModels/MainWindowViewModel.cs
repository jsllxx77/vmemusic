using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VmeMusic.Converters;
using VmeMusic.Models;
using VmeMusic.Services;

namespace VmeMusic.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly NavidromeClient _navidromeClient;
    private readonly PlayerService _playerService;
    private readonly AppSettingsService _settingsService;
    private readonly CoverArtCacheService _coverArtCacheService;
    private readonly DispatcherTimer _positionTimer;
    private bool _isUpdatingPlayerPosition;
    private int _currentQueueIndex = -1;
    private string? _activeServerId;

    [ObservableProperty]
    private string serverUrl = "";

    [ObservableProperty]
    private string username = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string serverProfileName = "";

    [ObservableProperty]
    private string coverArtCacheDirectory = "";

    [ObservableProperty]
    private string searchQuery = "";

    [ObservableProperty]
    private Song? selectedSong;

    [ObservableProperty]
    private Album? selectedAlbum;

    [ObservableProperty]
    private Artist? selectedArtist;

    [ObservableProperty]
    private Playlist? selectedPlaylist;

    [ObservableProperty]
    private SavedServerProfile? selectedSavedServer;

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
    private bool canSeek;

    [ObservableProperty]
    private string playbackPositionText = "0:00";

    [ObservableProperty]
    private string playbackDurationText = "0:00";

    [ObservableProperty]
    private int volume = 80;

    public MainWindowViewModel()
        : this(new NavidromeClient(), new PlayerService(), new AppSettingsService(), new CoverArtCacheService())
    {
    }

    public MainWindowViewModel(
        NavidromeClient navidromeClient,
        PlayerService playerService,
        AppSettingsService settingsService,
        CoverArtCacheService coverArtCacheService)
    {
        _navidromeClient = navidromeClient;
        _playerService = playerService;
        _settingsService = settingsService;
        _coverArtCacheService = coverArtCacheService;
        CoverArtConverter.CacheService = _coverArtCacheService;
        _playerService.EndReached += OnPlayerEndReached;
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionTimer.Tick += (_, _) => UpdatePlaybackPosition();
        _positionTimer.Start();
        _ = LoadSavedConnectionAsync();
    }

    public ObservableCollection<Song> SearchResults { get; } = [];

    public ObservableCollection<Album> Albums { get; } = [];

    public ObservableCollection<Artist> Artists { get; } = [];

    public ObservableCollection<Playlist> Playlists { get; } = [];

    public ObservableCollection<SavedServerProfile> SavedServers { get; } = [];

    public ObservableCollection<Song> PlaybackQueue { get; } = [];

    public bool IsSongsView => CurrentView == LibraryViewKind.Songs;

    public bool IsAlbumsView => CurrentView == LibraryViewKind.Albums;

    public bool IsArtistsView => CurrentView == LibraryViewKind.Artists;

    public bool IsPlaylistsView => CurrentView == LibraryViewKind.Playlists;

    public bool IsSettingsView => CurrentView == LibraryViewKind.Settings;

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
            await SaveCurrentServerCoreAsync(true);
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
        Artists.Clear();
        Playlists.Clear();
        PlaybackQueue.Clear();
        CurrentSong = null;
        CanSeek = false;
        PlaybackPosition = 0;
        PlaybackDuration = 0;
        PlaybackPositionText = "0:00";
        PlaybackDurationText = "0:00";
        SelectedSong = null;
        SelectedAlbum = null;
        SelectedArtist = null;
        SelectedPlaylist = null;
        SelectedSavedServer = null;
        SavedServers.Clear();
        ServerProfileName = "";
        _activeServerId = null;
        CurrentView = LibraryViewKind.Songs;
        ContentTitle = "Songs";
        StatusMessage = "Saved connection cleared.";
    }

    [RelayCommand]
    private void OpenSettings()
    {
        CurrentView = LibraryViewKind.Settings;
        ContentTitle = "Settings";
        StatusMessage = "Manage saved servers and cache settings.";
    }

    [RelayCommand]
    private async Task SaveCurrentServerAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await SaveCurrentServerCoreAsync(true);
            StatusMessage = "Saved current server profile.";
        });
    }

    [RelayCommand]
    private async Task UseSelectedServerAsync()
    {
        if (SelectedSavedServer is null)
        {
            StatusMessage = "Select a saved server first.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _settingsService.SetActiveServerAsync(SelectedSavedServer.Id);
            _activeServerId = SelectedSavedServer.Id;
            ServerProfileName = SelectedSavedServer.DisplayName;
            ServerUrl = SelectedSavedServer.ServerUrl;
            Username = SelectedSavedServer.Username;
            Password = await LoadServerPasswordAsync(SelectedSavedServer);
            _navidromeClient.Configure(CreateConnection());
            StatusMessage = $"Active server set to {SelectedSavedServer.DisplayName}.";
        });
    }

    [RelayCommand]
    private async Task RemoveSelectedServerAsync()
    {
        if (SelectedSavedServer is null)
        {
            StatusMessage = "Select a saved server first.";
            return;
        }

        var removedId = SelectedSavedServer.Id;
        await RunBusyAsync(async () =>
        {
            await _settingsService.RemoveServerAsync(removedId);
            await LoadSavedServerProfilesAsync();
            if (_activeServerId == removedId)
            {
                var connection = await _settingsService.LoadConnectionAsync();
                if (connection is not null)
                {
                    ApplyConnection(connection, SavedServers.FirstOrDefault());
                }
            }

            StatusMessage = "Removed saved server.";
        });
    }

    [RelayCommand]
    private async Task ClearCoverArtCacheAsync()
    {
        await RunBusyAsync(async () =>
        {
            _coverArtCacheService.Clear();
            await _settingsService.SaveCoverArtCacheDirectoryAsync(CoverArtCacheDirectory);
            StatusMessage = "Cleared cover art cache.";
        });
    }

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        if (!ConfigureClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Artists;
            ContentTitle = "Artists";
            Artists.Clear();
            var artists = await _navidromeClient.GetArtistsAsync();
            foreach (var artist in artists)
            {
                Artists.Add(artist);
            }

            SelectedArtist = Artists.FirstOrDefault();
            StatusMessage = artists.Count == 0
                ? "No artists found."
                : $"Loaded {artists.Count} artists.";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedArtistAsync()
    {
        if (SelectedArtist is null)
        {
            StatusMessage = "Select an artist first.";
            return;
        }

        if (!ConfigureClient())
        {
            return;
        }

        var artist = SelectedArtist;
        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Albums;
            ContentTitle = artist.Name;
            Albums.Clear();
            var albums = await _navidromeClient.GetArtistAlbumsAsync(artist.Id);
            foreach (var album in albums)
            {
                Albums.Add(album);
            }

            SelectedAlbum = Albums.FirstOrDefault();
            StatusMessage = albums.Count == 0
                ? "Artist has no albums."
                : $"Loaded {albums.Count} albums from {artist.Name}.";
        });
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
    private void RemoveSelectedFromQueue()
    {
        if (SelectedSong is null || !PlaybackQueue.Contains(SelectedSong))
        {
            StatusMessage = "Select a queued song first.";
            return;
        }

        var removedIndex = PlaybackQueue.IndexOf(SelectedSong);
        var wasCurrentSong = CurrentSong?.Id == SelectedSong.Id;
        PlaybackQueue.Remove(SelectedSong);

        if (PlaybackQueue.Count == 0)
        {
            _currentQueueIndex = -1;
            SelectedSong = null;
        }
        else
        {
            _currentQueueIndex = Math.Clamp(_currentQueueIndex, 0, PlaybackQueue.Count - 1);
            if (removedIndex <= _currentQueueIndex)
            {
                _currentQueueIndex = Math.Max(0, _currentQueueIndex - 1);
            }

            SelectedSong = PlaybackQueue[Math.Clamp(removedIndex, 0, PlaybackQueue.Count - 1)];
        }

        StatusMessage = wasCurrentSong
            ? "Removed current song from queue. Playback will continue until stopped or changed."
            : "Removed song from queue.";
    }

    [RelayCommand]
    private void ClearQueue()
    {
        PlaybackQueue.Clear();
        _currentQueueIndex = -1;
        SelectedSong = SearchResults.FirstOrDefault();
        StatusMessage = "Playback queue cleared.";
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
        CanSeek = false;
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
        OnPropertyChanged(nameof(IsArtistsView));
        OnPropertyChanged(nameof(IsPlaylistsView));
        OnPropertyChanged(nameof(IsSettingsView));
    }

    partial void OnSelectedSavedServerChanged(SavedServerProfile? value)
    {
        if (value is null)
        {
            return;
        }

        ServerProfileName = value.DisplayName;
        ServerUrl = value.ServerUrl;
        Username = value.Username;
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
        if (CanSeek)
        {
            _playerService.Seek(TimeSpan.FromSeconds(value));
        }
    }

    partial void OnPlaybackDurationChanged(double value)
    {
        CanSeek = value > 0;
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
        try
        {
            _playerService.Volume = Volume;
            _playerService.PlayUrl(_navidromeClient.GetStreamUrl(song.Id));
            CurrentSong = song;
            IsPlaying = true;
            PlaybackPosition = 0;
            PlaybackDuration = song.DurationSeconds ?? 0;
            CanSeek = PlaybackDuration > 0;
            PlaybackPositionText = "0:00";
            PlaybackDurationText = FormatDuration(TimeSpan.FromSeconds(PlaybackDuration));
            StatusMessage = $"Playing {song.Title}.";
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            StatusMessage = $"Could not start playback: {ex.Message}";
            return;
        }

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
            CanSeek = true;
        }

        PlaybackPositionText = FormatDuration(position);
        _isUpdatingPlayerPosition = false;
        IsPlaying = _playerService.IsPlaying;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return DurationFormatter.FormatSeconds((int)Math.Max(0, duration.TotalSeconds));
    }

    private async Task LoadSavedConnectionAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            CoverArtCacheDirectory = settings.CoverArtCacheDirectory;
            _coverArtCacheService.Configure(CoverArtCacheDirectory);
            await LoadSavedServerProfilesAsync(settings);

            var connection = await _settingsService.LoadConnectionAsync();
            if (connection is null)
            {
                return;
            }

            ApplyConnection(connection, SavedServers.FirstOrDefault(server => server.Id == settings.ActiveServerId));
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

    private async Task SaveCurrentServerCoreAsync(bool setActive)
    {
        await _settingsService.SaveServerAsync(_activeServerId, ServerProfileName, CreateConnection(), setActive);
        var settings = await _settingsService.LoadSettingsAsync();
        _activeServerId = settings.ActiveServerId;
        await LoadSavedServerProfilesAsync(settings);
        SelectedSavedServer = SavedServers.FirstOrDefault(server => server.Id == _activeServerId);
    }

    private async Task LoadSavedServerProfilesAsync(AppSettings? settings = null)
    {
        settings ??= await _settingsService.LoadSettingsAsync();
        SavedServers.Clear();
        foreach (var server in settings.Servers.OrderBy(server => server.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            SavedServers.Add(server);
        }

        _activeServerId = settings.ActiveServerId;
        SelectedSavedServer = SavedServers.FirstOrDefault(server => server.Id == _activeServerId)
            ?? SavedServers.FirstOrDefault();
    }

    private void ApplyConnection(NavidromeConnection connection, SavedServerProfile? profile)
    {
        ServerUrl = connection.BaseUrl;
        Username = connection.Username;
        Password = connection.Password;
        ServerProfileName = profile?.DisplayName ?? $"{connection.Username}@{connection.BaseUrl}";
        _activeServerId = profile?.Id;
        _navidromeClient.Configure(connection);
    }

    private async Task<string> LoadServerPasswordAsync(SavedServerProfile profile)
    {
        var settings = await _settingsService.LoadSettingsAsync();
        var connection = await _settingsService.LoadConnectionAsync();
        if (settings.ActiveServerId == profile.Id && connection is not null)
        {
            return connection.Password;
        }

        var protectedPassword = settings.Servers.FirstOrDefault(server => server.Id == profile.Id)?.ProtectedPassword ?? "";
        return new PasswordProtector().Unprotect(protectedPassword);
    }
}
