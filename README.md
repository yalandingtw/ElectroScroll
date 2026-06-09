# ElectroScroll

[繁體中文](README.zh-TW.md)

ElectroScroll is an experimental Windows utility that adds velocity-aware mouse
wheel acceleration with a flywheel-like inertial tail. It was built for users who
like the fast free-spin feel of devices such as the Logitech MX Master series,
but want to approximate that behavior on a normal notched wheel.

The design goal is simple: slow, single-notch scrolling should stay precise;
fast wheel bursts should glide farther and decay smoothly.

## Status

ElectroScroll is a prototype. It works by installing a user-level low-level mouse
hook, detecting wheel speed, and posting synthetic `WM_MOUSEWHEEL` messages to
the target window. It does not use `SendInput`.

Use it carefully while tuning. Some applications handle synthetic wheel messages
better than others.

## Features

- Precise low-speed scrolling: slow wheel input is passed through unchanged.
- Velocity trigger: ElectroScroll only intercepts once wheel speed crosses a
  configurable threshold.
- Flywheel inertia: fast bursts become smooth decaying output packets.
- Presets: `Precise`, `Balanced`, and `Free-spin`.
- App profiles: built-in profiles for browsers and Codex/ChatGPT-style desktop
  WebView apps, with more profiles configurable through `settings.json`.
- Reverse brake: scrolling in the opposite direction during inertia cancels the
  glide immediately.
- Modifier bypass: Ctrl/Shift/Alt/Win wheel gestures stay native.
- Fullscreen and game bypass: known game processes and fullscreen windows are
  bypassed by default.
- Multi-monitor handling: output can target the root window on non-primary
  monitors to avoid child-window and DPI mismatches.
- Performance mode: requests 1 ms timer resolution, disables EcoQoS execution
  throttling when supported, and uses low-latency GC.
- Bilingual UI: English and Traditional Chinese.
- Optional monitor chart: input and output signal charts are off by default and
  only collect samples when enabled.
- Tray support and single-instance startup.

## Requirements

- Windows 10 or Windows 11.
- .NET 9 SDK to build from source.
- .NET 9 Desktop Runtime to run a framework-dependent build.

## Build

From the repository root:

```powershell
dotnet build .\ElectroScroll.csproj -c Release -o .\bin\Release
```

Run:

```powershell
.\bin\Release\ElectroScroll.exe
```

Safe UI-only mode, without installing the global mouse hook:

```powershell
.\bin\Release\ElectroScroll.exe --no-hook
```

## Packaged Release

To create a self-contained Windows x64 zip for GitHub Releases:

```powershell
.\scripts\package-release.ps1 -Version 0.1.1
```

The package is written to:

```text
artifacts\ElectroScroll-0.1.1-win-x64.zip
```

The zip contains the single-file executable plus `README.md`,
`README.zh-TW.md`, and `LICENSE`.

## Usage

1. Start `ElectroScroll.exe`.
2. Move the cursor over the app you want to scroll.
3. Try the `Precise` preset first.
4. Use `Balanced` or `Free-spin` if you want a stronger inertial tail.
5. Click `Save` after tuning.

The status cards show the current speed, boost, velocity, target process, target
window, and whether ElectroScroll is currently using `Native`, `Intercepting`, or
`Bypassed` behavior.

## Settings

Settings are saved here:

```text
%APPDATA%\ElectroScroll\settings.json
```

The UI currently edits the default tuning profile. Advanced users can edit
`settings.json` to adjust process-specific profiles or known game process names.
Close ElectroScroll before editing the file manually.

## Diagnostics

The `Log` checkbox enables low-overhead file diagnostics. It is off by default.
When enabled, ElectroScroll writes wheel input, target resolution, bypass reasons,
profile decisions, output packets, and `PostMessage` results here:

```text
%APPDATA%\ElectroScroll\logs\electroscroll.log
```

The log is batched on a background timer and rotates to `electroscroll.1.log`
after it reaches the configured size.

## Tuning Guide

- `Step`: base distance per wheel tick. Higher values move farther even before
  inertia starts.
- `Threshold`: wheel speed required before ElectroScroll intercepts. Raise it if
  inertia triggers too easily.
- `Acceleration`: how strongly extra wheel speed becomes boost after crossing the
  threshold.
- `Max boost`: hard cap for acceleration.
- `Impulse time`: how quickly a burst becomes inertial velocity. Lower values feel
  more immediate.
- `Friction`: how long inertia takes to fade.
- `Flywheel`: extra tail after a fast burst. Higher values feel closer to
  free-spin.
- `Direct share`: how much of a burst is applied immediately instead of becoming
  inertia.
- `Smoothness`: how many smaller wheel packets are emitted. Lower this toward `1`
  for older applications that ignore small high-resolution packets.

## Performance And Safety

ElectroScroll keeps the hook path small:

- root window process/title lookups are cached by HWND;
- physics runs on a 4 ms timer when motion is active;
- UI metrics are throttled;
- diagnostic charts and file logging are disabled by default;
- injected low-level wheel events from other software are bypassed by the hook;
- generated output uses `PostMessage` only.

If scrolling feels wrong, uncheck `Enabled`, exit from the tray icon, or start the
app with `--no-hook`.

## Known Limitations

- No installer is provided yet.
- The UI only edits the default profile, although per-app profile matching exists.
- Some applications do not respond well to synthetic `WM_MOUSEWHEEL` messages.
- Elevated/admin windows may require ElectroScroll to run with matching
  privileges.
- Raw Input/HID per-device handling is not implemented.
- High-refresh output uses a timer, not display vsync.
- Game bypass is best-effort. Keep bypass enabled for games.

## Not Affiliated

ElectroScroll is not affiliated with Logitech, Microsoft, OpenAI, or any mouse or
application vendor mentioned in this repository.

## Contributing

Issues and experiments are welcome. When changing scroll behavior, please include:

- the target application and monitor setup;
- the active preset/profile;
- whether the app was in `Native`, `Intercepting`, or `Bypassed` mode;
- a short description of the wheel motion that felt wrong.
- the diagnostic log if `Log` was enabled while reproducing the issue.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
