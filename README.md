![Wineel radial application switcher](assets/wineel-readme-hero.png)

# Wineel

Wineel is a fast, native radial application switcher for Windows 10 and 11. Hold `Alt`, press `Tab`, and scroll through open applications without moving the pointer. Wineel can also leave Windows Alt+Tab untouched and open from the latched `Ctrl+Alt+Space` shortcut.

## Highlights

- Native C#/.NET 10 WPF application—no browser runtime or local server.
- No-activate, per-monitor-DPI overlay centered on the pointer's monitor by default, with optional pointer-following placement.
- Instant type-to-search, mouse wheel, repeated Tab, Shift+Tab, arrows, number badges, click, Enter, and Escape controls.
- Application grouping by package identity or canonical executable path, with Space-driven window drill-down and an individual-windows mode.
- Favorites that keep pinned applications at the front of the wheel; toggle them with `Ctrl+P` or right-click.
- MRU ordering from `SetWinEventHook`, Alt+Tab-style filtering, minimized-window restore, and foreground activation fallback.
- Dynamic app icons, icon-derived beam colors, and a neutral acrylic-style wheel surface.
- System tray lifecycle, first-run onboarding, per-user startup, single-instance signaling, versioned settings, rolling local logs, and privacy-safe diagnostics export.
- UI Automation list, selection, and invocation patterns for screen readers and assistive tooling.
- No telemetry, accounts, advertising, analytics, or network communication.

## System requirements

- Windows 10 version 2004 (build 19041) or newer, or Windows 11.
- x64 processor and Windows installation.
- Standard desktop session. Wineel does not require administrator privileges.

The portable build is self-contained; users do not need to install .NET.

## Installation and portable usage

Portable:

1. Download `Wineel-0.1.2-win-x64-portable.zip` or the `Wineel-0.1.2-win-x64-setup.exe` installer from the GitHub release.
2. Extract the entire archive to a writable folder.
3. Run `Wineel.exe`.

Installer:

1. Publish the application with `scripts/publish.ps1`.
2. Compile `installer/Wineel.iss` with Inno Setup 6.
3. Run the generated per-user installer. Elevation is not required.

## First-run setup

Wineel starts in the system tray and opens a concise welcome screen on first launch. The fallback shortcut is available immediately. Alt+Tab replacement is off by default and must be enabled explicitly under **General**. Select **Try Wineel** to open a latched preview without changing Windows Alt+Tab.

Later launches remain in the tray unless **Launch minimized to the system tray** is disabled. Launching Wineel a second time asks the existing process to open Settings.

## Controls

| Control | Action |
|---|---|
| Hold `Alt`, press `Tab` | Open Wineel when replacement is enabled |
| Release both Alt keys | Activate the selected application |
| `Ctrl+Alt+Space` | Open latched mode |
| Mouse wheel down / up | Next / previous application |
| `Tab` / `Shift+Tab` | Next / previous application |
| Arrow keys | Move backward / forward |
| Type letters | Filter open applications by name |
| `Space` | Open a grouped application and choose a specific window |
| `Backspace` | Erase search text, or return from the window list |
| `Ctrl+P` or right-click | Pin or unpin the selected application |
| `1`–`9`, `0` | Select the corresponding visible badge |
| `Enter` | Activate selection |
| Click an icon | Activate it immediately |
| `Escape` | Cancel and keep the original application |

## Settings

- **General:** Alt+Tab replacement, configurable fallback shortcut, startup, tray launch, pause, grouping mode, current virtual desktop, and full-screen suppression.
- **Appearance:** monitor-centered or pointer-following placement, wheel and icon size, 4–12 visible icons, plate opacity, beam intensity, animation speed, labels, badges, theme, and reduced motion.
- **Input:** wheel direction, repeated Tab, mouse click selection, wrapping, and reset.
- **Favorites:** review and remove pinned application identities.
- **Exclusions:** executable paths or stable application identities, including the currently active application.

Settings are saved automatically to `%LocalAppData%\Wineel\settings.json`. A corrupt file is renamed with a `.corrupt-<timestamp>.json` suffix before defaults are restored. Diagnostic logs are kept under `%LocalAppData%\Wineel\Logs` for seven days; window titles and raw keystrokes are never logged.

Choose **Export diagnostics…** from the tray menu to create a reviewable ZIP. It excludes window titles, raw keystrokes, executable paths, names of pinned/excluded applications, the Windows user name, and the machine name.

## Build and test from source

Install the .NET 10 SDK on Windows, then run:

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

The solution targets `net10.0-windows10.0.19041.0` and x64. The test suite covers radial geometry, mixed-DPI conversion and monitor clamping, precision-wheel accumulation, wraparound, switching state, cancellation and commit, MRU ordering, grouping, type-to-search ranking, favorites, filter predicates, long viewports, badges, settings persistence/recovery/migration, shortcut parsing, and dominant-color selection. The release smoke matrix is documented in [`docs/compatibility-matrix.md`](docs/compatibility-matrix.md).

## Publishing

```powershell
./scripts/publish.ps1
```

Outputs:

- Self-contained application: `artifacts/publish/win-x64/Wineel.exe`
- Portable archive: `artifacts/Wineel-0.1.2-win-x64-portable.zip`
- Per-user installer: run `./scripts/build-installer.ps1` after publishing; output is `artifacts/installer/Wineel-0.1.2-win-x64-setup.exe`
- SHA-256 hashes: `./scripts/checksums.ps1`
- WinGet singleton manifest: `./scripts/generate-winget.ps1`

`Directory.Build.props` is the single source of truth for the application version. Windows CI restores, builds, tests, publishes, and uploads the portable archive. A `vMAJOR.MINOR.PATCH` tag triggers the release workflow, which validates the tag, produces the portable ZIP and Inno Setup installer, generates checksums and a WinGet manifest, and attaches them to the GitHub release.

Release signing is optional and secret-driven. Add repository secrets `WINEEL_SIGN_CERT_BASE64` (the base64-encoded PFX) and `WINEEL_SIGN_CERT_PASSWORD` to Authenticode-sign and verify both executables. Without those secrets the workflow deliberately produces clearly unsigned artifacts; no private certificate material belongs in the repository. After the first generated manifest is accepted into `microsoft/winget-pkgs`, users can install with `winget install --id yappologistic.Wineel --exact`.

## Windows limitations

- Wineel intentionally does not run on the UAC secure desktop. Standard non-elevated Wineel cannot reliably switch to some elevated windows because Windows integrity boundaries restrict foreground interaction.
- Windows ultimately controls `SetForegroundWindow`; Wineel uses a narrowly scoped thread-input fallback only in direct response to user input and always detaches in `finally`.
- Exclusive full-screen games, protected video surfaces, anti-cheat software, and apps using unusual owner/tool-window arrangements may not participate. Full-screen suppression is enabled by default.
- Windows does not expose the complete historical Alt+Tab MRU order to new processes. Wineel tracks accurate foreground changes after it starts; the first session after launch may use a stable fallback order for applications not yet observed.
- Packaged app icons depend on the icon exposed by the top-level window when a direct shell icon is unavailable.
- Mixed-DPI behavior is implemented with Per-Monitor V2 awareness and physical monitor positioning; the automated suite validates the conversion and clamp math, while each real monitor topology should still receive a practical visual check.

Compatibility claims in this README are limited to the included automated tests and the manual smoke checks recorded for each release.

## Troubleshooting

**Native Alt+Tab still appears:** confirm **Replace Windows Alt+Tab** is enabled and Wineel is not paused. Full-screen suppression may also be active. Exit duplicate development builds before retrying.

**The fallback shortcut does nothing:** another app may own the shortcut. Change it in Settings; Wineel shows a tray notification when `RegisterHotKey` fails.

**The wheel has no icons:** some protected or unusual windows do not expose icons. Wineel uses its generated fallback icon instead.

**A window cannot come forward:** restore the target manually once, check whether it is elevated, and inspect `%LocalAppData%\Wineel\Logs`.

**Hooks behave unexpectedly:** use the tray menu to pause and resume Wineel. Exiting the process removes all low-level hooks; process termination also removes them automatically because hooks are owned by Wineel's process.

## Complete uninstall

1. Exit Wineel from the tray.
2. Uninstall it from Windows **Installed apps**, or delete the extracted portable folder.
3. If necessary, disable **Start with Windows** first; the installer also removes the per-user startup value.
4. To remove settings and logs, delete `%LocalAppData%\Wineel`.

Wineel never installs a service, kernel driver, browser component, or code in another process.
