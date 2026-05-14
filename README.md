<p align="center">
  <img src="src/Dock.App/Assets/rock-et-dock-icon.png" width="96" alt="Rock ET Dock icon">
</p>

# Rock ET Dock

**Your Windows dock should not feel like office furniture. It should hit like a loud amp.**

Rock ET Dock is a per-user Windows dock and app launcher built in WPF, inspired by the classic RocketDock workflow and rebuilt as clean modern code. Pin the tools you actually use, drag files straight into the dock, throw the native taskbar backstage, and run your desktop from a sharper, faster, louder launcher.

> **Download the installer:** [latest GitHub release](https://github.com/Discasa/Rock-ET-Dock/releases/latest)

No portable zip. No loose DLL hunt. Install it, launch it, tune it, move on.

## Screenshots

![Rock ET Dock showcase with Windows button and sample items](assets/screenshots/dock-showcase.png)

![Rock ET Dock with the native Windows taskbar hidden](assets/screenshots/dock-taskbar-hidden.png)

| Left vertical | Right vertical |
| --- | --- |
| ![Rock ET Dock running vertically on the left edge](assets/screenshots/dock-left-vertical.png) | ![Rock ET Dock running vertically on the right edge](assets/screenshots/dock-right-vertical.png) |

| Top left | Top right |
| --- | --- |
| ![Rock ET Dock at the top-left corner](assets/screenshots/dock-top-left.png) | ![Rock ET Dock at the top-right corner](assets/screenshots/dock-top-right.png) |

| Bottom left | Bottom right |
| --- | --- |
| ![Rock ET Dock at the bottom-left corner](assets/screenshots/dock-bottom-left.png) | ![Rock ET Dock at the bottom-right corner](assets/screenshots/dock-bottom-right.png) |

![Rock ET Dock using the AstroOrange theme](assets/screenshots/dock-astro-orange.png)

## What It Does

- Runs as a clean per-user dock for Windows, with one app managing multiple docks.
- Starts new docks with Windows, Windows Settings, File Explorer, Microsoft Edge, and Recycle Bin ready.
- Supports files, folders, links, separators, animated GIFs, the native Recycle Bin, and temporary minimized-window items.
- Opens the native Start menu with left-click on the Windows button and the native Win+X menu with right-click.
- Shows native Windows shell context menus on dock items, so right-click behavior feels like Explorer.
- Lets you place docks on the top, bottom, left, or right edge, with true vertical layout on the side edges.
- Applies every setting immediately. There is no Apply button and no waiting room.
- Ships a separate **Rock ET Dock Settings** launcher for configuration and new dock creation.
- Hides the native Windows taskbar while Rock ET Dock is running, then restores it on exit.
- Uses hover magnification that pushes neighboring icons aside instead of stacking them.
- Supports English and Brazilian Portuguese from the settings window.
- Ships as an installer built with Inno Setup.

## Install

1. Go to the [latest release](https://github.com/Discasa/Rock-ET-Dock/releases/latest).
2. Download `Rock-ET-Dock-Setup-<version>-win-x64.exe`.
3. Run the installer.
4. Launch **Rock ET Dock**. Use the Start Menu shortcut **Rock ET Dock Settings** when you want to tune it.

The installer is the intended distribution path. The app is self-contained, so the user does not need to install the .NET runtime separately.

## Tune The Dock

Open **Rock ET Dock Settings** from the Start Menu. In the repository build, `settings.bat` opens the same settings-only mode.

- **General:** dock name, language, startup, locking, auto-hide, Windows button, Recycle Bin, native taskbar hiding, new dock creation, move modifier key, GIF modifier key, and managed dock folder.
- **Icons:** size, opacity, labels, spacing, bottom margin, quality, hover magnification, magnification range, and animated GIF items.
- **Position:** monitor, screen edge, layering, width, height, edge distance, and center offset.
- **Style:** themes, background opacity, dock radius, icon tile radius, font, font size, and label color.
- **Behavior:** minimized-window items, running indicators, existing-instance activation, and mouseover popup.

The settings window uses a Windows Settings-style layout and follows the system light/dark app theme. When the dock is already running, the settings launcher asks that live process to open settings, so changes still apply immediately.

## Data Contract

Rock ET Dock is intentionally per-user:

- Configuration: `%LOCALAPPDATA%\Rock ET Dock\dock.config.json`
- Runtime log: `%LOCALAPPDATA%\Rock ET Dock\logs\runtime.log`
- Managed dock root: `%USERPROFILE%\Rock ET Dock`
- Managed folder per dock: `%USERPROFILE%\Rock ET Dock\<dock-name>`

When you drop a file or folder onto the dock, Rock ET Dock creates a shortcut in the managed dock folder by default. Hold the configured move modifier key, `Shift` by default, to move the item into the dock folder instead. Hold the configured GIF modifier key, `Alt` by default, while dropping `.gif` files to add them as looping animated dock items. Dragging an item out of the dock removes the dock shortcut by default; holding the move modifier moves app-managed dock files back out.

## Build From Source

Requirements:

- Windows
- .NET 10 SDK
- PowerShell
- Inno Setup 6, only when building the installer

```powershell
dotnet build Dock.slnx -v minimal
dotnet run --project src\Dock.App\Dock.App.csproj
.\settings.bat
dotnet run --project tests\Dock.GeometryChecks\Dock.GeometryChecks.csproj
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

The installer is written to `artifacts\installer`.

## Clean-Room Line

RocketDock is used as behavior reference only. Rock ET Dock is new WPF code, with project-owned runtime assets and its own configuration model. Research notes live in [`docs/rocketdock-recreation-notes.md`](docs/rocketdock-recreation-notes.md).
