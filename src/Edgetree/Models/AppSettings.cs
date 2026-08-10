using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace SidebarExplorer.App.Models;

public class AppSettings
{
    public double ExpandedWidth { get; set; } = 240;

    // The image viewer panel (header eye button / 뷰어에서 보기). ExpandedWidth
    // above stays the TREE's width alone: opening the viewer widens the WINDOW
    // by ViewerWidth on top of it, and every place that persists a width
    // subtracts the panel again (see MainWindow's viewer region). ViewerOpen
    // survives restarts; auto-hide and dock transitions fold the panel and
    // clear it (2026-08-08).
    public bool ViewerOpen { get; set; } = false;
    public double ViewerWidth { get; set; } = 360;

    // The zoom navigator (chip at the end of the viewer's zoom strip). OFF by
    // default on purpose: it was asked for on behalf of other people rather
    // than wanted on the asker's own screen, so it exists for whoever goes
    // looking and stays out of the way of everyone who doesn't. Even switched on it only appears while the picture
    // is actually bigger than the panel; there is nothing to navigate at fit.
    public bool ViewerNavigator { get; set; } = false;

    // The filmstrip: the folder's pictures and films as a row of thumbnails
    // under the panel. OFF by default for the same reason the navigator is -
    // it takes height from the picture, and a sidebar has little to spare - so
    // it waits behind the chip in the carousel row for whoever wants it.
    //
    // The size is the CELL's height in DIPs, not the strip's: the strip is
    // whatever the cells plus their padding come to, so the number stays
    // meaningful if the padding is ever changed. Cell width follows at 4:3, the
    // shape of a film frame.
    public bool ViewerFilmstrip { get; set; } = false;
    public double ViewerFilmstripCellHeight { get; set; } = 64;

    // Whether the strip fetches the WHOLE folder rather than only the part
    // around what is on screen. Off by default because it is a trade, not an
    // improvement: on a folder of 1359 photos over SMB it is a few minutes of
    // background reading and 100-250MB of thumbnails held in memory, in exchange
    // for a strip that never has a gap in it once it has settled. That is a good
    // trade for someone working through a shoot and a bad one for someone
    // glancing at a folder, which is exactly the shape a setting is for.
    public bool ViewerPrecacheThumbnails { get; set; } = false;

    // The help window's size, because it is the one dialog here that can be
    // resized and therefore the one someone can have an opinion about. 0 means
    // "never sized by hand" - the window works out its own first size then, and
    // deliberately not from the content: the document is several screens long,
    // so sizing to it opens a window as tall as the monitor.
    public double HelpWindowWidth { get; set; } = 0;
    public double HelpWindowHeight { get; set; } = 0;

    // Playback marks: places in a film someone wanted to be able to come back
    // to, kept per file and shown as ticks over the position bar.
    //
    // A LIST rather than a dictionary, and that is deliberate: it has to be
    // pruned, and pruning needs an order. Newest first, so the cap drops the
    // file nobody has touched in longest rather than whichever one the
    // serializer happened to put last (see MainWindow's VideoMark* region for
    // the caps).
    //
    // Nothing here is load-bearing. A path that has moved simply never matches
    // again and ages out on its own - the same tolerance the other
    // remembered-path settings carry, and the reason there is no pruning pass
    // that touches the disk.
    public List<VideoMarkEntry> VideoMarks { get; set; } = new();

    // Subtitles that sit beside the film as a .smi/.srt (see SubtitleService for
    // why that is the only kind there can be). ON by default, unlike the
    // navigator and the filmstrip: those add something to a panel that was
    // complete without them, while a film whose subtitle file is right there and
    // silent is a film missing half of itself.
    //
    // The size is in DIPs and deliberately NOT tied to the tree's Ctrl +/-: it
    // is read against the picture, at whatever size the panel happens to be,
    // rather than alongside the file names.
    public bool ViewerSubtitles { get; set; } = true;
    public double ViewerSubtitleFontSize { get; set; } = 16;

    // Collapsed to a bare sliver at the screen edge that peeks open on
    // mouse-over - see MainWindow.xaml.cs's EnterAutoHide/ExitAutoHide.
    // Entered by a single click on the app icon while docked and expanded.
    public bool IsAutoHidden { get; set; } = false;

    // "즉시자동숨김" in the options menu. True (default, matches the original
    // behavior from before this toggle existed) closes the peeked-open reveal
    // shortly after the cursor leaves it (MainWindow_MouseLeave). False keeps
    // it open regardless of the cursor and closes only once the user clicks
    // somewhere outside the window instead (see
    // MainWindow.StartAutoHideOutsideClickWatch) - for someone who wants to
    // read the tree without it snapping shut the moment the mouse drifts off.
    public bool AutoHideCloseOnMouseLeave { get; set; } = true;

    // "자동 숨김 두께" in the options menu - the width (px) of the bare edge
    // sliver IsAutoHidden collapses to (see MainWindow.xaml.cs's
    // EnterAutoHide/CloseAutoHideReveal). User-adjustable 3~8; 3 matches the
    // original hardcoded value, so existing users see no change until they
    // customize it.
    public double AutoHideSliverWidth { get; set; } = 3;

    // Collapse to a short handle at the middle of the screen edge instead of a
    // sliver running its whole height.
    //
    // The sliver IS the trigger - it opens wherever the pointer touches it - so
    // a full-height one claims the entire edge. That edge is also the route to
    // whatever sits in the screen corners, and a drag passing through it opens
    // the sidebar mid-drag (2026-08-05: dragging files out of the desktop's
    // top-left corner). A handle leaves the rest of the edge alone.
    //
    // False here, TRUE on a first run - see ForFirstRun at the bottom of this
    // file. The two answers are for two different people:
    //
    // Someone already using the app chose nothing when this option appeared, so
    // flipping it would quietly shrink the reveal target they have been aiming
    // at for months. They keep the full edge until they ask otherwise.
    //
    // Someone opening it for the first time is judging it, and the full-height
    // bar reads as unfriendly there - it draws a line down the whole side of the
    // screen, so the screen looks cut in two rather than having something
    // sitting at its edge. The handle is the same thickness and does not, and
    // has been the only mode in use here since it shipped.
    public bool AutoHideUseHandle { get; set; } = false;

    // Whether the peek slides in and out or simply appears.
    //
    // Worth an option rather than a decision: auto-hide is one of this app's
    // defining behaviours, so the motion is on screen constantly, and how it
    // reads depends on the display it is read on - clean on a 144Hz panel,
    // noticeably less so at 60Hz. It is also switched off automatically where
    // sliding would carry the window across a neighbouring monitor, so the
    // instant path has to stay a first-class one either way.
    public bool AutoHideSlide { get; set; } = true;

    // How much of the screen edge the docked window occupies, and where in it.
    //
    // Ratios, not pixels, and that is the whole reason they are shaped this way:
    // a height in pixels is wrong the moment the window lands on a monitor of a
    // different size, and this app recomputes its geometry on every DPI, monitor
    // and taskbar change (PositionToWorkArea). A ratio survives all of them.
    //
    // DockedTopRatio is a fraction of the LEFTOVER space rather than of the work
    // area - 0 is against the top, 1 against the bottom, 0.5 centred - so the
    // band cannot be placed partly off the screen no matter what the two values
    // are, including in a hand-edited file. Both are clamped at the point of use
    // anyway (MainWindow.DockedBand), the same as MaxItemsPerFolder and
    // AutoHideSliverWidth.
    //
    // 1.0 / 0.0 is the full edge, which is what the app has always done.
    public double DockedHeightRatio { get; set; } = 1.0;
    public double DockedTopRatio { get; set; } = 0.0;
    public double TreeFontSize { get; set; } = 12;
    public ObservableCollection<FavoriteEntry> Favorites { get; set; } = new();
    public double FavoritesPanelHeight { get; set; } = 100;

    // Options ("...") menu toggles.
    public bool AutoCollapseFolders { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;

    // Defaults to true to match the tray icon's existing always-on behavior
    // (see App.xaml.cs) from before this toggle existed.
    public bool AlwaysShowTrayIcon { get; set; } = true;

    // "아이콘 방식" - true shows the same icons Windows Explorer does (see
    // ShellIconService), false the bundled PNG set. Default flipped to the
    // Explorer icons one day after the feature shipped (2026-07-21): the
    // familiar look is the better first impression, and the
    // v1.2.0 cohort who preferred the PNG set can (and existing users who
    // never opened the option will) simply see the switch and pick.
    public bool UseShellIcons { get; set; } = true;

    // Color settings ("색상 설정"). Defaults match the original hardcoded
    // brushes in MainWindow.xaml, so existing users see no change until they
    // customize - ColorSettingsWindow's "기본값" button restores these exact
    // values via `new AppSettings()`.
    public string BackgroundColorHex { get; set; } = "#FF1A1A1A";

    // Named after folders specifically (not just "normal/highlight font")
    // since ShowFileIcons made it possible to hide every icon at once - with
    // no icons left at all, folder vs. file rows need their OWN colors to
    // stay visually distinguishable, so this split from a single pair into
    // four. JSON property names kept as the original pre-split ones so
    // existing users' customized colors carry over as their new folder-name
    // color instead of silently resetting to default.
    [JsonPropertyName("NormalFontColorHex")]
    public string FolderNameColorHex { get; set; } = "#FFA8AAAE";
    [JsonPropertyName("HighlightFontColorHex")]
    public string FolderNameHighlightColorHex { get; set; } = "#FFF0F2F6";

    // New - defaults match FolderName*'s own defaults exactly, so a fresh
    // install or an upgrade both look identical to before this split, until a
    // user actually customizes one of the four independently.
    public string FileNameColorHex { get; set; } = "#FFA8AAAE";
    public string FileNameHighlightColorHex { get; set; } = "#FFF0F2F6";

    public string SelectionColorHex { get; set; } = "#FF323438";

    // A shade lighter than BackgroundColorHex (28 vs 26 in RGB) - subtle
    // depth cue, same idea as HeaderBackgroundColorHex being lighter still.
    public string HistoryBackgroundColorHex { get; set; } = "#FF1C1C1C";

    public string HoverBackgroundColorHex { get; set; } = "#FF2A2C32";

    // Split the same way FolderName*/FileName* above were, for the same
    // reason - replaces the single HoverForegroundColorHex this app used to
    // have (deliberately not kept for backward compatibility - unlike the
    // folder/file split above, this one was asked to be a clean replacement).
    public string FolderNameHoverColorHex { get; set; } = "#FFA8AAAE";
    public string FileNameHoverColorHex { get; set; } = "#FFA8AAAE";

    // The "…더 보기 (N개)" overflow row's own text color - previously just
    // inherited FolderNameColorHex at reduced opacity, same default here so
    // existing users see no change until they customize it separately.
    public string ShowMoreColorHex { get; set; } = "#FFA8AAAE";
    public string GuideLineColorHex { get; set; } = "#FF323438";
    public string GuideLineActiveColorHex { get; set; } = "#FF5C5E62";

    // The header/favorites/tree panel-separator lines - previously just
    // reused GuideLineColorHex (see MainWindow.xaml's history), which meant
    // changing the tree's own indent guide line color also silently changed
    // these. Same default so existing users see no change until they
    // customize it separately.
    public string PanelDividerColorHex { get; set; } = "#FF323438";
    // "뷰어 배경" - the image viewer panel's own backdrop, separate from the
    // tree background so a photo can sit on near-black while the tree stays
    // its own colour. Same default as the tree background, so nothing
    // changes until it's customized.
    public string ViewerBackgroundColorHex { get; set; } = "#FF1A1A1A";

    // Lightest of the three background shades (30 vs 28 favorites, 26 tree)
    // for a subtle depth hierarchy across the three panels.
    public string HeaderBackgroundColorHex { get; set; } = "#FF1E1E1E";

    // The auto-hidden sidebar - both the handle and the full-height bar, which
    // share one colour because they are the same thing at two lengths.
    //
    // A new colour row is normally the last resort here (reuse an existing one
    // first), and this is the exception that earns one: it is the only part of
    // the app that sits on the DESKTOP, against whatever wallpaper the user
    // has, rather than against the app's own chrome. Everything else is judged
    // next to the tree.
    //
    // Stored as null until it is actually set, and null means "whatever the
    // sidebar background is". That is what keeps an upgrade invisible: someone
    // who spent time on a custom background gets a handle in that same custom
    // colour, not the shipped default. Reset writes the colour out for real
    // (see ColorSettingsWindow.ResetDefaults_Click) - by then the background
    // beside it has been reset too, so the two still agree.
    // The two below are the only ones written to settings.json; the JSON names
    // are the ordinary ones, so nothing about the file looks unusual.
    //
    // Their C# names deliberately do NOT end in ColorHex. That suffix is what
    // 색상만 내보내기/불러오기 collects by (ColorSettingsWindow.ColorProperties),
    // and a palette file has no business carrying "not set on the machine this
    // came from" - the resolved pair below travels instead.
    [JsonPropertyName("AutoHideHandleColorHex")]
    public string? StoredAutoHideHandleColor { get; set; }
    [JsonPropertyName("LightAutoHideHandleColorHex")]
    public string? StoredLightAutoHideHandleColor { get; set; }

    [JsonIgnore]
    public string AutoHideHandleColorHex
    {
        get => StoredAutoHideHandleColor ?? BackgroundColorHex;
        set => StoredAutoHideHandleColor = value;
    }

    [JsonIgnore]
    public string LightAutoHideHandleColorHex
    {
        get => StoredLightAutoHideHandleColor ?? LightBackgroundColorHex;
        set => StoredLightAutoHideHandleColor = value;
    }

    // "라이트/다크 모드" toggle above the color rows in ColorSettingsWindow -
    // which of the two palettes below is currently active/persisted/applied.
    public bool IsLightMode { get; set; } = false;

    // Light-mode counterpart to each of the 15 dark colors above (the 16th,
    // the auto-hide handle, keeps its own light twin beside it - the two share
    // the fallback logic and read as one thing) - a
    // deliberately hand-picked VS Code Light+-style palette, not a
    // mathematical inversion of the dark values (which tends to look muddy).
    // Kept as their own flat, separately-named properties rather than nested
    // under the dark ones, so existing users' dark customizations keep
    // deserializing into the exact same fields they always have - adding an
    // entirely new nested object here would have needed its own migration
    // path for zero benefit.
    public string LightBackgroundColorHex { get; set; } = "#FFFFFFFF";
    public string LightFolderNameColorHex { get; set; } = "#FF3B3B3B";
    public string LightFolderNameHighlightColorHex { get; set; } = "#FF000000";
    public string LightFileNameColorHex { get; set; } = "#FF3B3B3B";
    public string LightFileNameHighlightColorHex { get; set; } = "#FF000000";
    public string LightSelectionColorHex { get; set; } = "#FFCCE4FF";
    public string LightHistoryBackgroundColorHex { get; set; } = "#FFF5F5F5";
    public string LightHoverBackgroundColorHex { get; set; } = "#FFE8E8E8";
    public string LightFolderNameHoverColorHex { get; set; } = "#FF3B3B3B";
    public string LightFileNameHoverColorHex { get; set; } = "#FF3B3B3B";
    public string LightShowMoreColorHex { get; set; } = "#FF6E6E6E";
    public string LightGuideLineColorHex { get; set; } = "#FFD9D9D9";
    public string LightGuideLineActiveColorHex { get; set; } = "#FFA0A0A0";
    public string LightPanelDividerColorHex { get; set; } = "#FFD9D9D9";
    public string LightViewerBackgroundColorHex { get; set; } = "#FFFFFFFF";
    public string LightHeaderBackgroundColorHex { get; set; } = "#FFF3F3F3";

    // "ko" or "en" (see Services/Strings.cs). Restart-only - Strings.Initialize
    // reads this once at process startup, before any window's XAML loads.
    // Defaults to whatever DetectDefaultLanguage below resolves at the
    // moment a brand-new AppSettings is constructed (no settings.json yet,
    // or an unreadable one) - once saved, this sticks, so a later Windows
    // display-language change doesn't silently flip an existing user's
    // choice out from under them.
    public string Language { get; set; } = DetectDefaultLanguage();

    // Korean Windows installs default to Korean; everything else (including
    // a UI culture we don't otherwise localize for) defaults to English
    // rather than assuming Korean.
    private static string DetectDefaultLanguage()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ko", StringComparison.OrdinalIgnoreCase)
            ? "ko"
            : "en";

    // App-wide default sort ("정렬 기준" submenu) - see FileSystemService.
    // SortField is the live one ("name" | "date" | "type" | "size"); SortByDate
    // is its predecessor, kept written and read for the same reason as
    // FolderSortOverrideEntry.SortField (older builds, and files written by
    // them, still make sense).
    public string SortField { get; set; } = string.Empty;
    public bool SortByDate { get; set; } = false;
    public bool SortDescending { get; set; } = false;

    // Per-folder sort overrides ("정렬" from a specific folder's own right-click
    // menu, kept independent of the app-wide default above until explicitly
    // cleared via "전역 정렬 따르기" or the folder's own override icon) - see
    // FileSystemService.SortOverrides, which mirrors this list at startup and
    // whenever it changes. A path that no longer exists is simply never
    // matched again, same tolerance as ExpandedFolderPaths below.
    public List<FolderSortOverrideEntry> FolderSortOverrides { get; set; } = new();

    // How many items a folder shows before collapsing the rest behind "더
    // 보기" (see Models/FileSystemItem.DisplayCap) - user-adjustable 1~50 from
    // the "..." options menu, for low-resolution screens that want fewer rows.
    // Default lowered from 25 to 20 (2026-07-17).
    public int MaxItemsPerFolder { get; set; } = 20;

    // "들여쓰기 간격" in the options menu - the per-nesting-level indent width in
    // pixels (also drives the expand arrow's column width and the guide
    // line's position beneath it, and the file icon/name alignment shift -
    // see MainWindow.xaml.cs's ApplyLayoutMetrics). User-adjustable 4~24;
    // default lowered from the original hardcoded 16 to 12 (2026-07-17).
    public int TabSpacing { get; set; } = 12;

    // "행 간격" in the options menu - a flat pixel offset added on top of the
    // row's own font-size-scaled vertical padding (see
    // MainWindow.xaml.cs's ApplyLayoutMetrics, which replaced
    // Converters/FontSizeToRowPaddingConverter's job so this second input
    // could be folded in). User-adjustable -4~+8 relative to the existing
    // default, 0 meaning no change from that default.
    public int RowSpacing { get; set; } = 0;

    // Thickness of the overlay scrollbar, and with it the width of the lane
    // reserved for it beside the content (that lane is this plus the 1px
    // divider - see MainWindow.xaml's MinimalScrollViewerTemplate). Exposed as
    // an option because taste on this genuinely splits: the same bar reads as
    // "tidy" to one person and "impossible to grab" to another, and it costs
    // horizontal space in a sidebar that is already narrow. User-adjustable
    // 6~20, defaulting to 12: the 8 it first shipped at was chosen to be
    // unobtrusive, but the bar is a pointer target before it is decoration and
    // 8 was awkward to grab. 6 is there for whoever still wants it hairline.
    public int ScrollBarThickness { get; set; } = 12;

    // Folder icons only - file icons (already distinct per extension) are
    // unaffected either way. A VS Code-minimal-theme-style option: off hides
    // just the folder glyph, leaving the expand arrow and name.
    public bool ShowFolderIcons { get; set; } = true;

    // Same idea as ShowFolderIcons, but for file rows' per-extension icon
    // instead - independent toggle, so either can be off while the other
    // stays on.
    public bool ShowFileIcons { get; set; } = true;

    // "제목 표시줄 타이틀 제거" - hides the "내 PC"/"This PC" text in the title bar
    // (RootPathText), for someone who wants the title bar as bare as possible.
    // Doesn't touch the Debug-only "(DEBUG)" suffix's own logic (see
    // Strings.Initialize) - that's a separate, unrelated concern.
    public bool HideTitleBarTitle { get; set; } = false;

    // The path strip above the footer's filter chips: shows the folder the
    // selection is in, and takes a pasted path + Enter to jump there.
    //
    // On by default. It does cost a permanent tree row, which is the thing a
    // 1080p laptop is short of - but a strip nobody finds is worth nothing,
    // and the install base is still small enough that changing the default
    // layout under existing users is cheap (2026-08-10). Anyone
    // who wants the row back turns it off in 기본 설정.
    public bool ShowPathBar { get; set; } = true;

    // Swaps the favorites panel and the tree between the top and bottom Grid
    // row - see MainWindow.xaml's Row1/Row3 comment and
    // MainWindow.xaml.cs's ApplyFavoritesPosition.
    public bool FavoritesAtBottom { get; set; } = false;

    // Docks against the right edge of the work area instead of the left -
    // see MainWindow.xaml.cs's PositionToWorkArea/ResizeThumb_DragDelta/
    // AnimateWidth, all of which branch on this to keep the right edge
    // anchored instead of the left one.
    public bool DockOnRight { get; set; } = false;

    // Which folders (including drive roots) were expanded when the app last
    // closed, restored on the next launch - see MainWindow.xaml.cs's
    // MainWindow_Loaded/SaveCurrentWidth. A path that no longer exists (drive
    // unplugged, folder deleted/renamed) is silently skipped on restore
    // (FindItemForPath returns null for it), not an error.
    public List<string> ExpandedFolderPaths { get; set; } = new();
    public string? LastSelectedPath { get; set; }

    // 책갈피 rows (MainWindow.ToggleBookmark) - list order is the Ctrl+Alt+L/J
    // cycle order (insertion order). A bookmarked path that no longer exists
    // is skipped when cycling, not an error.
    public List<string> BookmarkPaths { get; set; } = new();

    // What the panel above (or below) the tree shows: "favorites", "bookmarks"
    // or "none". Defaults to favorites so an existing install looks exactly as
    // it did before the choice existed.
    //
    // "none" is not the same as an empty favorites list, even though both end
    // with no panel on screen: the list keeps its entries and comes back when
    // the mode does. Before this, giving that row back to the tree meant
    // deleting favorites (see MainWindow.UpdateFavoritesPanelVisibility, which
    // collapses the row only when the list is empty) - a real cost on a
    // 1080p screen, paid in data.
    public string SidePanelMode { get; set; } = "favorites";

    // Tree text weight: "normal", "bold", or "folders" (folders bold, files
    // normal - the structure reads without every file name thickening with it).
    public string TreeFontWeight { get; set; } = "normal";

    // Which file kinds the tree lists (FileTypeFilter's category keys). EMPTY
    // MEANS EVERYTHING: "전체" is the absence of a filter, not an entry of its
    // own, so a fresh install and a cleared filter are the same state and
    // neither needs a migration.
    public List<string> FileFilterCategories { get; set; } = new();

    // The one user-defined kind ("사용자 지정"), as normalised extensions -
    // lower case, no dots, no duplicates, comma-separated: "psd,ai,fig".
    // Empty means the row is not offered at all.
    //
    // ONE, not a list of named filters: a second one needs a managing list
    // with a − per row (the shape 숨긴 폴더 uses) and a name per entry, while
    // going from one to many later is easy and the reverse is not
    // (2026-08-06). It is selected like any other kind, so it can be combined
    // with 코드 or 이미지 rather than replacing them.
    public string FileFilterCustomExtensions { get; set; } = "";

    // The exclusion list ("제외"), stored the same normalised way as the custom
    // kind above. Deliberately NOT an entry in FileFilterCategories: an
    // exclusion has to be able to hold while 전체 is on, and a category list
    // that isn't empty means 전체 is off. See FileTypeFilter for the rest.
    public string FileFilterExcludeExtensions { get; set; } = "";

    // Whether the list above is armed. Separate from the list so the footer
    // chip can switch the rule off for a moment without the user losing what
    // they typed - every other chip in that strip works that way. Meaningless
    // while the list is empty.
    public bool FileFilterExcludeEnabled { get; set; } = true;

    // Folders the user has taken out of the tree ("이 폴더 숨기기"). Only the
    // tree hides them - the file search still finds what is inside, because a
    // search is a deliberate act of looking, and a file that is plainly there
    // but cannot be found is a worse surprise than seeing a folder you hid
    // (decided 2026-08-02).
    //
    // Kept as paths rather than as a flag on the items for the same reason
    // bookmarks are: item instances are created lazily and thrown away by every
    // refresh, so the truth has to live somewhere that outlives them.
    // A path that no longer exists is simply never matched - no pruning pass,
    // and nothing to go wrong while a drive is briefly away.
    public List<string> HiddenFolderPaths { get; set; } = new();


    // File-search feature (see Services/FileSearchService). The last folder
    // chosen via "폴더 찾기" is remembered so reopening search restores the same
    // scope. Null until the user picks one for the first time. A path that no
    // longer exists is simply re-prompted, same tolerance as the other
    // remembered-path settings above.
    public string? LastSearchFolder { get; set; }

    // Recent search queries, most-recent-first, shown in the search box's
    // history dropdown. Capped in code (see MainWindow.CommitSearchHistory) so
    // it can't grow without bound.
    public List<string> SearchHistory { get; set; } = new();

    // Results-only sort/grouping for the search view, independent of the
    // explorer tree's own sort. 0=folder group (default), 1=name asc, 2=name
    // desc, 3=date asc, 4=date desc - see MainWindow's SearchSortMode enum and
    // SearchSortButton_Click. Remembered across sessions.
    public int SearchSortMode { get; set; } = 0;

    // The settings a machine gets the very FIRST time the app runs on it -
    // no settings.json anywhere, not even the pre-rebrand one (see
    // SettingsService.Load, which is the only caller and is careful to tell a
    // missing file apart from an unreadable one: a corrupt file is an existing
    // install having a bad day, not a new user).
    //
    // Only entries that should differ from an UPGRADE belong here. Every other
    // default stays where it is declared above, because the rule this project
    // works to is that upgrading changes nothing on screen - and a first run
    // has no screen to change.
    public static AppSettings ForFirstRun() => new()
    {
        AutoHideUseHandle = true,
    };
}
