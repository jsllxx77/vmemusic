# vmemusic

VmeMusic is an Avalonia desktop client for Navidrome-compatible servers.

## Stack

- C# / .NET 8
- Avalonia UI
- CommunityToolkit.Mvvm
- LibVLCSharp
- Navidrome Subsonic API

## Development

Install .NET 8 SDK, then run:

```bash
dotnet restore
dotnet build
dotnet run
```

The first milestone focuses on connecting to a Navidrome server, searching songs, and playing streams.

Current playback and library features:

- Search songs
- Load newest albums
- Browse artists and open artist albums
- Load playlists
- Open albums and playlists into the playback queue
- Show cover art from Navidrome
- Cache cover art in memory while the app runs
- Remove songs from the playback queue
- Clear the playback queue
- Play, pause, stop, previous, and next
- Track playback progress and volume

## Windows build

GitHub Actions builds a self-contained Windows x64 zip on every push and pull request.
Create a version tag to publish a GitHub Release:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Settings

VmeMusic stores the last successful Navidrome connection in the user app data folder.
On Windows, the password is protected with DPAPI for the current user.
On non-Windows development environments, the password is stored with a `plain:` marker for local testing only.
