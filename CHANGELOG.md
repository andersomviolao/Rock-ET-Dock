# Changelog

All notable changes to this project are documented here.

## 0.4.1 - 2026-05-14

- Connected the remaining settings UI placeholders: app display name editing, functional settings search, and live minimize-animation control.
- Fixed native Windows taskbar restoration when the last dock window is closed while taskbar hiding is enabled.
- Reworked the settings window to more closely match Windows 11 Settings, including custom dark window chrome, sidebar navigation, row cards, toggle switches, slim scrollbars, and accent sliders.
- Renamed the internal solution, project folders, project files, and namespaces from `Dock.*` to `RockETDock.*`.
- Moved local RocketDock reference material under the ignored `RocketDock/` folder.
- Fixed the background opacity slider so its minimum value makes the dock shell fully invisible, including border and shadow.
- Renamed the repository helper scripts to `dock_run.bat`, `dock_settings.bat`, and `dock_reset.bat`.

## 0.4.0 - 2026-05-13

- Added native Windows shell context menus for dock items so right-click behavior matches Explorer.
- Added a separate settings launcher mode, `settings.bat`, and packaged `Rock ET Dock Settings.exe` with a Start Menu shortcut.
- Added named-pipe handoff so the settings launcher opens the live dock process settings window when Rock ET Dock is already running.
- Redesigned settings to follow the Windows Settings visual model with a navigation rail, search-style header, cards, and system light/dark theme colors.
- Added a settings area for creating new docks on the left, right, top, or bottom edge.
- Fixed hover magnification clipping at the start and end of the dock by sizing the transparent hover overhang per axis.
- Added `reset-config.bat` to reset the current user's dock configuration while preserving managed dock items.
- Removed the dock item versions of Settings and Exit; those actions remain in the dock context menu.
- Changed first-run defaults to Windows, Windows Settings, File Explorer, Microsoft Edge, and Recycle Bin.
- Changed drag-and-drop defaults so dropping into the dock creates shortcuts and dragging out removes dock shortcuts; holding the configurable move modifier, `Shift` by default, performs managed file moves.
- Added a configurable GIF drop modifier, `Alt` by default, that imports dropped `.gif` files as looping animated dock items.
- Added distinct drag animations for move actions and remove-from-dock actions.
- Refined drop feedback with short opacity/scale pulses and separate accent colors for shortcut, move, and looping GIF imports.
- Refined drag-out feedback so removing a shortcut fades and shrinks differently from moving a managed file out of the dock folder.
- Fixed first-run Windows Settings and File Explorer items so they launch through Windows shell commands instead of being treated as ordinary running app executables.
- Stabilized drag-in spacing so nearby items open symmetrically around the drop position without rapid back-and-forth layout jitter.
- Smoothed internal item reordering by coalescing drag updates to the render loop and skipping layout work when the insertion slot has not changed.
- Kept hover magnification active while reordering so the dock remains expanded and the drag preview preserves the zoom scale without square clipping.

## 0.3.0 - 2026-05-13

- Translated the application code surface and project documentation to English.
- Added an immediate language selector for English and Brazilian Portuguese in the settings window.
- Centralized UI text in a language catalog instead of spreading hardcoded labels through windows and menus.
- Localized dock settings, context menus, dialogs, enum labels, and built-in special item labels.
- Fixed side-edge docks so Left and Right render as true vertical docks with stacked upright icons.
- Reworked the README with release-oriented copy, icon branding, and screenshot sections.
- Updated packaging metadata to version `0.3.0`.

## 0.2.2 - 2026-05-13

- Set the app version to `0.2.2`.
- Adjusted `installer/build-installer.ps1` to generate only the official installer.
- Updated documentation to avoid distributing a manual zip package.

## 0.2.1 - 2026-05-13

- Added the Inno Setup/ISCC installer script at `installer/RockETDock.iss`.
- Added `installer/build-installer.ps1` to publish, package, and compile the installer.
- Set the app version to `0.2.1` for the installer release.
- Documented the installer build command.

## 0.2.0 - 2026-05-13

- Refactored file and folder move/copy operations into `ManagedPathService`.
- Extracted `.lnk` shortcut creation and resolution into `ShellShortcutService`.
- Added the global `Ctrl+Alt+R` hotkey to hide or show all open docks.
- Added running-app indicators and best-effort existing-instance activation for resolvable `.exe` items and `.lnk` shortcuts.
- Added persistent special items for separators, Settings, and Exit.
- Added a context menu for adding files, folders, separators, Settings, and Exit.
- New docks now start with Windows button, Recycle Bin, Settings, and Exit.
- Removed the manual Apply flow from the settings window; changes are saved and reflected immediately.
- Fixed hover zoom so neighboring icons move aside instead of overlapping visually.
- Fixed animated GIF playback so GIF items resume looping when loaded and visible.
- Set the app version to `0.2.0` for the initial public release.
- Added checks for special items and executable mapping.

## 0.1.0 - 2026-05-13

- Created the initial WPF Rock ET Dock app.
- Added a transparent borderless dock with positioning on all four screen edges.
- Added file, folder, link, animated GIF, Windows button, and Recycle Bin items.
- Added drag-and-drop to import, reorder, export to desktop, and remove items.
- Added the settings window with immediate application.
- Added settings for icon size, zoom, zoom range, opacity, spacing, margins, width, height, positioning, and themes.
- Added smooth hover magnification through interpolation.
- Added initial multi-dock support.
- Added executable checks for geometry, reorder, import/export, placeholder, and sizing behavior.
- Documented clean-room RocketDock research in `docs/rocketdock-recreation-notes.md`.
