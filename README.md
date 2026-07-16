# Edgetree

[한국어 안내](README-ko.md)

A lightweight, always-on-hand file explorer for Windows that docks to the left
or right edge of the screen, VS Code Explorer style. It's not meant to replace
Windows Explorer — it's a quick way to glance at a folder structure and jump
straight to a file without opening a full Explorer window.

## Download

Grab the latest build from the [Releases page](https://github.com/legendsteel11-dotcom/Edgetree/releases/latest). Two options are attached to each release:

- **`Edgetree-<version>-win-x64.exe`** (~1 MB) — pick this if you already have
  the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed.
- **`Edgetree-<version>-win-x64-standalone.exe`** (~160 MB) — a single file
  with everything bundled in, no .NET install required. Pick this if you're
  not sure, or if the smaller exe complains about a missing runtime.

Either way, it's one `.exe` — no installer, just run it.

## Screenshots

| | |
|---|---|
| ![Docked to the left edge](screenshot/docked-left.png) | ![Docked to the right edge](screenshot/docked-right.png) |
| Docked to the left edge | Docked to the right edge |
| ![Browsing a wider window](screenshot/browsing.png) | ![Right-click context menu](screenshot/context-menu.png) |
| Browsing, undocked/wider | Right-click context menu |
| ![Options menu](screenshot/options-menu.png) | ![Color settings](screenshot/color-settings.png) |
| Options ("...") menu | Color settings |

## Features

- **Docked to the screen edge** — left by default, switchable to the right
  from the options menu — spanning the full work area height (excludes the
  taskbar), hidden from Alt+Tab and the taskbar while docked
- **Collapse/expand** by clicking the app icon: one click shrinks it to an
  icon-only rail, a second click auto-hides it to a bare sliver at the screen
  edge that peeks open on mouse-over and quietly re-hides once you move away
  — pin it back open from the peeked-out state to stop auto-hiding — all with
  a smooth width animation
- **Drag-to-resize** width while docked
- **Undock into a floating window**: drag the header past the edge to detach
  it into a normal, freely movable and resizable window (shows in Alt+Tab and
  the taskbar like any other app); click the pin button, or double-click the
  header, to snap it back to the edge — remembers its floating position and
  size across a dock/undock round trip
- **Drive-based root** ("This PC"): all connected drives listed at the top
  level, folders lazily loaded as you expand them
- **Picks up where you left off**: expanded folders and your last-selected
  item are restored the next time you launch the app
- **Large folders stay fast**: each folder shows its first 25 items with a
  "… Show N more" row to reveal the rest, so the tree never renders thousands
  of rows at once — jumping to a favorite below a huge folder stays instant.
  Adjustable from 1–50 in the options menu (handy on smaller screens)
- **Export/import settings**: save your settings to a JSON file and load them
  on another PC (favorites included; paths that don't exist there are dropped)
- Single click a folder row to expand/collapse (VS Code-style, not just the
  arrow); double-click a file to open it with its default application
- **Auto Collapse** (in the "..." options menu): accordion mode — expanding a
  folder collapses every other open one, keeping just the path to it visible
- **Favorites panel**: pin folders below the header; a single click
  expands/scrolls the tree straight to that folder (landing it at the top of
  the tree), expanding the folder itself too — reliably in one click even when
  it sits below a huge folder or on a different drive. Height auto-fits the
  current number of favorites (double-click the divider to fit exactly), and
  it follows the tree's font-size zoom. Switchable to the bottom from the
  options menu (works the same docked or floating)
- **Inline rename** (VS Code-style): `F2` or the right-click menu edits the
  name directly in the tree row, no popup dialog. Clicking an already-selected
  **file** again after a pause starts a rename too (Explorer-style); folders
  are excluded, since a click there toggles expand/collapse — use `F2` or the
  right-click menu for those
- **Right-click menu**: new folder, refresh, open, open with (files only),
  copy/paste, rename, delete (to the Recycle Bin, with a confirmation
  prompt), copy path, open a terminal here, reveal in Explorer (a folder opens
  itself; a file opens its parent with the file selected), properties — with
  matching keyboard shortcuts (`F2`, `F5`, `Delete`, `Ctrl+C`/`Ctrl+V`,
  `Enter`) that **keep working while this same right-click menu is open**
  (not other popups, like Color Settings); opens below the row it targets so
  it never covers the item
- **Drag files out** of the sidebar into Explorer or any other app (standard
  Windows file drag, so copy/move/drop-to-open all work as expected)
- **Sort**: by name or date, ascending or descending — from the "..." options
  menu, or a folder's own right-click menu, which also re-sorts that folder
  immediately instead of waiting for the next expand
- **Material Icon Theme** file/folder icons (see
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)); drive names shown in bold
- Header: pin, collapse-all, an **options ("...") menu** (Auto Collapse,
  always on top, start with Windows, always show the tray icon, show folder
  icons, show file icons, favorites at bottom, dock to the right edge, color
  settings, default sort, items per folder, language, export/import settings,
  about — all remembered), minimize to tray, and close
- **Minimize to the system tray**: click the tray icon (or its "Open" menu
  item) to restore the window; right-click for Open/Exit
- **Color settings**: 14 customizable colors — folder name, file name (each
  with its own selected-highlight and mouse-hover variant), explorer
  background, favorites background, selection, mouse-hover background, guide
  line/highlight, title bar background, and the divider line between the
  title bar/favorites/explorer panels — via Windows' own color picker - the
  custom palette now sticks around while you move between rows - with a
  one-click reset to defaults
- **Korean/English UI language**, switchable from the options menu (restarts
  the app to apply)
- Hand cursor over tree/favorites rows; header icons sit slightly dimmed and
  brighten on hover
- **About** window: version, author, build date, license summary (including
  a plain-language no-warranty note)
- Rounded corners when floating (undocked) on Windows 11, to match native windows
- `Ctrl` `+`/`-` to zoom the tree's font size (9–16pt), `Ctrl+0` to reset —
  row spacing scales along with it, in the favorites panel too
- Selected file's parent folder gets a brighter indent-guide line, VS
  Code-style
- Unassociated file types fall back to the same "How do you want to open this
  file?" picker Explorer would show, instead of doing nothing

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build/run
  from source)

## Building & running

```bash
git clone <this-repo-url>
cd SIDEBARVSCODELIKEEXPLORER
dotnet run --project src/Edgetree
```

Or build the whole solution:

```bash
dotnet build Edgetree.sln
```

## Publishing a standalone executable

Framework-dependent (small, needs the .NET 8 Desktop Runtime on the target machine):

```bash
dotnet publish src/Edgetree -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Self-contained (large, no .NET install needed on the target machine):

```bash
dotnet publish src/Edgetree -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

`IncludeNativeLibrariesForSelfExtract` matters here — without it, WPF's native
interop DLLs (`D3DCompiler_47_cor3.dll`, `wpfgfx_cor3.dll`, etc.) get dropped
loose next to the exe instead of bundled into it, so the exe stops working the
moment it's moved on its own.

Either way, the resulting `.exe` is in
`src/Edgetree/bin/Release/net8.0-windows/win-x64/publish/`.

## License

MIT — see [LICENSE.md](LICENSE.md). Bundled icons are from the Material Icon
Theme project (also MIT) — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## About Development

This tool was designed and iterated on by the author, with implementation
done in collaboration with Claude Code (Anthropic). Feature decisions,
UX design, and real-world testing were driven by daily personal use.
