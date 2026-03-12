# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ToyBoxx is a lightweight Windows media player built with C# 13, WPF, and .NET 9. It supports video playback with transform controls (zoom, rotate, pan), segment looping, speed adjustment, and screenshot capture.

## Build & Run

```powershell
# First-time setup: download FFmpeg and SoundTouch DLLs
powershell ./requirements.ps1

# Build and publish (Release, x64)
dotnet publish ./ToyBoxx/ToyBoxx.csproj -c Release -r win-x64 -p:PublishReadyToRun=true

# Run from Visual Studio or:
dotnet run --project ./ToyBoxx/ToyBoxx.csproj
```

There are no automated tests in this project.

## Architecture

**MVVM with Microsoft.Extensions.Hosting DI**, using FFME.Windows for FFmpeg-backed media playback.

### Startup flow
`App.xaml.cs` builds an `IHost` with DI, then `ApplicationHostService` (an `IHostedService`) initializes FFmpeg, applies the theme from `appsettings.json`, and shows `MainWindow`.

### Key components

- **`MainWindow.xaml.cs`** — Handles all keyboard shortcuts (Space=play/pause, R=rotate, F=fit, S=screenshot, arrows=seek), mouse wheel zoom, drag-to-pan, double-click fullscreen, drag-and-drop open, and auto-hides the controller panel after 3s of inactivity.

- **`RootViewModel`** — Owns the media element instances (main + preview via `MediaElementProvider`), transform state (scale/rotation/translation), window title, and top-level commands (Open, Close, Play, Pause, Seek, Capture, SegmentLoop, SpeedRatio).

- **`ControllerViewModel`** — Manages the playback control panel state: button visibility, segment loop endpoints, speed ratio, and restores user preferences (volume, looping, window position) from `Properties/Settings`.

- **`ControllerPanelControl`** — The bottom control bar; includes a thumbnail preview that appears when hovering over the seek bar.

- **`ApplicationHostService`** — Initializes FFmpeg hardware acceleration (CUDA/D3D11VA/DXVA2), applies WPF-UI theme, configures window title/icon.

### Foundation utilities

- **`DelegateCommand`** — Thread-safe `ICommand` with async support and execution-state tracking. Prevents re-entrancy via `Monitor.TryEnter`.
- **`ReactiveExtensions.WhenChanged()`** — Lightweight reactive binding: subscribes to `INotifyPropertyChanged` and returns an `IObservable`-like chain without RxJava.
- **`WindowStatus`** — Captures/restores window position and suppresses screen timeout via P/Invoke (`SetThreadExecutionState`).

### Configuration

- `appsettings.json` — FFmpeg binary path and application theme (`Dark`/`Light`/`HighContrast`).
- `Properties/Settings.settings` — Persisted user preferences (window bounds, volume, loop behavior).
