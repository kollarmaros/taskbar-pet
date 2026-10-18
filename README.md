# Taskbar Pet

A small Windows 10/11 app that puts a pixel-art raccoon on your taskbar. The busier your CPU, the more frantic the
raccoon: it sleeps when the PC is idle, wakes up, gets curious, rummages, runs, panics and finally melts down at full
load.

- Shown on the taskbar next to the tray icons (default), as a tray icon, or as its own taskbar button.
- CPU ranges, animations and how quickly the raccoon reacts are all adjustable in a settings window.
- Bring your own animations: drop GIFs into a folder next to the app.
- No admin rights needed, no window, about 1 % of one CPU core.

---

## Contents

- [Installation](#installation)
- [Using the app](#using-the-app)
- [Settings](#settings)
- [Custom animations](#custom-animations)
- [Building from source](#building-from-source)
- [Project structure](#project-structure)
- [How it works](#how-it-works)
- [Troubleshooting](#troubleshooting)

---

## Installation

Download from the [Releases](../../releases) page:

| File | What it is |
|---|---|
| `TaskbarPet-<version>-setup.exe` | **Installer (recommended).** Installs for the current user, no admin rights. Adds a Start-menu entry, an uninstall entry in Settings → Apps, and optionally starts the pet when you sign in. |
| `TaskbarPet-<version>-portable.exe` | The app as a single file. No installation: put it anywhere and run it. |

Both include everything they need (.NET and the animations); nothing else has to be installed. Windows 10 (1809) or
newer, 64-bit.

> **"Windows protected your PC"**: the app isn't code-signed, so Windows SmartScreen warns about it on first run.
> Click **More info → Run anyway**.

**Updating:** run the newer installer; it closes the running pet and replaces it. Settings are kept.

**Uninstalling:** Settings → Apps → Installed apps → *Taskbar Pet* → Uninstall. This also removes the settings and the
autostart entry. For the portable exe: right-click the pet → untick **Start with Windows** → **Exit**, then delete the
file and `%AppData%\TaskbarPet`.

---

## Using the app

| Action | What happens |
|---|---|
| Look at the taskbar | The raccoon animation for the current CPU load. |
| Hover the pet | Tooltip `CPU: 37% (avg 2 s: 41%) · 3_curious` (latest load, average and current animation). |
| Right-click → **Settings…** (or double-click) | Opens the [settings window](#settings). With the *Taskbar button* display, left-click the button for this menu. |
| Right-click → **Show CPU number** | Switches between the animation and the plain CPU number. |
| Right-click → **Start with Windows** | Starts the pet automatically when you sign in. Also listed in Windows Settings → Apps → Startup. |
| Right-click → **Exit** | Closes the app. |

Only one copy runs at a time. If the app was moved or reinstalled elsewhere, the autostart entry is updated the next
time it runs.

### Where the pet is shown

Choose in Settings → **Display → Show in**. Sizes are for 150 % display scaling.

| Option | Size | Menu / settings | Notes |
|---|---|---|---|
| **Taskbar overlay** (default) | ~64 × 64 | right-click / double-click | Drawn on the taskbar just left of the tray icons (gap adjustable). Hidden while a fullscreen app is in front. Main monitor only. |
| **Tray icon** | 24 × 24 | right-click / double-click | Next to the clock. Windows fixes this size. Windows 11 may hide it in the `^` menu at first: drag it onto the taskbar, or turn it on in Settings → Personalization → Taskbar → Other system tray icons. |
| **Taskbar button** | 36 × 36 | left-click the button | Its own button among the app buttons. Also appears in Alt+Tab. Its Windows menu → *Close window* exits the app. |

---

## Settings

Right-click → **Settings…**. **Apply** takes effect immediately, **OK** applies and closes. Every setting has an ⓘ
icon; hover it for an explanation.

| Tab | Setting | Default |
|---|---|---|
| **Display** | Show in: Taskbar overlay / Tray icon / Taskbar button | Taskbar overlay |
| | Overlay position: gap between the overlay and the tray icons (px) | 8 |
| | Show: Animation or CPU number | Animation |
| **CPU & switching** – *Measurement* | Samples per second (1–20) | 5 |
| | Average over (seconds, 0–60): the animation follows this average, not single readings. `0` = every reading. | 2 |
| | Weighting: *Recent samples count more* or *All samples count equally* | Recent count more |
| **CPU & switching** – *Animation switching* | Minimum animation time (seconds): an animation keeps playing at least this long once started. `0` = off. | 3 |
| | Range stickiness: the current range is only left once the average is this many points beyond its edges (e.g. 46–60 % with 5 → moves up above 65 %, down below 41 %) | on, 5 |
| **Animations** | Table of From % / To % / Animation with an animated preview | see below |
| **General** | Start with Windows; version; settings file location | off |

| CPU % | Animation |
|---|---|
| 0–15 | `1_sleep` |
| 16–30 | `2_wake` |
| 31–45 | `3_curious` |
| 46–60 | `4_rummage` |
| 61–75 | `5_run` |
| 76–90 | `6_frantic` |
| 91–100 | `7_meltdown` |

- Ranges are inclusive whole percents and must not overlap. For CPU values in a gap, the CPU number is shown.
- The switching settings exist because CPU load jumps a lot: in a 70-second recording of normal use, a
  plain 2-second average would have switched animations 52 times. A 3-second minimum animation time brought that down
  to 13 without making big changes show up later.

Settings are stored in `%AppData%\TaskbarPet\settings.json`.

---

## Custom animations

The seven raccoon animations are built into the app. To add your own, create a folder named `animations` next to
`TaskbarPet.exe` (for the installed app: `%LOCALAPPDATA%\Programs\TaskbarPet\animations`) and put GIFs into it. They
appear in the Animations tab by file name. A GIF with the same name as a built-in one (e.g. `1_sleep.gif`) replaces it.

- Square GIFs work best (the built-in ones are 256 × 256). They're scaled down to the display size and placed
  bottom-centred.
- Frame durations are taken from the GIF.
- A transparent background lets the taskbar show through in the overlay display.

---

## Building from source

### Prerequisites

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download): `winget install Microsoft.DotNet.SDK.10`
- For the installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php): `winget install JRSoftware.InnoSetup`

### Build and run

```powershell
cd src\TaskbarPet
dotnet build -c Release
.\bin\Release\net10.0-windows\TaskbarPet.exe
```

Windows locks a running exe, so exit the pet before rebuilding (right-click → Exit, or `Stop-Process -Name TaskbarPet`).

### Release build

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Creates in `dist\`:

- `TaskbarPet-<version>-setup.exe` (~37 MB): the installer, from [`installer/TaskbarPet.iss`](installer/TaskbarPet.iss).
- `TaskbarPet-<version>-portable.exe` (~115 MB): self-contained single-file exe, from
  [`win-x64.pubxml`](src/TaskbarPet/Properties/PublishProfiles/win-x64.pubxml).

The version comes from `<Version>` in [`TaskbarPet.csproj`](src/TaskbarPet/TaskbarPet.csproj).

> Single-file compression is deliberately **off**: it would shrink the portable exe to about half, but the app would
> then unpack itself into memory on every start and use more than twice the RAM. The installer is compressed anyway.

### Developing from WSL

If the repository is on the Windows filesystem (e.g. `/mnt/c/...`), it can be edited from WSL and built with the
Windows `dotnet.exe` (the app itself only runs on Windows):

```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"   # not on the WSL PATH by default
cd src/TaskbarPet
powershell.exe -NoProfile -Command "Stop-Process -Name TaskbarPet -ErrorAction SilentlyContinue" < /dev/null
"$DOTNET" build -c Release < /dev/null
powershell.exe -NoProfile -Command "Start-Process '$(wslpath -w bin/Release/net10.0-windows/TaskbarPet.exe)'" < /dev/null
```

> Launch with `powershell.exe Start-Process`, not `cmd.exe /c start`: with `start`, the app inherits the shell's
> output handle and the WSL command hangs until the app exits.

---

## Project structure

```
├── build.ps1                        # release build: installer + portable exe into dist/
├── installer/TaskbarPet.iss         # Inno Setup script
├── animations/<name>/<name>.gif     # built-in animations (embedded in the exe)
└── src/TaskbarPet/
    ├── TaskbarPet.csproj            # .NET 10 WinForms project; embeds animations/*/<name>.gif
    ├── app.ico                      # app icon
    ├── Program.cs                   # entry point, single-instance guard
    ├── TrayApp.cs                   # timers, shared menu, picks the surface
    ├── IPetSurface.cs               # interface: where frames are shown
    ├── OverlaySurface.cs            # … in a layered always-on-top window on the taskbar
    ├── TraySurface.cs               # … as a tray icon
    ├── TaskbarButtonSurface.cs      # … as the icon of a minimized window's taskbar button
    ├── CpuMonitor.cs                # total CPU % via GetSystemTimes
    ├── CpuAverager.cs               # running average (plain or weighted toward recent samples)
    ├── RangeSelector.cs             # picks the CPU range: minimum animation time + stickiness
    ├── IIconRenderer.cs             # interface: "give me the image for this CPU %"
    ├── AnimationRenderer.cs         # plays the GIF for the current range
    ├── NumberRenderer.cs            # draws the CPU % as digits
    ├── AnimationLibrary.cs          # built-in GIFs + optional animations folder
    ├── GifAnimation.cs              # loads a GIF, scales frames, reads frame delays
    ├── AppSettings.cs               # settings.json
    ├── SettingsForm.cs              # settings window
    ├── StartupManager.cs            # "Start with Windows" (HKCU Run key)
    ├── Native.cs                    # Win32 declarations
    └── Properties/PublishProfiles/win-x64.pubxml
```

---

## How it works

```
CpuMonitor ──(5×/s)──► CpuAverager ──► IIconRenderer ──(every frame)──► IPetSurface
                                        ├─ AnimationRenderer             ├─ OverlaySurface
                                        │    RangeSelector (ranges)      ├─ TraySurface
                                        │    GifAnimation (frames)       └─ TaskbarButtonSurface
                                        └─ NumberRenderer
```

- **CpuMonitor** compares idle and total CPU time between calls to `GetSystemTimes` (all cores). Readings less than
  50 ms apart are ignored. Values can differ slightly from Task Manager, which uses a turbo-boost-aware counter.
- **CpuAverager** keeps the running average; **RangeSelector** turns it into a range, applying the minimum animation
  time and range stickiness.
- **AnimationRenderer** loads the range's GIF on first use (via **AnimationLibrary**), scales it to the surface size
  and plays it with the GIF's own frame durations. When the range changes, the new animation continues at the same
  frame number.
- **IPetSurface** implementations only display finished frames, so the three display options share everything else.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Pet not visible | Check it's running (Task Manager → *TaskbarPet*). Tray icon display: look in the `^` menu. Overlay: it hides while a fullscreen app is in front. |
| "Windows protected your PC" | The app isn't code-signed. Click **More info → Run anyway**. |
| Doesn't start at sign-in | Right-click → make sure **Start with Windows** is ticked; check Windows Settings → Apps → Startup shows it *On*. |
| Animation changes too often / too slowly | Settings → **CPU & switching**: raise or lower *Minimum animation time* and *Average over*. |
| CPU % differs from Task Manager | Expected, see [How it works](#how-it-works). |
| Build fails: *file is being used by another process* | The pet is running. Exit it first. |

---

## License

[MIT](LICENSE). The raccoon animations were created with the help of an AI image generator. The code was written
with the help of Claude Opus 5.5 (Anthropic).
