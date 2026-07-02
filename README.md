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

## Settings

VmeMusic stores the last successful Navidrome connection in the user app data folder.
On Windows, the password is protected with DPAPI for the current user.
On non-Windows development environments, the password is stored with a `plain:` marker for local testing only.
