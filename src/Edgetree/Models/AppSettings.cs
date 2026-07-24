using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace SidebarExplorer.App.Models;

public class AppSettings
{
    public double ExpandedWidth { get; set; } = 240;

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
    // Explorer icons one day after the feature shipped (user call,
    // 2026-07-21): the familiar look is the better first impression, and the
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

    // Lightest of the three background shades (30 vs 28 favorites, 26 tree)
    // for a subtle depth hierarchy across the three panels.
    public string HeaderBackgroundColorHex { get; set; } = "#FF1E1E1E";

    // "라이트/다크 모드" toggle above the color rows in ColorSettingsWindow -
    // which of the two palettes below is currently active/persisted/applied.
    public bool IsLightMode { get; set; } = false;

    // Light-mode counterpart to each of the 15 dark colors above - a
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

    // File/folder sort order ("정렬" submenu) - see Services/FileSystemService.
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

    // "탭간격" in the options menu - the per-nesting-level indent width in
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
}
