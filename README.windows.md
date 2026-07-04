# codexU for Windows

This is a lightweight Windows implementation of the codexU desktop widget. It mirrors the macOS app's core workflow: read local Codex quota and session usage, show a small desktop widget, and keep a tray icon for refresh/show/hide.

## Requirements

- Windows 10 or later.
- Local Codex installed and signed in.
- `.NET Framework 4.x` compiler tools for building from source. A standard Windows installation often has `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
- `winsqlite3.dll`, which is included with supported Windows versions.

## Build

```powershell
.\scripts\build-windows.ps1
```

The executable is written to:

```text
dist\codexU-win.exe
```

## Run

```powershell
.\dist\codexU-win.exe
```

Diagnostics:

```powershell
.\dist\codexU-win.exe --dump-json
```

## Data Sources

- Quota: `codex app-server` JSON-RPC methods `account/read` and `account/rateLimits/read`.
- Thread index and task counts: `%USERPROFILE%\.codex\state_5.sqlite`.
- Token usage: `token_count` events from the rollout paths recorded in the SQLite `threads` table. The parser follows the macOS implementation and only counts `payload.info.total_token_usage`.
- Scheduled tasks: enabled automation metadata discovered under `%USERPROFILE%\.codex\automations\**\automation.toml`.
- Hotkey: `Ctrl + Alt + U` toggles the widget.

The Windows widget reads local data only. It does not upload Codex usage, threads, or account data to third-party services.
