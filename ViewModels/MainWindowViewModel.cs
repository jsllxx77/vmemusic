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
        : this(new NavidromeClient(), new PlayerService())
    {
    }

    public MainWindowViewModel(NavidromeClient navidromeClient, PlayerService playerService)
    {
        _navidromeClient = navidromeClient;
        _playerService = playerService;
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
            StatusMessage = "Connected to Navidrome.";
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

        _navidromeClient.Configure(new NavidromeConnection(ServerUrl, Username, Password));
        return true;
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
