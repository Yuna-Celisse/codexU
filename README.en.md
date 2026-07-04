# codexU

codexU is a desktop widget for tracking OpenAI Codex / ChatGPT Codex quota, token usage, and today's task status. It keeps the information you check most on the desktop, so you can quickly see remaining quota, reset times, and daily work progress.

> **Upstream**: [shanggqm/codexU](https://github.com/shanggqm/codexU) — original macOS version  
> **This fork**: [Yuna-Celisse/codexUWin](https://github.com/Yuna-Celisse/codexUWin) — adds Windows (Qt/QML) port

![codexU desktop widget screenshot](docs/screenshot-0.2.0.png)

## Platform Support

| Platform | Stack | Status |
|----------|-------|--------|
| macOS | Swift + SwiftUI | Upstream original, stable |
| Windows | C++ + Qt 6 QML | This fork, usable |

## Features

- Displays Codex 5-hour and 7-day quota remaining percentage and reset times.
- Summarizes today, last 7 days, and lifetime token usage with uncached input, cached input, and output breakdowns.
- Estimates monthly API-equivalent value from OpenAI API token prices and shows progress against Plus ($20), Pro 100 ($100), Pro 200 ($200), and full monthly quota.
- Builds a daily task board from local Codex threads and enabled automations (Active / Pending / Scheduled / Done).
- Reads data locally — no usage, thread, or account data uploaded to third-party services.
- Chinese and English UI, auto-selected by system time zone.
- System / light / dark appearance modes (macOS).
- Window pinning (Windows) — locks position and prevents click-to-activate.

## Keyboard Shortcuts & Controls

**macOS**
- `Command + U`: toggle between desktop layer and foreground.
- Menu bar gauge icon: same as `Command + U`.
- Top `中 | EN` switch: toggle Chinese/English.
- Drag anywhere on the widget background to reposition.

**Windows**
- `Ctrl + Alt + U`: show/hide window.
- System tray icon: double-click to toggle, right-click menu for refresh/exit.
- Pin button (锁): locks position, prevents click-to-focus.
- Drag the header area to reposition.

## macOS: Build & Install

### Requirements

- macOS 14 or later.
- Codex installed and signed in.
- Codex used at least once (`~/.codex/state_5.sqlite` must exist).
- Xcode Command Line Tools (for building from source).

### Build from source

```sh
make build    # build
make run      # run
make install  # install to /Applications
make probe    # dump data source JSON for debugging
```

### Package a DMG

```sh
make release         # current architecture
make release-arm64   # Apple Silicon
make release-intel   # Intel
make release-all     # universal
```

See upstream [shanggqm/codexU](https://github.com/shanggqm/codexU) for more details.

## Windows: Build & Install

### Requirements

- Windows 10 or later.
- Qt 6.5+ (MinGW 64-bit).
- Codex installed and signed in.
- Codex used at least once.
- CMake 3.16+ and Ninja (bundled with Qt).

### Build from source

```powershell
# Configure
cmake -S codexU-qml -B codexU-qml/build -G Ninja `
  -DCMAKE_BUILD_TYPE=Release `
  -DCMAKE_PREFIX_PATH="C:\Qt\6.11.1\mingw_64" `
  -DCMAKE_CXX_COMPILER="C:\Qt\Tools\mingw1310_64\bin\g++.exe"

# Build
cmake --build codexU-qml/build --config Release

# Deploy Qt DLLs
windeployqt codexU-qml/build/codexU-qml.exe --qmldir codexU-qml/src/qml

# Copy SQLite driver
mkdir codexU-qml/build/sqldrivers
copy C:\Qt\6.11.1\mingw_64\plugins\sqldrivers\qsqlite.dll codexU-qml/build/sqldrivers\
```

Or use the build script:

```powershell
.\codexU-qml\scripts\build.ps1
```

### Run

```powershell
.\codexU-qml\build\codexU-qml.exe

# Mock data mode (UI preview)
.\codexU-qml\build\codexU-qml.exe --mock

# JSON diagnostic output
.\codexU-qml\build\codexU-qml.exe --dump-json
```

## Value Progress ("Wool Progress")

The "Value Progress" card estimates the API-equivalent dollar value of your monthly Codex usage. It converts locally parsed tokens to USD using OpenAI API token pricing per model, then compares against Plus ($20), Pro 100 ($100), Pro 200 ($200), and the full monthly quota ceiling.

Per-event cost formula:

```text
API-equivalent value =
  uncached input tokens / 1,000,000 × uncached input price
+ cached input tokens / 1,000,000 × cached input price
+ output tokens / 1,000,000 × output price
```

> This is an API-price-based estimate only. It does not represent actual billing or official refund amounts.

## Data Sources

- **Account & quota**: `codex app-server` JSON-RPC (`account/read`, `account/rateLimits/read`, `account/usage/read`)
- **Local token totals**: `~/.codex/state_5.sqlite` (macOS) / `%USERPROFILE%\.codex\state_5.sqlite` (Windows)
- **Detailed token splits**: `token_count` events from sessions/archived_sessions JSONL files
- **Task board**: unarchived and today-archived Codex threads in SQLite
- **Scheduled tasks**: `automation.toml` under `~/.codex/automations/`

The current Codex quota API exposes rolling-window percentages and reset times, not absolute quota sizes. See upstream [RESEARCH.md](https://github.com/shanggqm/codexU/blob/main/RESEARCH.md) for the full data model.

## FAQ

### Is codexU an official OpenAI product?

No. codexU is an unofficial local utility that reads local Codex data.

### Does codexU upload my data?

No. codexU reads data locally only and does not upload to any third-party service.

### Does the Windows version have all macOS features?

The Windows (Qt/QML) version implements the core features — quota rings, token cards, value progress, and task board. Some advanced features (session cache acceleration, daily trend chart, environment diagnostics) are currently macOS-only.

## Contributing

- Upstream macOS version: [shanggqm/codexU](https://github.com/shanggqm/codexU)
- Windows port and other modifications: [Yuna-Celisse/codexUWin](https://github.com/Yuna-Celisse/codexUWin)

Issues and pull requests welcome.

## License

MIT. See [LICENSE](LICENSE).

## WeChat Official Account

Scan the QR code to follow the original author's WeChat official account for AI tools, Codex usage notes, and independent product building.

<img src="docs/wechat-official-account-qr.png" alt="WeChat official account QR code" width="220" />
