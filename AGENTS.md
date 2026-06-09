# AGENTS.md

## Project

ElectroScroll is a Windows WPF/.NET 9 prototype for velocity-aware mouse wheel
acceleration. Its goal is to approximate the feel of an electromagnetic
free-spin wheel while preserving precise single-notch scrolling.

## Build

Use PowerShell from `M:\Source\ElectroScroll`:

```powershell
dotnet build .\ElectroScroll.csproj -c Release -o M:\Source\ElectroScroll\bin\Release
```

The expected executable is:

```text
M:\Source\ElectroScroll\bin\Release\ElectroScroll.exe
```

To produce a GitHub Releases asset:

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

The zip is created under `artifacts/`, which is intentionally git-ignored.

## Safety Notes

- Output is `PostMessage` only. Do not reintroduce `SendInput` unless the user
  explicitly asks for a separate experimental branch.
- Avoid heavy work in the low-level mouse hook path.
- Do not enable diagnostic charts by default. When disabled, charts should not
  collect samples or rebuild UI points.
- Preserve modifier, fullscreen, and known-game bypass behavior unless the user
  explicitly asks to change it.
- Keep reverse-direction braking immediate: a wheel reversal during inertia
  should cancel active glide before starting any opposite-direction motion.
- For recovery or UI-only debugging, start with `--no-hook`.

## UI And Language

- The settings UI supports English and Traditional Chinese.
- New visible strings should go through `ViewModels/LocalizedText.cs`.
- Slider help text should describe what happens when a value is raised or
  lowered.

## Tuning Priorities

- `Precise` should keep slow or single-notch scrolling native-feeling.
- Browser profiles should avoid triggering inertia too easily.
- Built-in profiles are seeds. If a profile already exists in `settings.json`,
  preserve the user's tuning and process list unless a value is missing/null.
- Codex/Desktop WebView profiles may need stronger inertia and root-window
  output for multi-monitor behavior.
- Performance changes should be verified with the app running in normal hook mode.
