using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
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

    // Puts the panel on the other side of the tree than the dock would choose.
    // Left alone, the panel takes the screen-INTERIOR side - right of the tree
    // while docked left, left of it while docked right - so the tree always
    // keeps the screen edge. This turns that around for anyone who wants the
    // opposite (asked 2026-08-15). See MainWindow.xaml.cs's ApplyViewerSide,
    // which is the one place the choice is turned into _viewerOnLeft, and note
    // that the side the panel sits on is NOT the same question as which window
    // edge stays pinned - see ViewerGrowsLeftward there.
    public bool ViewerSideSwapped { get; set; } = false;

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

    // The largest a filmstrip thumbnail may be fetched and kept, in pixels. The
    // strip asks for what the current cell needs; this caps that, so a taller
    // strip is drawn from the same picture rather than a bigger one.
    //
    // It is the multiplier on a big folder: 2,402 files are 86MB of thumbnails
    // at 128 and 318MB at 256, and none of those extra pixels are drawn at the
    // default cell height. Defaulted low deliberately - the machines this costs
    // the most are the ones least able to pay it (2026-08-12).
    public int ViewerThumbnailMaxSize { get; set; } = 128;

    // Pulls an HDR film's colour back for an SDR screen (see
    // HdrToneMapEffect). Off by default and remembered once switched on:
    // applying it to a film that is NOT HDR makes it worse, and nothing here
    // yet reads the file's colour flags to tell them apart - so the choice is
    // the eye's, and it is worth only making once.
    public bool ViewerHdrToneMap { get; set; } = false;

    // The dials on that correction, in the shader's own units rather than as
    // percentages: 100 is where 100-nit diffuse white lands at screen white,
    // and 1.0 means "leave it alone" - so the defaults say what they mean, and
    // a settings file edited by hand still does.
    //
    // These four are the LAST USED values, not the only ones. Each film keeps
    // its own once it has been tuned (VideoMarkEntry.Hdr*); these are what a
    // film that has never been touched starts from, so a preference set on one
    // carries to the next without overwriting a film that was already answered.
    public double ViewerHdrExposure { get; set; } = 100;
    public double ViewerHdrSaturation { get; set; } = 1.15;
    public double ViewerHdrContrast { get; set; } = 1.0;

    // Whether the strip fetches the WHOLE folder rather than only the part
    // around what is on screen. Off by default because it is a trade, not an
    // improvement: on a folder of 1359 photos over SMB it is a few minutes of
    // background reading and 100-250MB of thumbnails held in memory, in exchange
    // for a strip that never has a gap in it once it has settled. That is a good
    // trade for someone working through a shoot and a bad one for someone
    // glancing at a folder, which is exactly the shape a setting is for.
    public bool ViewerPrecacheThumbnails { get; set; } = false;

    // Whether a double-click (and Enter, which means the same thing) on a
    // picture, a track or a film hands it to this app's own panel instead of to
    // whatever Windows opens it with.
    //
    // MEDIA ONLY, and that is not a simplification - "our panel" has no meaning
    // for a .txt or a .psd, so everything else goes to the default program
    // whatever this says. The menu label names the three kinds for that reason.
    //
    // Off by default. Handing a file to Windows is what a double-click has
    // always done here and what it does everywhere else in the OS, so the app
    // does not quietly change it on anyone; someone who wants the panel says so
    // once. Worth most with the panel CLOSED - with it already open, a single
    // click has previewed the file there anyway.
    public bool OpenMediaInViewer { get; set; } = false;

    // The panel opens on a media file without being asked - "매번..좀
    // 귀찮군요" (2026-08-15).
    //
    // OPENS ONLY. It shut on a non-media file too for the few hours between
    // being built and being tried, and the author cut that half: opening is a
    // convenience, closing is a decision, and the panel's width belongs to
    // whoever set it. Folders were already exempt for the same reason at a
    // faster rate - a tree is WALKED through them.
    //
    // It acts when the selection SETTLES rather than on each change; see
    // ScheduleViewerAutoToggle for why that distinction is the whole feature.
    //
    // Off by default: an app that moves the window on its own has to be asked.
    public bool ViewerFollowsSelection { get; set; } = false;

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

    // Network places added by hand, shown as roots under the drive letters.
    //
    // WHY THIS EXISTS: the tree's roots come from DriveInfo.GetDrives(), which
    // answers with drive LETTERS and nothing else. A share that Windows has
    // mapped to a letter has always worked here - that is what a NAS on Y:
    // is - but a UNC path used as a UNC path (\\NAS\공유) could not be reached
    // at all, not even by typing it into the path bar: NavigateToPath walks
    // down from a root, and there was no root it began with. WebDAV is the same
    // story - mapped to a letter it has always worked, unmapped it could not be
    // reached.
    //
    // A LIST OF STRINGS, in the order they were added, because that is what
    // the tree shows. Kept out of presets deliberately (see AppPreset): a
    // place the user typed in is data, not a shape.
    //
    // Nothing here is load-bearing. A location that stops answering is drawn
    // the same way a sleeping mapped drive is - the row stays so it can be
    // clicked again - and one that is gone for good is removed from the menu
    // it was added from.
    public List<string> NetworkLocations { get; set; } = new();

    // What an UNMODIFIED drag inside the tree means. On, it follows Explorer:
    // a move within the same volume, a copy across volumes, with Shift and Ctrl
    // forcing either one anywhere. Off, a plain drag always copies and Shift is
    // the way to move.
    //
    // ON, after being built off (2026-08-13) and turned round the same day on
    // the user's call: a drag that leaves the original behind is what someone
    // coming from Explorer reads as the feature being missing, which is how
    // this started. The risk that argued for off is real and is written down
    // where the gesture lives - a click with a few pixels of travel has read as
    // a drop here before, and a stray move is a file quietly somewhere else
    // with no undo in this app. The switch is what that risk buys: anyone who
    // wants a drag that can never take a file away turns it off.
    public bool DragMovesInsideTree { get; set; } = true;

    // 전체 표시. Its own switch rather than a value past the top of the count's
    // range, which is what it was for half a day: 51 meaning "all" made the
    // number stop being a number, and the stepper's readout - two digits wide -
    // had to draw a word inside it.
    //
    // The count below keeps its own value while this is on, so turning it off
    // comes back to the number that was there rather than to a default.
    public bool ShowAllItemsPerFolder { get; set; } = false;

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

    // What happens when a file finishes playing: "off" (stay on it, which is
    // what the panel always did), "all" (the next one in the folder), "one"
    // (the same file again) or "shuffle".
    //
    // A STRING rather than an enum, like every other choice in this file: the
    // settings are meant to survive being opened in an editor, and a number
    // whose meaning lives in a C# file is not a setting anyone can read. An
    // unknown value reads as "off", so a typo costs the feature and not the
    // launch.
    //
    // Off by default. Music wants this and a folder of films does not, and the
    // panel is not a player anyone opened on purpose - it is what a selected
    // file turned into.
    public string ViewerRepeat { get; set; } = "off";

    // Sound carries on while the tree moves somewhere else - the panel normally
    // drops what it is playing the moment another file is selected, which meant
    // putting music on and then working in another folder was not possible
    // without a second app (2026-08-11).
    //
    // Off by default, and NOT part of a preset: it is a thing you switch on for
    // an hour, not part of what the app looks like. AUDIO ONLY; a film's picture
    // is the point of it, so there is nothing to carry.
    public bool ViewerBackgroundPlay { get; set; } = false;

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

    // The handle's own thickness, separate from the sliver's above because the
    // two are not the same object. The sliver is a TRIGGER - it is meant to sit
    // on the edge unnoticed until the pointer touches it, which is why 3px is
    // its sensible floor. The handle is a CONTROL, and has to be found by eye
    // before it can be aimed at.
    //
    // It used to share the number above, floored at 6 - so the sliver's own
    // "without eating much screen edge" ceiling of 8 was quietly the handle's
    // ceiling too. 6px of the sidebar's background colour (which is what the
    // handle's default colour follows) is a bar that vanishes outright against
    // a dark wallpaper, and on a multi-monitor setup it sits on the seam
    // between two displays where nothing draws the eye to it. That is a bug
    // report from 2026-08-13 (see MainWindow.RestoreFromOutside for the other
    // half of it).
    //
    // 8 rather than the 6 it used to floor at: a deliberately small nudge. The
    // handle's problem was never mostly its thickness - it was its HEIGHT and
    // its colour - and a bar thick enough to be found by width alone is a bar
    // that has started eating the screen edge.
    public double AutoHideHandleWidth { get; set; } = 8;

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
    // 슬라이드 쇼 - how long each picture is held, in seconds. The RUNNING
    // state deliberately does not live here: an app that started moving
    // pictures by itself on launch would be answering a question nobody asked
    // this session. Turning it on is one menu row away.
    public int SlideshowSeconds { get; set; } = 5;
    // 잠금 화면's clock over the panel. It was a slideshow row for an afternoon
    // and came out again the same day (2026-08-16): the show is when it is most
    // wanted, but it is not ABOUT the show - it draws on whatever the panel is
    // showing, and tying it to one mode meant it could not be turned on to look
    // at a single picture. F9 is the switch; this is where the answer lives.
    //
    // OFF by default: it covers part of the picture, and someone opening a
    // picture opened it to see the picture.
    public bool ViewerClock { get; set; } = false;

    // 시계 크기, as a multiple of the size the panel would choose on its own
    // (asked 2026-08-16, by someone who saw it).
    //
    // A MULTIPLIER rather than a point size, because the thing being adjusted
    // is not a font: the clock is sized as a share of the surface it sits on,
    // so the same number has to mean the same thing in a 240px column and on a
    // 4K cover. A stored point size would be right on one of those and absurd
    // on the other, and the panel would go on overruling it either way.
    //
    // It scales the CEILING with it, which is the point of asking for 150%: the
    // ceiling exists so a full cover does not become a backdrop for a number,
    // and someone turning this up has said that is what they want.
    // 75%, not 100% (2026-08-16). The panel's own sizing was tuned before there
    // was a dial, so what used to be the only size is now the ladder's middle
    // rung - and on the panel it reads as large rather than as a default.
    // Anyone who wants the old size is one step up.
    //
    // ONE PLACE, because three other spots answer with the default when the
    // stored value is off the ladder (Sane below, the getter in MainWindow, and
    // the stepper's starting index). Left as literals they drift apart, and the
    // symptom of that is a dial that jumps somewhere nobody chose.
    public const double DefaultViewerClockScale = 0.75;

    public double ViewerClockScale { get; set; } = DefaultViewerClockScale;

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

    // 즐겨찾기·북마크 패널의 행 이름, at the same three states a folder name has.
    // The panel drew FolderName* until 2026-08-17, which meant a palette could
    // not tell the two lists apart - and the panel is a different list sitting
    // directly above the tree, not more of it.
    //
    // Defaults are the folder name's own values in both themes, so an upgrade
    // looks identical until one of the three is picked.
    public string PanelNameColorHex { get; set; } = "#FFA8AAAE";
    public string PanelNameHighlightColorHex { get; set; } = "#FFF0F2F6";
    public string PanelNameHoverColorHex { get; set; } = "#FFA8AAAE";

    // The "…더 보기 (N개)" overflow row's own text color - previously just
    // inherited FolderNameColorHex at reduced opacity, same default here so
    // existing users see no change until they customize it separately.
    public string ShowMoreColorHex { get; set; } = "#FFA8AAAE";
    public string GuideLineColorHex { get; set; } = "#FF323438";
    public string GuideLineActiveColorHex { get; set; } = "#FF5C5E62";

    // 펼침기호 - the chevron in front of every folder. It was a literal on the
    // toggle's own template and so had no theme at all: dark and light drew the
    // same grey, which is the only colour in the app that did (2026-08-15).
    // Its HOVER state is not stored - that has always been ForegroundText, i.e.
    // already the user's, and a second row would only let the two come apart.
    public string ExpanderColorHex { get; set; } = "#FF8A8A8A";

    // The footer's file-kind chips. Fixed per theme until 2026-08-15, and the
    // reason recorded then still holds for what it actually said: these must not
    // be DERIVED from the tree's selection colour, which is often a strong blue
    // and put a shout in a strip meant to be read at a glance. A row of their
    // own is a different thing - it is the user saying what the strip should
    // look like, not the app guessing from somewhere else.
    //
    // Only the LIT states are stored. An unlit chip draws in ForegroundText at
    // the strip's own 0.65, which is already the user's colour.
    public string FilterChipCheckedBackgroundColorHex { get; set; } = "#FF5A5A5A";
    public string FilterChipCheckedForegroundColorHex { get; set; } = "#FFFFFFFF";
    // 제외 칩 - the one control in the strip that REMOVES, which is why it
    // carries a warm hue instead of the app's blue accent.
    public string FilterChipExcludeColorHex { get; set; } = "#FFE08C82";
    public string FilterChipExcludeCheckedBackgroundColorHex { get; set; } = "#FF8A423A";

    // The header/favorites/tree panel-separator lines - previously just
    // reused GuideLineColorHex (see MainWindow.xaml's history), which meant
    // changing the tree's own indent guide line color also silently changed
    // these. Same default so existing users see no change until they
    // customize it separately.
    public string PanelDividerColorHex { get; set; } = "#FF323438";
    // Whether those lines are drawn at all. Off makes the brush transparent
    // rather than collapsing anything: the lines are 1px elements that other
    // things are laid out against, so hiding them by Visibility would shift
    // the whole window by a pixel in several places, and a plain borderless
    // look is the point rather than a slightly different layout.
    //
    // One flag for both themes. It is a taste about the app's SHAPE, not a
    // colour, and someone who does not want lines does not want them in the
    // dark theme only. The colour rows for both themes are kept and untouched,
    // so turning it back on restores exactly what was there.
    public bool ShowPanelDividers { get; set; } = true;
    // "멀티미디어 패널 배경" - the panel's own backdrop, separate from the
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
    // The panel's three, light twins - see PanelNameColorHex above.
    public string LightPanelNameColorHex { get; set; } = "#FF3B3B3B";
    public string LightPanelNameHighlightColorHex { get; set; } = "#FF000000";
    public string LightPanelNameHoverColorHex { get; set; } = "#FF3B3B3B";
    public string LightShowMoreColorHex { get; set; } = "#FF6E6E6E";
    public string LightGuideLineColorHex { get; set; } = "#FFD9D9D9";
    public string LightGuideLineActiveColorHex { get; set; } = "#FFA0A0A0";
    // A shade darker than the dark theme's, because it now has white to clear
    // rather than a dark ground - and it keeps the same relationship the dark
    // theme has, a quiet step away from the row's own text (#3B3B3B).
    public string LightExpanderColorHex { get; set; } = "#FF7A7A7A";
    // The lit chip takes a BLUE on light where dark takes a grey, and the split
    // is the point: on dark, grey with white on it is enough to lift the chip
    // off the seven quiet ones, while on light grey had nothing to push against
    // - the whole strip is already pale. The blue is the bookmark ribbon's own
    // #4A90E2, and reusing the accent the app already draws with is the part
    // worth keeping: one accent in two places rather than a second one invented
    // for this strip.
    public string LightFilterChipCheckedBackgroundColorHex { get; set; } = "#FF4A90E2";
    public string LightFilterChipCheckedForegroundColorHex { get; set; } = "#FFFFFFFF";
    // Darker on light and lighter on dark, the same way every other pair here
    // splits: each has to clear its own ground.
    public string LightFilterChipExcludeColorHex { get; set; } = "#FFB3453B";
    public string LightFilterChipExcludeCheckedBackgroundColorHex { get; set; } = "#FFB3453B";
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

    // Drive roots, split off from ShowFolderIcons on request (2026-08-16).
    //
    // They ARE folders, so they had always followed that switch, and the split
    // is about what each switch is for rather than about what a drive is.
    // Turning folder icons off answers "the same mark on hundreds of rows says
    // nothing" - and the drive icons are the opposite case: half a dozen of
    // them, each different from the next, the only icons in the window that
    // tell you what a row IS rather than that it is a folder.
    public bool ShowDriveIcons { get; set; } = true;

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

    // Which colour rows are LINKED in the colour window - by the swatch name
    // the hex boxes already address them with. Setting one linked row sets
    // every other linked row in its group, so a palette can be kept in step
    // without editing the same colour twice (see ColorSettingsWindow's
    // ChainGroups). Membership rather than a group switch: the name pairs are
    // usually wanted joined and the four backgrounds usually are not.
    //
    // NOT in presets. A preset carries what the app LOOKS like; this is about
    // how the colour window edits, and a preset that silently relinked rows
    // would change what the next edit does rather than what is on screen.
    public List<string> ChainedColorRows { get; set; } = new();

    // The soft veil over the first and last rows of the tree and the side
    // panels, up only while there is more list that way (see MainWindow's
    // UpdateEdgeShades). Lives with the COLOURS rather than in the options
    // menu, and that was a deliberate call: named among the app's behaviour
    // toggles it reads as a window shadow for the whole app, where beside the
    // palette it reads as what it is - shading inside these lists.
    public bool TreeEdgeShades { get; set; } = true;

    // Swaps the favorites panel and the tree between the top and bottom Grid
    // row - see MainWindow.xaml's Row1/Row3 comment and
    // MainWindow.xaml.cs's ApplyFavoritesPosition.
    public bool FavoritesAtBottom { get; set; } = false;

    // Docks against the right edge of the work area instead of the left -
    // see MainWindow.xaml.cs's PositionToWorkArea/ResizeThumb_DragDelta/
    // AnimateWidth, all of which branch on this to keep the right edge
    // anchored instead of the left one.
    public bool DockOnRight { get; set; } = false;

    // Named snapshots of the app's shape, applied from the header's right-click
    // menu - see AppPreset, which is also the one place deciding what a preset
    // contains. Capped at AppPreset.MaxPresets.
    public List<AppPreset> Presets { get; set; } = new();

    // Which one was put on last, so the menu can mark it. REMEMBERED rather
    // than worked out: the first version compared every field a preset holds
    // against the live settings, and one value drifting - a width nudged, a
    // ratio recomputed - was enough for the mark to vanish while the app was
    // plainly still in that shape (2026-08-11).
    //
    // By NAME, not by index: an index moves when a slot above it is deleted,
    // and a name is what the user is looking at. Two slots named the same would
    // both be marked, which is a fair answer to having named them the same.
    public string ActivePreset { get; set; } = string.Empty;

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

    // ----- 값이 레이아웃에 닿기 전에 -------------------------------------------
    //
    // Run on everything that comes in from outside: the settings file, an
    // imported settings file, and a preset. Each of those can carry a number
    // this build never wrote - a file edited by hand, one brought from another
    // machine, one left by a version that meant something else by the same
    // field.
    //
    // It deliberately does NOT repeat the tunable ranges (6-20 for the
    // scrollbar, 1-50 for the display cap, and so on). Those are clamped where
    // they are used, and a second copy here would be a second source of truth
    // that can drift from the first - which is a worse failure than the one it
    // guards against, because it is silent.
    //
    // What it does enforce is the part WPF will throw over, and that no clamp
    // at a use site can save: a value has to be a finite number, and a length
    // has to be non-negative. Math.Clamp does not help there - Clamp(NaN, 0, 1)
    // is NaN - so the finiteness test has to come first. new GridLength() on a
    // negative or NaN double throws, and FavoritesPanelHeight is built into one
    // on the way to the screen.
    public void Normalize()
    {
        ExpandedWidth = Sane(ExpandedWidth, 240, min: 1);
        ViewerWidth = Sane(ViewerWidth, 360, min: 1);
        ViewerFilmstripCellHeight = Sane(ViewerFilmstripCellHeight, 64, min: 1);
        AutoHideSliverWidth = Sane(AutoHideSliverWidth, 3, min: 1);
        AutoHideHandleWidth = Sane(AutoHideHandleWidth, 8, min: 1);
        SlideshowSeconds = (int)Sane(SlideshowSeconds, 5, min: 3);
        TreeFontSize = Sane(TreeFontSize, 12, min: 1);
        // 배수라 0이 곧 글자 없음이다 - 0은 FontSize에서 예외이고, 그 앞의
        // Math.Clamp는 NaN을 NaN 그대로 통과시킨다. 범위는 쓰는 자리가 정한다.
        ViewerClockScale = Sane(ViewerClockScale, DefaultViewerClockScale, min: 0.1);
        FavoritesPanelHeight = Sane(FavoritesPanelHeight, 100, min: 0);
        DockedHeightRatio = Sane(DockedHeightRatio, 1.0, min: 0, max: 1);
        DockedTopRatio = Sane(DockedTopRatio, 0.0, min: 0, max: 1);
        NormalizeColors();
        NormalizePresets();
    }

    // ----- 프리셋 목록이 밖에서 들어올 때 ---------------------------------------
    //
    // 위의 숫자들과 달리 여기서 막는 것은 값이 아니라 NULL이다. System.Text.Json은
    // 선언된 초기값을 무시하고 `"Presets": null`을 그대로 null로 넣으므로, 손으로
    // 고친(혹은 반쯤 쓰다 만) 파일 하나가 헤더 우클릭 한 번에 앱을 끝낸다 -
    // 메뉴를 여는 것이 `_settings.Presets.Count`이기 때문이다. 목록 안의 null
    // 원소와 `"Values": null`도 같은 자리에서 같은 방식으로 터진다.
    //
    // 개수는 손대지 않는다. 다섯을 넘겨 적어 둔 파일이 있으면 여섯 번째도 그대로
    // 보이고 `프리셋 추가`만 사라진다. 넘치는 것을 잘라내는 쪽이 깔끔해 보이지만
    // 그건 사용자가 적어 둔 것을 앱이 조용히 지우는 일이고, 이 파일의 나머지
    // 전체가 그 반대 방향으로 쓰여 있다(읽을 수 없는 파일도 지우지 않고 옆에
    // 남겨 둔다).
    private void NormalizePresets()
    {
        if (Presets is null)
        {
            Presets = new();
            return;
        }

        Presets.RemoveAll(preset => preset is null);

        for (int i = 0; i < Presets.Count; i++)
        {
            var preset = Presets[i];
            preset.Values ??= new();
            // 이름 없는 슬롯은 지우지 않고 이름을 준다. 빈 줄은 메뉴에서 누를 수
            // 있으면서 아무것도 아닌 것으로 보이고, 그 프리셋이 담고 있는 모양은
            // 멀쩡하다.
            if (string.IsNullOrWhiteSpace(preset.Name))
            {
                preset.Name = $"#{i + 1}";
            }
        }
    }

    private static double Sane(double value, double fallback, double min, double max = double.MaxValue)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    // ----- 손으로 고친 색 문자열 -----------------------------------------------
    //
    // THE ONE PLACE A TYPO COULD STOP THE APP STARTING. Thirty-four colour
    // fields are stored as text, and every one of them ends up at
    // ColorConverter.ConvertFromString on the way to a brush - which THROWS on
    // anything it does not recognise. The call sites are written as
    // `ConvertFromString(hex) is Color c`, and that pattern only guards null:
    // the exception is raised before there is anything to test. Colours are
    // applied while the window is being built, so the failure is a launch that
    // does not happen - the one kind nobody can fix from inside the app.
    //
    // Measured rather than assumed (2026-08-13). Rejected: "zzz", "#GGGGGG",
    // "#FF2E7D3" (a digit short), "" and a value with a zero-width space in it.
    // Accepted: leading and trailing spaces, and the 3-digit form.
    //
    // TWO STEPS, in this order, because they answer different mistakes:
    //   1. CLEAN, then re-test. A value that came through a chat window or a
    //      web page can carry a zero-width space or a stray control character
    //      that nobody can see in an editor either. Those are repaired, because
    //      the colour the person wrote is right there and throwing it away over
    //      an invisible character would be the surprise.
    //   2. Only if it still will not parse, take THIS FIELD'S OWN DEFAULT from
    //      a fresh AppSettings. Not black, not white: one bad line costs that
    //      one colour and the palette around it is left alone.
    //
    // Found by name rather than listed, the same as the export path's own
    // lookup - a 35th colour is covered without anyone remembering this code
    // exists. It is a WIDER net than that one on purpose: export deliberately
    // leaves out the two nullable handle colours (see their note above), and
    // "not set" is a legitimate value here that must be left as null.
    private static PropertyInfo[] ColorProperties { get; } =
        typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite
                && p.Name.Contains("Color", StringComparison.Ordinal))
            .ToArray();

    private void NormalizeColors()
    {
        AppSettings? defaults = null;
        foreach (var property in ColorProperties)
        {
            // null means "nothing stored", which the two handle colours use to
            // mean "follow the background". Only text can be malformed.
            if (property.GetValue(this) is not string stored)
            {
                continue;
            }

            if (IsColor(stored))
            {
                continue;
            }

            string cleaned = CleanColorText(stored);
            if (IsColor(cleaned))
            {
                property.SetValue(this, cleaned);
                continue;
            }

            defaults ??= new AppSettings();
            property.SetValue(this, property.GetValue(defaults));
        }
    }

    private static bool IsColor(string text)
    {
        try
        {
            return System.Windows.Media.ColorConverter.ConvertFromString(text)
                is System.Windows.Media.Color;
        }
        catch (FormatException)
        {
            return false;
        }
        // ConvertFromString raises this for input it cannot even tokenize, and
        // it is the same answer as a FormatException as far as this is
        // concerned: not a colour.
        catch (NotSupportedException)
        {
            return false;
        }
    }

    // Whitespace the converter already forgives, so this is only about what an
    // editor will not show: zero-width and directional marks, the BOM when it
    // has been pasted into the middle of a value, and any control character.
    //
    // WRITTEN AS CODE POINTS, never as the characters themselves. A literal
    // zero-width space in this file would be invisible in every editor that
    // opens it, including to whoever comes to change this list.
    private static bool IsInvisible(char c)
        => char.IsControl(c)
        || c is (char)0x200B    // zero-width space
            or (char)0x200C     // zero-width non-joiner
            or (char)0x200D     // zero-width joiner
            or (char)0x200E     // left-to-right mark
            or (char)0x200F     // right-to-left mark
            or (char)0x2060     // word joiner
            or (char)0xFEFF;    // BOM, when it has been pasted mid-value

    private static string CleanColorText(string text)
    {
        var kept = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (IsInvisible(c))
            {
                continue;
            }
            kept.Append(c);
        }

        return kept.ToString().Trim();
    }
}
