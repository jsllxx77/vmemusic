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
    private NavidromeConnection? _activeConnection;
    private string? _activeServerId;
    private string? _editingServerId;

    [ObservableProperty]
    private string serverUrl = "";

    [ObservableProperty]
    private string username = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string serverProfileName = "";

    [ObservableProperty]
    private string activeServerName = "未选择服务器";

    [ObservableProperty]
    private string activeServerUrl = "";

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
    private bool recentSidebarSelected;

    [ObservableProperty]
    private LibraryViewKind currentView = LibraryViewKind.Home;

    [ObservableProperty]
    private string contentTitle = "首页";

    [ObservableProperty]
    private string statusMessage = "请在设置中添加 Navidrome 服务器。";

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

    public ObservableCollection<Album> HomeAlbums { get; } = [];

    public ObservableCollection<Playlist> HomePlaylists { get; } = [];

    public ObservableCollection<Song> RecentlyPlayed { get; } = [];

    public ObservableCollection<SavedServerProfile> SavedServers { get; } = [];

    public ObservableCollection<Song> PlaybackQueue { get; } = [];

    public bool IsHomeView => CurrentView == LibraryViewKind.Home;

    public bool IsSongsView => CurrentView == LibraryViewKind.Songs;

    public bool IsAlbumsView => CurrentView == LibraryViewKind.Albums;

    public bool IsArtistsView => CurrentView == LibraryViewKind.Artists;

    public bool IsPlaylistsView => CurrentView == LibraryViewKind.Playlists;

    public bool IsSettingsView => CurrentView == LibraryViewKind.Settings;

    public bool IsNowPlayingView => CurrentView == LibraryViewKind.NowPlaying;

    public bool HasSelectedSavedServer => SelectedSavedServer is not null;

    public bool HasActiveServer => _activeConnection is not null;

    public bool IsQueueSidebar => !RecentSidebarSelected;

    public bool IsRecentSidebar => RecentSidebarSelected;

    public string SidebarTitle => RecentSidebarSelected ? "最近播放" : "播放队列";

    public bool IsEditingSavedServer => !string.IsNullOrWhiteSpace(_editingServerId);

    public string ConnectionFormTitle => IsEditingSavedServer ? "编辑服务器" : "新建服务器";

    public string ConnectionFormHint => IsEditingSavedServer
        ? "正在编辑已保存的服务器，保存后会更新并启用该配置。"
        : "正在新建服务器配置，填写完成后点击“保存并启用”。";

    public string SaveServerButtonText => IsEditingSavedServer ? "更新并启用" : "保存并启用";

    public string PlaybackToggleToolTip => IsPlaying ? "暂停" : "继续播放";

    public string CurrentSongSubtitle => CurrentSong is null
        ? "还没有播放歌曲"
        : $"{CurrentSong.Artist} · {CurrentSong.Album}";

    public string CurrentSongLyrics => CurrentSong is null
        ? "播放歌曲后，这里会显示歌词、专辑和播放信息。"
        : string.Join(
            "\n\n",
            new[]
            {
                CurrentSong.Title,
                $"{CurrentSong.Artist} · {CurrentSong.Album}",
                "歌词接口尚未接入。",
                "后续可以在这里展示歌词、制作信息和更完整的播放详情。"
            });

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!ConfigureClientFromForm())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _navidromeClient.PingAsync();
            IsConnected = true;
            await SaveCurrentServerCoreAsync(true);
            StatusMessage = "已连接到 Navidrome。";
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
        HomeAlbums.Clear();
        HomePlaylists.Clear();
        RecentlyPlayed.Clear();
        ActiveServerName = "未选择服务器";
        ActiveServerUrl = "";
        ServerProfileName = "";
        SetActiveConnection(null);
        _activeServerId = null;
        _editingServerId = null;
        OnPropertyChanged(nameof(IsEditingSavedServer));
        OnPropertyChanged(nameof(ConnectionFormTitle));
        OnPropertyChanged(nameof(ConnectionFormHint));
        OnPropertyChanged(nameof(SaveServerButtonText));
        CurrentView = LibraryViewKind.Settings;
        ContentTitle = "设置";
        StatusMessage = "已清除保存的连接配置。";
    }

    [RelayCommand]
    private void OpenSettings()
    {
        CurrentView = LibraryViewKind.Settings;
        ContentTitle = "设置";
        StatusMessage = "管理服务器、连接信息和缓存。";
    }

    [RelayCommand]
    private async Task OpenHomeAsync()
    {
        if (!EnsureActiveClient())
        {
            CurrentView = LibraryViewKind.Home;
            ContentTitle = "首页";
            StatusMessage = "请先在设置中添加并启用 Navidrome 服务器。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await LoadHomeContentAsync();
            CurrentView = LibraryViewKind.Home;
            ContentTitle = "首页";
            StatusMessage = "首页内容已加载。";
        });
    }

    [RelayCommand]
    private void ShowQueueSidebar()
    {
        RecentSidebarSelected = false;
    }

    [RelayCommand]
    private void ShowRecentSidebar()
    {
        RecentSidebarSelected = true;
    }

    [RelayCommand]
    private void OpenNowPlayingDetails()
    {
        if (CurrentSong is null)
        {
            StatusMessage = "先播放一首歌，再打开歌曲详情。";
            return;
        }

        CurrentView = LibraryViewKind.NowPlaying;
        ContentTitle = "正在播放";
    }

    [RelayCommand]
    private async Task SaveCurrentServerAsync()
    {
        if (!ConfigureClientFromForm())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await SaveCurrentServerCoreAsync(true);
            StatusMessage = "已保存并启用服务器配置。";
        });
    }

    [RelayCommand]
    private void NewServer()
    {
        _editingServerId = null;
        ServerProfileName = "";
        ServerUrl = "";
        Username = "";
        Password = "";
        SelectedSavedServer = null;
        ContentTitle = "设置 · 新建服务器";
        OnPropertyChanged(nameof(IsEditingSavedServer));
        OnPropertyChanged(nameof(ConnectionFormTitle));
        OnPropertyChanged(nameof(ConnectionFormHint));
        OnPropertyChanged(nameof(SaveServerButtonText));
        StatusMessage = "已进入新建服务器模式，请填写连接信息后保存并启用。";
    }

    [RelayCommand]
    private async Task EditSelectedServerAsync()
    {
        if (SelectedSavedServer is null)
        {
            StatusMessage = "请先选择一个已保存的服务器。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await LoadFormFromProfileAsync(SelectedSavedServer);
            ContentTitle = "设置 · 编辑服务器";
            StatusMessage = $"正在编辑 {SelectedSavedServer.DisplayName}。";
        });
    }

    [RelayCommand]
    private async Task CancelServerEditAsync()
    {
        var profile = SavedServers.FirstOrDefault(server => server.Id == _activeServerId);
        if (profile is not null)
        {
            await RunBusyAsync(async () =>
            {
                await LoadFormFromProfileAsync(profile);
                ContentTitle = "设置";
                StatusMessage = "已恢复为当前启用的服务器配置。";
            });
            return;
        }

        NewServer();
        ContentTitle = "设置";
        StatusMessage = "已清空连接表单。";
    }

    [RelayCommand]
    private async Task UseSelectedServerAsync()
    {
        if (SelectedSavedServer is null)
        {
            StatusMessage = "请先选择一个已保存的服务器。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _settingsService.SetActiveServerAsync(SelectedSavedServer.Id);
            _activeServerId = SelectedSavedServer.Id;
            await LoadFormFromProfileAsync(SelectedSavedServer);
            SetActiveServerSummary(SelectedSavedServer.DisplayName, SelectedSavedServer.ServerUrl);
            SetActiveConnection(CreateConnection());
            _navidromeClient.Configure(_activeConnection!);
            await LoadHomeContentAsync();
            CurrentView = LibraryViewKind.Home;
            ContentTitle = "首页";
            StatusMessage = $"已启用服务器 {SelectedSavedServer.DisplayName}。";
        });
    }

    [RelayCommand]
    private async Task RemoveSelectedServerAsync()
    {
        if (SelectedSavedServer is null)
        {
            StatusMessage = "请先选择一个已保存的服务器。";
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
                else
                {
                    ActiveServerName = "未选择服务器";
                    ActiveServerUrl = "";
                    SetActiveConnection(null);
                }
            }

            StatusMessage = "已删除服务器配置。";
        });
    }

    [RelayCommand]
    private async Task ClearCoverArtCacheAsync()
    {
        await RunBusyAsync(async () =>
        {
            _coverArtCacheService.Clear();
            await _settingsService.SaveCoverArtCacheDirectoryAsync(CoverArtCacheDirectory);
            StatusMessage = "已清理封面缓存。";
        });
    }

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        if (!EnsureActiveClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Artists;
            ContentTitle = "艺术家";
            Artists.Clear();
            var artists = await _navidromeClient.GetArtistsAsync();
            foreach (var artist in artists)
            {
                Artists.Add(artist);
            }

            SelectedArtist = Artists.FirstOrDefault();
            StatusMessage = artists.Count == 0
                ? "没有找到艺术家。"
                : $"已加载 {artists.Count} 位艺术家。";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedArtistAsync()
    {
        if (SelectedArtist is null)
        {
            StatusMessage = "请先选择一位艺术家。";
            return;
        }

        if (!EnsureActiveClient())
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
                ? "这位艺术家没有专辑。"
                : $"已加载 {artist.Name} 的 {albums.Count} 张专辑。";
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!EnsureActiveClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Songs;
            ContentTitle = "搜索结果";
            SearchResults.Clear();
            var songs = await _navidromeClient.SearchSongsAsync(SearchQuery);
            ReplaceSongs(songs);
            StatusMessage = songs.Count == 0
                ? "没有找到歌曲。"
                : $"找到 {songs.Count} 首歌曲。";
        });
    }

    [RelayCommand]
    private async Task LoadNewestAlbumsAsync()
    {
        if (!EnsureActiveClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Albums;
            ContentTitle = "最新专辑";
            Albums.Clear();
            var albums = await _navidromeClient.GetNewestAlbumsAsync();
            foreach (var album in albums)
            {
                Albums.Add(album);
            }

            SelectedAlbum = Albums.FirstOrDefault();
            StatusMessage = albums.Count == 0
                ? "没有找到专辑。"
                : $"已加载 {albums.Count} 张专辑。";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedAlbumAsync()
    {
        if (SelectedAlbum is null)
        {
            StatusMessage = "请先选择一张专辑。";
            return;
        }

        if (!EnsureActiveClient())
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
                ? "这张专辑没有歌曲。"
                : $"已加载 {album.Name} 的 {songs.Count} 首歌曲。";
        });
    }

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        if (!EnsureActiveClient())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            CurrentView = LibraryViewKind.Playlists;
            ContentTitle = "播放列表";
            Playlists.Clear();
            var playlists = await _navidromeClient.GetPlaylistsAsync();
            foreach (var playlist in playlists)
            {
                Playlists.Add(playlist);
            }

            SelectedPlaylist = Playlists.FirstOrDefault();
            StatusMessage = playlists.Count == 0
                ? "没有找到播放列表。"
                : $"已加载 {playlists.Count} 个播放列表。";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusMessage = "请先选择一个播放列表。";
            return;
        }

        if (!EnsureActiveClient())
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
                ? "这个播放列表没有歌曲。"
                : $"已加载 {playlist.Name} 的 {songs.Count} 首歌曲。";
        });
    }

    [RelayCommand]
    private async Task PlaySelectedAsync()
    {
        if (SelectedSong is null)
        {
            StatusMessage = "请先选择一首歌曲。";
            return;
        }

        if (!EnsureActiveClient())
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
            StatusMessage = "播放队列为空。";
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
            StatusMessage = "播放队列为空。";
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
            StatusMessage = "请先选择队列中的歌曲。";
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
            ? "已从队列移除当前歌曲，播放会持续到停止或切歌。"
            : "已从队列移除歌曲。";
    }

    [RelayCommand]
    private void ClearQueue()
    {
        PlaybackQueue.Clear();
        _currentQueueIndex = -1;
        SelectedSong = SearchResults.FirstOrDefault();
        StatusMessage = "已清空播放队列。";
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        _playerService.Pause();
        IsPlaying = _playerService.IsPlaying;
        StatusMessage = IsPlaying ? "已继续播放。" : "已暂停播放。";
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
        StatusMessage = "已停止播放。";
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

    private bool ConfigureClientFromForm()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "请填写服务器地址、用户名和密码。";
            return false;
        }

        _navidromeClient.Configure(CreateConnection());
        return true;
    }

    private bool EnsureActiveClient()
    {
        if (_activeConnection is null)
        {
            StatusMessage = "请先在设置中添加并启用 Navidrome 服务器。";
            return false;
        }

        _navidromeClient.Configure(_activeConnection);
        return true;
    }

    private NavidromeConnection CreateConnection()
    {
        return new NavidromeConnection(ServerUrl, Username, Password);
    }

    partial void OnCurrentViewChanged(LibraryViewKind value)
    {
        OnPropertyChanged(nameof(IsHomeView));
        OnPropertyChanged(nameof(IsSongsView));
        OnPropertyChanged(nameof(IsAlbumsView));
        OnPropertyChanged(nameof(IsArtistsView));
        OnPropertyChanged(nameof(IsPlaylistsView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsNowPlayingView));
    }

    partial void OnSelectedSavedServerChanged(SavedServerProfile? value)
    {
        OnPropertyChanged(nameof(HasSelectedSavedServer));
    }

    partial void OnCurrentSongChanged(Song? value)
    {
        OnPropertyChanged(nameof(CurrentSongSubtitle));
        OnPropertyChanged(nameof(CurrentSongLyrics));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackToggleToolTip));
    }

    partial void OnRecentSidebarSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsQueueSidebar));
        OnPropertyChanged(nameof(IsRecentSidebar));
        OnPropertyChanged(nameof(SidebarTitle));
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
        if (!EnsureActiveClient())
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
            UpdateRecentlyPlayed(song);
            IsPlaying = true;
            PlaybackPosition = 0;
            PlaybackDuration = song.DurationSeconds ?? 0;
            CanSeek = PlaybackDuration > 0;
            PlaybackPositionText = "0:00";
            PlaybackDurationText = FormatDuration(TimeSpan.FromSeconds(PlaybackDuration));
            StatusMessage = $"正在播放 {song.Title}。";
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            StatusMessage = $"播放失败：{ex.Message}";
            return;
        }

        try
        {
            await _navidromeClient.ScrobbleAsync(song.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"正在播放 {song.Title}。同步播放记录失败：{ex.Message}";
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
                NewServer();
                CurrentView = LibraryViewKind.Home;
                ContentTitle = "首页";
                StatusMessage = "请在左侧设置中添加并启用 Navidrome 服务器。";
                return;
            }

            ApplyConnection(connection, SavedServers.FirstOrDefault(server => server.Id == settings.ActiveServerId));
            await LoadHomeContentAsync();
            CurrentView = LibraryViewKind.Home;
            ContentTitle = "首页";
            StatusMessage = "已加载保存的 Navidrome 连接。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设置失败：{ex.Message}";
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
        await _settingsService.SaveServerAsync(_editingServerId, ServerProfileName, CreateConnection(), setActive);
        var settings = await _settingsService.LoadSettingsAsync();
        _activeServerId = settings.ActiveServerId;
        await LoadSavedServerProfilesAsync(settings);
        SelectedSavedServer = SavedServers.FirstOrDefault(server => server.Id == _activeServerId);
        SetActiveServerSummary(
            SelectedSavedServer?.DisplayName ?? ServerProfileName,
            SelectedSavedServer?.ServerUrl ?? ServerUrl);
        _editingServerId = _activeServerId;
        SetActiveConnection(CreateConnection());
        OnPropertyChanged(nameof(IsEditingSavedServer));
        OnPropertyChanged(nameof(ConnectionFormTitle));
        OnPropertyChanged(nameof(ConnectionFormHint));
        OnPropertyChanged(nameof(SaveServerButtonText));
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
        _editingServerId = profile?.Id;
        SetActiveServerSummary(ServerProfileName, connection.BaseUrl);
        SetActiveConnection(connection);
        OnPropertyChanged(nameof(IsEditingSavedServer));
        OnPropertyChanged(nameof(ConnectionFormTitle));
        OnPropertyChanged(nameof(ConnectionFormHint));
        OnPropertyChanged(nameof(SaveServerButtonText));
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

    private async Task LoadFormFromProfileAsync(SavedServerProfile profile)
    {
        ServerProfileName = profile.DisplayName;
        ServerUrl = profile.ServerUrl;
        Username = profile.Username;
        Password = await LoadServerPasswordAsync(profile);
        _editingServerId = profile.Id;
        OnPropertyChanged(nameof(IsEditingSavedServer));
        OnPropertyChanged(nameof(ConnectionFormTitle));
        OnPropertyChanged(nameof(ConnectionFormHint));
        OnPropertyChanged(nameof(SaveServerButtonText));
    }

    private void SetActiveServerSummary(string name, string url)
    {
        ActiveServerName = string.IsNullOrWhiteSpace(name) ? "未选择服务器" : name;
        ActiveServerUrl = url;
    }

    private void SetActiveConnection(NavidromeConnection? connection)
    {
        _activeConnection = connection;
        OnPropertyChanged(nameof(HasActiveServer));
    }

    private async Task LoadHomeContentAsync()
    {
        var albums = await _navidromeClient.GetNewestAlbumsAsync(size: 8);
        var playlists = await _navidromeClient.GetPlaylistsAsync();

        HomeAlbums.Clear();
        foreach (var album in albums)
        {
            HomeAlbums.Add(album);
        }

        HomePlaylists.Clear();
        foreach (var playlist in playlists.Take(8))
        {
            HomePlaylists.Add(playlist);
        }
    }

    private void UpdateRecentlyPlayed(Song song)
    {
        var existing = RecentlyPlayed.FirstOrDefault(item => item.Id == song.Id);
        if (existing is not null)
        {
            RecentlyPlayed.Remove(existing);
        }

        RecentlyPlayed.Insert(0, song);
        while (RecentlyPlayed.Count > 20)
        {
            RecentlyPlayed.RemoveAt(RecentlyPlayed.Count - 1);
        }
    }
}
