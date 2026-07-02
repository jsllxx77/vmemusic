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
