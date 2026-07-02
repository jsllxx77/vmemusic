using System.Collections.ObjectModel;
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
    private Song? currentSong;

    [ObservableProperty]
    private string statusMessage = "Configure your Navidrome server to start.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isPlaying;

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
        _ = LoadSavedConnectionAsync();
    }

    public ObservableCollection<Song> SearchResults { get; } = [];

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
        CurrentSong = null;
        SelectedSong = null;
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
            SearchResults.Clear();
            var songs = await _navidromeClient.SearchSongsAsync(SearchQuery);
            foreach (var song in songs)
            {
                SearchResults.Add(song);
            }

            StatusMessage = songs.Count == 0
                ? "No songs found."
                : $"Found {songs.Count} songs.";
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

        var song = SelectedSong;
        _playerService.PlayUrl(_navidromeClient.GetStreamUrl(song.Id));
        CurrentSong = song;
        IsPlaying = true;
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
        StatusMessage = "Playback stopped.";
    }

    public string? GetCoverArtUrl(Song song)
    {
        return _navidromeClient.GetCoverArtUrl(song.CoverArt);
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
        _playerService.Dispose();
    }
}
