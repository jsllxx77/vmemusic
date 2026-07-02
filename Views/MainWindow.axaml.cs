using Avalonia.Controls;
using Avalonia.Input;
using VmeMusic.ViewModels;

namespace VmeMusic.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SongsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.PlaySelectedCommand.CanExecute(null))
        {
            viewModel.PlaySelectedCommand.Execute(null);
        }
    }

    private void AlbumsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.LoadSelectedAlbumCommand.CanExecute(null))
        {
            viewModel.LoadSelectedAlbumCommand.Execute(null);
        }
    }

    private void ArtistsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.LoadSelectedArtistCommand.CanExecute(null))
        {
            viewModel.LoadSelectedArtistCommand.Execute(null);
        }
    }

    private void PlaylistsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.LoadSelectedPlaylistCommand.CanExecute(null))
        {
            viewModel.LoadSelectedPlaylistCommand.Execute(null);
        }
    }
}
