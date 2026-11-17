# WinPla

WinPla puts the windows of the applications you choose on the monitor you want, maximized, at their
normal size or at an exact position, every time you sign in to Windows. It runs for a few minutes after
sign-in, places the windows, and then closes itself. Nothing stays running in the background.

The program's interface is in Spanish. This guide quotes the Spanish labels exactly as they appear on
screen, with an English translation in parentheses the first time.

## Requirements

- Windows 10 or 11.
- **PowerShell 7** (`pwsh`). It is not the same as the "Windows PowerShell" that comes with Windows.
  If you don't have it, install it from a terminal with:

  ```powershell
  winget install --id Microsoft.PowerShell --source winget
  ```

You don't need anything else. In particular, you don't need the .NET SDK or Visual Studio.

## Install

1. Open **PowerShell** or **Terminal** from the Start menu. Either Windows PowerShell or PowerShell 7 works.
2. Paste this line and press Enter:

   ```powershell
   irm https://raw.githubusercontent.com/propstgonz/winpla-manager/main/install.ps1 | iex
   ```

3. The installer downloads WinPla, adds a **WinPla** shortcut to the Start menu and opens the program.
   If PowerShell 7 is missing, it says so, shows the `winget` command above and changes nothing.
4. In the WinPla window, under **Configuración** (Settings), tick **Iniciar con Windows** (Start with
   Windows). Until you do this, nothing happens at sign-in.

**Update:** run the same command again. The program files are replaced, but your rules and settings are kept.

**Open it later:** Start menu → **WinPla**. It takes a couple of seconds to appear because it compiles
itself each time it starts (see [Why PowerShell and not an .exe](#why-powershell-and-not-an-exe)).

**Uninstall:**

1. Open WinPla, untick **Iniciar con Windows** and close it.
2. Delete the folder `%APPDATA%\WinPla` (paste that path into the File Explorer address bar to find it).
3. Delete the **WinPla** shortcut from the Start menu. It is the file
   `%APPDATA%\Microsoft\Windows\Start Menu\Programs\WinPla.lnk`.

Or, with WinPla closed, run this in PowerShell to do all three steps:

```powershell
Remove-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run -Name WinPla -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\WinPla" -Recurse -Force
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\WinPla.lnk"
```

## Using WinPla

The main window has three sections:

- **Aplicaciones** (Applications): your rules. Each rule says "the windows of this program go to this
  monitor, in this mode".
- **Monitores** (Monitors): the monitors that are connected now. Each one is shown with its number, its
  resolution, **Principal** (Primary) if it is the main monitor, and its name as Windows reports it
  (for example "LG ULTRAGEAR"). If Windows has no name for a monitor, its model code is shown instead
  (for example `GSM5C00`). The list updates when you connect or disconnect a monitor.
- **Configuración** (Settings): the two settings and the **Aplicar reglas ahora** (Apply rules now) button.

### Add a rule

Click **Añadir aplicación** (Add application). In the dialog, choose the program in one of three ways:

| Option | When to use it |
|---|---|
| **Examinar…** (Browse) | Choose the program's `.exe` file. Use it when the program is not open and is not in the startup list. It only works if the window belongs to that same `.exe` (see the Steam case below). |
| **…o elige una ventana abierta** (…or pick an open window) | Lists the programs that have a window open right now, as "program — window title". **This is the most reliable option**, because it picks exactly the program that owns the window. Open the program first. |
| **…o elige una aplicación del arranque de Windows** (…or pick an app from Windows startup) | Lists the programs that start automatically with Windows and are enabled, which are the same ones shown as enabled in Task Manager → Startup apps. Handy when you want to place exactly those programs. For launchers such as Discord or Slack (`Update.exe --processStart …`), it picks the real program. Microsoft Store apps are not listed, and entries started through a script (`.bat`, `cmd`) can't be resolved to the real program. |

**When the window belongs to another program (Steam, for example).** Some programs start with one `.exe`
and show their window from a different one. Steam starts as `steam.exe`, but its window belongs to
`steamwebhelper.exe`. A rule for `steam.exe`, whether picked with Browse or from the startup list, never finds a window.
In those cases, open the program and choose it from **…o elige una ventana abierta**. The dialog
says the same: if an application's window is not being placed, open it and pick it from the list of open windows.

You can have only one rule per program. If you try to add a second one, WinPla asks you to edit the existing rule instead.

### Choose the monitor and the mode

Under **Monitor**, pick the target monitor. Under **Modo** (Mode), pick one of:

- **Maximizada** (Maximized): the window is moved to that monitor and maximized.
- **Normal (se mueve al monitor conservando su tamaño)** (Normal: moved to the monitor, keeping its size):
  the window is not maximized. If it is already on that monitor, it stays where it is. Otherwise, it is
  centered on the monitor. If it is bigger than the monitor, it is shrunk to fit.
- **Personalizada** (Custom): an exact position and size. Fill in **X**, **Y**, **Ancho** (Width) and **Alto**
  (Height), in pixels from the top-left corner of the monitor's usable area (the screen minus the taskbar).
  These are real screen pixels, not scaled ones. Width and height must be greater than 0.

  The easiest way to fill these in is to put the window where you want it, then click
  **Usar la posición actual de la ventana** (Use the window's current position). WinPla reads where the
  window is now, selects the monitor it is on, and fills in X, Y, width and height. The program must be open.

Click **Aceptar** (OK) to save or **Cancelar** (Cancel) to discard.

### Manage rules

Each rule in the list shows the program's icon and name, its monitor and its mode. Next to it are:

- **Activada** (Enabled): untick it to turn the rule off without deleting it. A disabled rule is ignored
  at sign-in and by **Aplicar reglas ahora**.
- **Editar** (Edit) and **Eliminar** (Delete). Delete asks for confirmation first.

If a rule's monitor is not connected, the rule shows **Monitor desconectado** (Monitor disconnected) in red.

Every change is saved immediately. There is no save button.

### Apply rules now

**Aplicar reglas ahora** places the windows that are open right now, once, and shows one line per window:

| Result | Meaning |
|---|---|
| `colocada` | Placed. |
| `ya estaba en su sitio` | Already in place. Nothing to do. |
| `no está abierta` | The program has no window open. |
| `su monitor no está conectado` | The rule's monitor is not connected. The window was not touched. |
| `se ejecuta como administrador; WinPla no puede moverla` | The program runs as administrator. WinPla can't move it. |
| `no se pudo mover` | Windows refused the move for another reason. |

This is a quick way to test a rule without signing out.

### Settings

- **Iniciar con Windows** (Start with Windows): makes Windows run WinPla when you sign in. If you disable
  WinPla in Task Manager → Startup apps, this box appears unticked. Ticking it enables WinPla again.
- **Colocar las ventanas automáticamente al iniciar sesión** (Place windows automatically at sign-in):
  turns the sign-in placement on or off without removing WinPla from startup. When it is off, WinPla still
  starts at sign-in but exits immediately.

## What happens when you sign in

Windows starts WinPla in the background, with no window and no tray icon. WinPla then:

1. Exits immediately if **Colocar las ventanas automáticamente al iniciar sesión** is off, if there are no
   rules, or if another copy is already doing this.
2. For **5 minutes** (300 seconds), checks once per second for windows of the programs in your enabled rules,
   and places them. Every window of the program is handled, not only the first, so a splash screen
   followed by the real window still works.
3. Keeps checking each window until it has been in place for **3 checks in a row** (about 3 seconds).
   Many programs restore their own last position just after they appear, and this undoes that. A window
   is also given up after **15 attempts**. After either one, WinPla leaves that window alone, so you can
   move it freely.
4. Leaves minimized windows minimized. WinPla changes where they will appear, and whether they will be
   maximized, so that when you open them they show up in the right place.
5. Closes itself when the 5 minutes are over.

What WinPla did, and what failed, is written to `%APPDATA%\WinPla\log.txt`. That file is the first place
to look when something doesn't work.

## Known limitations

- **Windows that appear late.** If a program starts hidden in the tray and you open its window after the
  5 minutes are up, WinPla doesn't place it. Click **Aplicar reglas ahora**, or make the watch time longer
  by changing `WatchSeconds` in `config.json` (see below).
- **Programs running as administrator** can't be moved. Windows doesn't let a normal program move them.
  WinPla reports this instead of failing silently.
- **Disconnected monitor.** If a rule's monitor isn't connected, the window is not touched. Monitors are
  recognized by a device ID, not by their number or name, so a change in numbering doesn't matter.
  However, plugging the same monitor into a different port may require you to pick it again in the rule.
- **One placement per program.** Rules match by program file name, so all windows of a program
  (for example, every browser window) go to the same place. You can't target one window by its title.
- **Microsoft Store apps** that run inside `ApplicationFrameHost.exe` can't be told apart from each other.
- **Windows on other virtual desktops** are not seen until you switch to their desktop.
- **Different display scaling.** When a window moves to a monitor with a different scaling percentage, the
  program itself may resize it, so the final size can differ slightly from the one you entered.

## Where WinPla keeps its data

| Location | What it is |
|---|---|
| `%APPDATA%\WinPla\app\` | The installed program (scripts, C# sources and XAML). Replaced when you update. |
| `%APPDATA%\WinPla\config.json` | Your rules and settings. Plain JSON that you can edit by hand while WinPla is closed. If the file is damaged, it is renamed to `config.json.bak` and WinPla starts with no rules. |
| `%APPDATA%\WinPla\log.txt` | What was placed and what failed, plus startup errors. When it grows past 512 KB, it is cleared and starts over. Log messages are in Spanish. |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `WinPla` | The "start with Windows" entry. Created and removed by **Iniciar con Windows**. |
| `%APPDATA%\Microsoft\Windows\Start Menu\Programs\WinPla.lnk` | The Start menu shortcut. It opens WinPla without a console window. |

`config.json` looks like this. `WatchSeconds` can only be changed here.

```json
{
  "AutoPlacementEnabled": true,
  "WatchSeconds": 300,
  "Rules": [
    {
      "ExeName": "Discord.exe",
      "ExePath": "C:\\Users\\me\\AppData\\Local\\Discord\\app-1.0.9000\\Discord.exe",
      "MonitorId": "\\\\?\\DISPLAY#GSM5C00#...",
      "MonitorLabel": "Monitor 2 · 2560×1440",
      "Mode": "Maximized",
      "X": 0, "Y": 0, "Width": 0, "Height": 0,
      "Enabled": true
    }
  ]
}
```

`Mode` is `Maximized`, `Normal` or `Custom`. `ExeName` is the only field used to find windows (case-insensitive).
`ExePath` is only used for the icon and tooltip. `MonitorLabel` is shown when the monitor is disconnected.

## Why PowerShell and not an .exe

Windows 11's **Smart App Control** blocks unsigned executables that were built locally, so a plain
`WinPla.exe` would not run on machines where it is on. Instead, `WinPla.ps1` compiles the same C# sources in
memory under PowerShell 7 (`Add-Type`) and runs them, and Smart App Control allows that. This is also why
WinPla takes a moment to open.

On a machine without Smart App Control, or if you sign the binary, you can build and use a normal executable.
This needs the **.NET 10 SDK**:

```powershell
git clone https://github.com/propstgonz/winpla-manager
cd winpla-manager
dotnet publish -c Release -o dist
.\dist\WinPla.exe
```

The `.exe` is framework-dependent, so the machine that runs it needs the **.NET Desktop Runtime 10**.
`dist\` is not tracked by git. If you tick **Iniciar con Windows** from the `.exe`, the startup entry runs
`"…\WinPla.exe" --startup`. The `.exe` and the PowerShell version share the same startup entry. When you
open either one and the entry exists, it points the entry at itself.

## For developers

A single C# / WPF project (`net10.0-windows`) with no NuGet dependencies, a little over a thousand lines of C#.
The same sources build into `WinPla.exe` with `dotnet` and run under PowerShell 7 through `WinPla.ps1`.

### Source layout

| Path | Responsibility |
|---|---|
| `Contracts.cs` | Shared models (`Rule`, `Config`, `MonitorInfo`, `WindowInfo`, `StartupApp`, `PlaceResult`, enums) and a comment listing the static API of each module. |
| `App.cs` | Entry point (`App.Main`) and modes: no arguments opens the UI, `--startup` runs the sign-in watch, and `--selftest` runs the self-test. |
| `GlobalUsings.cs` | Global `using`s, because `Add-Type` doesn't apply `ImplicitUsings`. |
| `Core/NativeMethods.cs` | Win32 P/Invoke declarations. |
| `Core/Monitors.cs` | Connected monitors: stable ID, number, display name, bounds and work area. |
| `Core/AppWindows.cs` | Enumerates top-level application windows and resolves the owning `.exe`. Also reads a window's restored rectangle. |
| `Core/Placer.cs` | `ComputeTarget` (pure: target rectangle for a rule) and `Apply` (`Get/SetWindowPlacement`, idempotent, returns `AlreadyOk` when nothing changes). |
| `Core/Engine.cs` | `ApplyOnce` (single pass, used by **Aplicar reglas ahora**) and `WatchAsync` (sign-in polling with per-window settle tracking). |
| `Core/StartupApps.cs` | Enabled startup apps from the `Run` keys (HKCU, HKLM, HKLM WOW6432Node) and both Startup folders, honouring `StartupApproved`. Resolves Squirrel `Update.exe --processStart X.exe` to `X.exe`. |
| `Core/SelfTest.cs` | Fixed-case checks of `Placer.ComputeTarget` and `StartupApps.ParseCommand`. |
| `Storage/ConfigStore.cs` | Loads and saves `config.json` (atomic write through a `.tmp` file; invalid JSON is moved to `.bak`). |
| `Storage/Autostart.cs` | The HKCU `Run` value, including Task Manager's enabled/disabled state. |
| `Storage/Log.cs` | Thread-safe log that never throws. |
| `UI/Ui.cs` | Loads loose XAML: from disk under `WinPla.ps1` (`Ui.Dir`), or from embedded resources in the `.exe`. |
| `UI/MainWindow.xaml(.cs)`, `UI/RuleDialog.xaml(.cs)` | The two windows. The XAML has no `x:Class`, and events are wired up in code. |
| `WinPla.ps1` | PowerShell 7 launcher. It compiles every `.cs` (excluding `bin/` and `obj/`) with `Add-Type`, sets `Ui.Dir` and the startup command, and calls `App.Main`. Launcher errors go to `log.txt`. |
| `WinPla.cmd` | Double-click launcher for a local clone. It runs `WinPla.ps1` through `conhost --headless`, so no console appears. |
| `install.ps1` | Remote installer. Downloads the `main` branch zip, validates it, copies only `WinPla.ps1`, `WinPla.cmd`, `*.cs` and `UI\*.xaml` to `%APPDATA%\WinPla\app` (swapping folders so a failed install leaves the old version intact), creates the shortcut and opens the UI. Never touches `config.json` or `log.txt`. |
| `WinPla.csproj`, `app.manifest` | Build for the `.exe`. The manifest declares PerMonitorV2 DPI awareness, and XAML is embedded as resources instead of being compiled. |

### Key design decisions

- **Rules match by executable file name** of the window's owning process, case-insensitive, never by path,
  because paths change with updates.
- **Monitors are identified by a stable device ID** (the device interface path from `EnumDisplayDevices`).
  The number (`\\.\DISPLAYn`) and the name are only for display. The name is the friendly name Windows
  reports for the monitor, with a fallback to the model code taken from the device ID.
- **Physical pixels everywhere.** The process is PerMonitorV2: through the manifest for the `.exe`, and through
  `SetProcessDpiAwarenessContext` under `pwsh`, which has no manifest of its own.
- **Placement uses the restored rectangle** (`WINDOWPLACEMENT.rcNormalPosition`, converted from workspace
  coordinates). To maximize, the window is first restored at the target and then maximized, because
  maximizing directly doesn't move a window that is already maximized on another monitor. Minimized windows
  stay minimized, and only their restore position and maximize flag change.
- **Bounded polling instead of hooks.** It polls once per second for `WatchSeconds`. This is simpler, and it
  also catches windows that already existed. A window stops being tracked after 3 consecutive `AlreadyOk`
  passes or 15 attempts (`SettledCycles` and `MaxAttempts` in `Engine.cs`). If the polling cost ever matters,
  the next step is `SetWinEventHook(EVENT_OBJECT_SHOW)`.
- **Nothing resident.** `--startup` exits when the watch ends. A named mutex (`Local\WinPla.Startup`) prevents
  overlapping runs.
- **The UI follows the system light/dark theme** through WPF's `ThemeMode.System`.

### Run from a local clone

Clone the repository and double-click `WinPla.cmd`, or run:

```powershell
pwsh -NoProfile -STA -File .\WinPla.ps1             # opens the UI
pwsh -NoProfile -STA -File .\WinPla.ps1 --startup   # runs the sign-in watch now
```

C# and XAML changes take effect the next time you launch it. Nothing is cached. Note that opening the UI
rewrites an existing startup entry to point at the copy you opened. If you have WinPla installed as well,
open the installed copy once afterwards to point it back. If you move the clone, opening it once from the
new location fixes the entry.

`install.ps1` has two test hooks: `$env:WINPLA_SOURCE_ZIP` installs from a local zip instead of downloading
(the zip must contain a top-level folder with `WinPla.ps1` in it, like GitHub's archive), and `$env:WINPLA_NO_LAUNCH`
skips opening the UI at the end.

### Checks

- **Self-test** (no window opens, no .NET SDK needed):

  ```powershell
  pwsh -NoProfile -STA -File .\WinPla.ps1 --selftest; $LASTEXITCODE
  ```

  Exit code `0` and `Selftest OK` in `log.txt` mean it passed. Otherwise, the exit code is `1` and the log
  shows `Selftest FALLO:` followed by the first failing case, or a `WinPla.ps1:` error if compilation failed.
- **Type-check build** (needs the .NET 10 SDK): `dotnet build`. Because `WinPla.ps1` only compiles at
  run time, this is the quickest way to catch C# errors. XAML is loaded at run time in both cases, so XAML
  mistakes only show up when the window opens.
