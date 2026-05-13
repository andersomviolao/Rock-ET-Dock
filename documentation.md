# Rock ET Dock - Documentation

Rock ET Dock is a WPF Windows dock and app launcher. It recreates classic dock behavior through a clean-room implementation and does not depend on RocketDock binaries at runtime.

## Repository Layout

- `Dock.slnx`: .NET solution.
- `src/Dock.App`: main WPF application.
- `tests/Dock.GeometryChecks`: executable checks for geometry, sizing, reorder, import/export, and configuration behavior.
- `assets`: project-owned images and README assets.
- `docs`: research notes and implementation requirements.
- `installer`: Inno Setup packaging script and PowerShell build helper.

Local reference folders such as `_reference` and `_tools`, plus the original `RocketDock-v1.3.5.exe` reference installer, are intentionally outside Git.

## Requirements

- Windows.
- .NET 10 SDK.
- PowerShell or an equivalent terminal.
- Inno Setup 6 to build the installer.

## Commands

```powershell
dotnet build Dock.slnx -v minimal
dotnet run --project src\Dock.App\Dock.App.csproj
dotnet run --project tests\Dock.GeometryChecks\Dock.GeometryChecks.csproj
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

`run.bat` also starts the app from the repository root.

The official distribution artifact is the installer under `artifacts\installer`. The publish folder under `artifacts\publish` is an intermediate input for the installer.

## User Data

The app stores data per user:

- Configuration: `%LOCALAPPDATA%\Rock ET Dock\dock.config.json`
- Logs: `%LOCALAPPDATA%\Rock ET Dock\logs\runtime.log`
- Managed dock root: `%USERPROFILE%\Rock ET Dock`
- Managed folder for each dock: `%USERPROFILE%\Rock ET Dock\<dock-name>`

For smoke tests without touching the real user profile:

```powershell
$env:ROCK_ET_DOCK_LOCALAPPDATA = "$env:TEMP\rock-et-dock-local"
$env:ROCK_ET_DOCK_USERPROFILE = "$env:TEMP\rock-et-dock-profile"
dotnet run --project src\Dock.App\Dock.App.csproj
```

## Current Features

- Multiple docks managed by one app instance.
- Top, bottom, left, and right positioning, with vertical item layout on the left and right edges.
- Fine-grained settings for icon size, hover zoom, zoom range, opacity, spacing, margins, width, height, edge distance, center offset, and layering.
- Built-in themes inspired by reference skin names, implemented with project-owned colors and shapes.
- Separate corner-radius controls for the dock shell and icon tile backgrounds.
- Drag-and-drop to add shortcuts, reorder, remove dock shortcuts, and move app-managed items with a configurable modifier key.
- Context menu to add files, folders, and separators.
- Windows button with native Start menu on left-click and native Win+X menu on right-click.
- Recycle Bin item with native context menu and file-drop deletion.
- Option to hide the native Windows taskbar while the app is running.
- Temporary items for minimized windows.
- Global `Ctrl+Alt+R` hotkey to hide or show all open docks.
- Running-app indicators and existing-instance activation for resolvable `.exe` items and `.lnk` shortcuts.
- Animated GIF dock items that loop while visible.
- Immediate settings application, including language switching between English and Brazilian Portuguese.

## Implementation Decisions

- The project is clean-room: behavior and formats are documented, but runtime code and assets are project-owned.
- Dropped files and folders are referenced through shortcuts in the managed dock folder by default. Holding the configured move modifier key, `Shift` by default, moves the source into the dock folder instead.
- Dropped `.gif` files become looping animated dock items when the configured GIF modifier key, `Alt` by default, is held during the drop.
- Dragging an item out of the dock removes only the dock shortcut by default. Holding the move modifier moves app-managed dock files out of the dock folder when the item has a managed backing file.
- File-system move/copy rules are centralized in `ManagedPathService`; shortcut creation and resolution are centralized in `ShellShortcutService`.
- Special shell-backed items stay separate from normal file-system items. Recycle Bin and Windows button behavior uses dedicated services.
- The settings window saves and reflects changes immediately so the user can tune the live dock.
- Hover zoom uses frame interpolation and layout offsets so neighboring icons move aside instead of overlapping.
- Running indicators use best-effort executable-path matching. Documents, URLs, modern/UWP apps, and indirect commands may not map to an existing visible window.

## Packaging

The installer is built with Inno Setup:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

The helper publishes the WPF app as a self-contained Windows x64 build, copies the root documentation into the publish folder, and then invokes `ISCC.exe`. The generated installer is:

```text
artifacts\installer\Rock-ET-Dock-Setup-<version>-win-x64.exe
```

The project does not publish a portable zip to avoid confusing users with multiple install paths.

## Validation

Before publishing changes, run:

```powershell
dotnet build Dock.slnx -v minimal
dotnet run --project tests\Dock.GeometryChecks\Dock.GeometryChecks.csproj
```

Avoid running build and checks in parallel because WPF generation can contend over temporary files in `obj`.
