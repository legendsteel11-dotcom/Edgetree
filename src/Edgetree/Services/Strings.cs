namespace SidebarExplorer.App.Services;

// Language switch is restart-only (not live): fields default to Korean and
// Initialize(), called once in App.OnStartup before base.OnStartup(e) builds
// the StartupUri window, overwrites them to English if that's the saved
// choice - so every x:Static reference throughout the XAML resolves to the
// right language the first (and only) time it's evaluated, and every plain
// code-behind reference just reads a field like any other string constant.
public static class Strings
{
    // Context menu (file/folder rows)
    public static string MenuAddFavorite = "즐겨찾기에 추가";
    public static string MenuBookmark = "북마크";
    public static string MenuBookmarkAdd = "북마크 추가";
    public static string MenuBookmarkRemove = "북마크 해제";
    public static string MenuBookmarkList = "북마크 목록";
    public static string MenuBookmarkListEmpty = "북마크 없음";
    public static string MenuBookmarkClearAll = "전체 해제";
    // "이 폴더" rather than just "숨기기": the row under the cursor is what goes,
    // and a bare verb next to 잘라내기/삭제 reads like it might mean the
    // selection or the whole view.
    // File-kind filter. "전체" is deliberately worded as a state, not a
    // category - picking it clears the rest.
    // "파일 종류"로 시작했다가 바꿈 - 그것만으로는 무엇을 하는 줄인지 안 읽힘.
    // 라벨이 그 자리에서 동작을 말해야 함(설명은 아무도 안 읽으므로).
    // ----- 프리셋 ------------------------------------------------------------
    //
    // The saved shapes of the whole app, at the TOP of the header's right-click
    // menu - no 프리셋 parent row, because the names are the rows.
    //
    // "현재 상태를 프리셋으로 지정" was the first wording and it was explaining
    // rather than labelling: anyone in this menu, looking at a list of their own
    // saved presets, already knows what adding one takes a snapshot of.
    public static string MenuPresetAdd = "프리셋 추가";
    // First item inside a preset's own submenu, and the only one of the four
    // that is not about editing the preset.
    public static string MenuPresetApply = "적용";
    // THE PRIMARY ONE, and it does both: the name box comes up with the slot's
    // current name in it, and pressing 확인 stores the app's present shape under
    // whatever name is left in the box. Renaming and overwriting used to be two
    // separate items, and doing only the first left a preset renamed but still
    // holding the old shape - a half-finished update with nothing to show it
    // (2026-08-11).
    public static string MenuPresetOverwrite = "덮어쓰기…";
    // Kept for the case the item above cannot serve: fixing a name WITHOUT
    // giving up the shape already stored. It sat directly under 덮어쓰기 as
    // "이름만 바꾸기" for a while - the qualifier stopped earning its place once
    // the two were one under the other in a four-item menu, where what each
    // does is read off the pair.
    public static string MenuPresetRename = "이름 바꾸기…";
    public static string MenuPresetDelete = "삭제";
    public static string PresetNameTitle = "프리셋 이름";
    // The name box is the same box for all three, so its TITLE is the only
    // thing saying which one is happening.
    public static string PresetSaveTitle = "현재 상태로 저장";
    public static string PresetRenameTitle = "이름 바꾸기";
    public static string PresetNameHint = "창 위치·크기·도킹·색상·파일 종류·현재 폴더가 함께 저장됩니다";
    // The default name for slot N. Deliberately dull - the user renames it to
    // what the shape is for, and a clever default would get in the way of that.
    public static string PresetDefaultName = "프리셋{0}";

    public static string MenuFileFilter = "표시할 파일 종류";
    // 푸터 칩은 여덟 개가 한 줄에 서야 해서 가장 긴 라벨만 줄여 씀. 나머지는
    // 메뉴와 같은 말을 그대로 쓴다 - 두 곳이 다른 이름을 부르면 같은 것인지
    // 알 수 없으므로.
    public static string FilterChipExecutable = "실행";
    public static string MenuFileFilterAll = "전체";
    public static string MenuFileFilterCode = "코드";
    public static string MenuFileFilterImage = "이미지";
    public static string MenuFileFilterDocument = "문서";
    public static string MenuFileFilterMedia = "미디어";
    public static string MenuFileFilterArchive = "압축";
    public static string MenuFileFilterExecutable = "실행 파일 · 바로가기";
    public static string MenuFileFilterOther = "기타";
    // Opens the input. The ellipsis is the whole promise that this row asks
    // something rather than switching something, which is what every other row
    // in this list does.
    public static string MenuFileFilterCustomEdit = "사용자 지정…";
    public static string FilterCustomTitle = "사용자 지정 확장자";
    // At the point of use, because nobody reads help - it has to say both the
    // separator and that a dot is optional, in one line.
    public static string FilterCustomHint = "쉼표로 구분 · psd, ai, .fig";
    // Under the box, only while it holds nothing: an empty box is the way to
    // remove the filter, and that is not guessable.
    public static string FilterCustomEmptyHint = "비워 두고 확인하면 사용자 지정이 없어집니다";
    // The other direction. Same ellipsis promise as 사용자 지정… above - both
    // rows ask, the rest switch.
    public static string MenuFileFilterExcludeEdit = "제외…";
    public static string FilterExcludeTitle = "제외할 확장자";
    // Says what makes this one different in the one place it will be read: it
    // does not join the other kinds, it overrules them.
    public static string FilterExcludeHint = "쉼표로 구분 · 여기 적은 것은 항상 숨깁니다";
    public static string FilterExcludeEmptyHint = "비워 두고 확인하면 제외가 없어집니다";
    public static string ButtonOk = "확인";
    public static string ButtonCancel = "취소";

    public static string MenuFontWeight = "글꼴 굵기";
    public static string MenuFontWeightNormal = "보통";
    public static string MenuFontWeightBold = "굵게";
    public static string MenuFontWeightFoldersOnly = "폴더만 굵게";
    public static string MenuFontWeightFilesOnly = "파일만 굵게";

    // The panel beside the tree: which list it shows, or none at all.
    public static string MenuSidePanel = "패널 표시";
    public static string MenuSidePanelFavorites = "즐겨찾기";
    public static string MenuSidePanelBookmarks = "북마크";
    public static string MenuSidePanelNone = "표시 안 함";

    public static string MenuHideFolder = "이 폴더 숨기기";
    // Replaces the line above while several rows are picked. Files among them
    // are skipped, so it names folders rather than "선택 항목".
    public static string MenuHideSelectedFolders = "선택한 폴더 숨기기";
    public static string MenuHiddenFolderList = "숨긴 폴더";
    public static string MenuHiddenFolderListEmpty = "숨긴 폴더 없음";
    public static string MenuUnhideFolder = "숨김 해제";
    // The button on every row of the bookmark / hidden-folder lists. One word
    // for both because the row it sits on already says what is being released.
    public static string MenuListRowRemove = "해제";
    public static string HiddenClearAllConfirmTitle = "숨긴 폴더 전체 해제";
    public static string HiddenClearAllConfirmBody = "숨긴 폴더 {0}개를 모두 다시 표시하겠습니까?";
    public static string BookmarkClearAllConfirmTitle = "북마크 전체 해제";
    public static string BookmarkClearAllConfirmBody = "북마크 {0}개를 모두 해제하겠습니까?";
    public static string FavoriteClearAllConfirmTitle = "즐겨찾기 전체 해제";
    public static string FavoriteClearAllConfirmBody = "즐겨찾기 {0}개를 모두 해제하겠습니까?";
    public static string BookmarkShortcutNext = "다음 북마크";
    public static string BookmarkShortcutPrev = "이전 북마크";
    public static string MenuNewFolder = "새 폴더";
    public static string MenuRefresh = "새로고침";
    // THE OPEN GROUP NAMES ITS DESTINATIONS. 열기 on its own said nothing about
    // where, which only became a question once the app grew a viewer of its own
    // - so the two that hand the file OUT say so, and the one that keeps it here
    // says nothing at all. That asymmetry is the point: the unmarked form is
    // "in here". 기본 프로그램에서 열기 is not new wording either - it is what the
    // viewer's own menu has always called this exact action (see
    // ViewerOpenExternally, which shares its handler).
    public static string MenuOpen = "기본 프로그램에서 열기";
    // One item whose word follows the file: a picture is looked at, a track or a
    // film is played.
    public static string MenuViewHere = "보기";
    public static string MenuPlayHere = "재생";
    public static string MenuOpenWith = "연결 프로그램";
    public static string MenuCut = "잘라내기";
    public static string MenuCopy = "복사";
    public static string MenuPaste = "붙여넣기";
    public static string MenuCompress = "압축";
    public static string MenuExtract = "압축 풀기";
    public static string MenuRename = "이름 바꾸기";
    public static string MenuDelete = "삭제";
    public static string MenuCopyPath = "경로 복사";
    public static string MenuMultiSelectionInfo = "{0}개 항목 선택됨";
    public static string MenuOpenTerminal = "터미널에서 열기";
    public static string MenuOpenWithCode = "Code로 열기";
    public static string MenuRevealInExplorer = "탐색기에서 위치 열기";
    public static string MenuRevealInTree = "트리에서 보기";
    public static string GestureDoubleClick = "더블클릭";
    public static string MenuProperties = "속성";
    public static string MenuRemoveFavorite = "즐겨찾기에서 제거";

    // Options ("...") menu
    public static string MenuAutoCollapse = "폴더 자동 접기";
    public static string MenuCollapseAllExpanded = "모든 펼친 폴더 접기";
    public static string CollapseAllConfirmTitle = "모든 펼친 폴더 접기";
    public static string CollapseAllConfirmBody = "모든 펼쳐졌던 폴더를 접겠습니까?";
    public static string MenuAlwaysOnTop = "항상 위에 표시";
    // The submenu holding the set-once housekeeping toggles (autostart, tray
    // icon, folder/file icons, title bar text) - they sat as top-level rows
    // until the menu grew long enough that daily rows and once-ever rows were
    // shoulder to shoulder (2026-08-08).
    public static string MenuGeneralSettings = "기본 설정";
    public static string MenuStartWithWindows = "부팅 후 자동 시작";
    public static string MenuAlwaysShowTrayIcon = "트레이 아이콘";
    public static string MenuShowFolderIcons = "폴더 아이콘";
    public static string MenuShowFileIcons = "파일 아이콘";
    public static string MenuHideTitleBarTitle = "제목 표시줄 타이틀 제거";
    // The SAME name the colour row uses (색상 설정's 영역 구분선), because they are
    // the same thing seen twice - one decides whether there is a line, the other
    // what colour it is. Two names for it would read as two features.
    public static string MenuShowPanelDividers = "영역 구분선";
    // MenuShowPathBar removed 2026-08-11 along with the toggle it labelled -
    // the strip is always on now that it carries the history chevrons.
    // Named after the PANEL, not favorites: it now holds either list. Lives
    // inside the 패널 표시 submenu, under the three modes.
    public static string MenuFavoritesAtBottom = "아래에 표시";
    public static string MenuDockOnRight = "고정 위치 오른쪽";
    public static string MenuAutoHideCloseOnLeave = "마우스 이탈 시 닫기";
    public static string MenuAutoHideUseHandle = "숨김 시 손잡이만";
    public static string MenuAutoHideSlide = "숨김 애니메이션";
    public static string MenuAutoHideSliverWidth = "숨김 시 막대 두께";
    public static string MenuColorSettings = "색상 설정";
    // On every swatch in the colour window. The right-click gesture has to be
    // stated somewhere the hand is already resting, or it may as well not exist.
    public static string ColorSwatchTooltip = "클릭: 색 선택기 · 우클릭: 색상 코드 입력";
    public static string ColorHexInputHint = "#RRGGBB · Enter 적용, Esc 취소";
    // The recovery lever for "something is off and I cannot say what": the
    // fault may not even be this app's (another program fighting over the
    // same edge/hooks), and a restart clears both kinds at once.
    public static string MenuRestart = "다시 시작";
    public static string MenuHelp = "도움말";
    public static string MenuAbout = "앱 정보";
    public static string HelpTitle = "Edgetree 도움말";

    // Fixed, deliberately not switched by Initialize() below - shown the same
    // in either language rather than "언어 / Language", since the word
    // "Language" alone is already understood regardless of which one a user
    // currently reads.
    public static readonly string MenuLanguage = "Language";

    // Fixed, deliberately not switched by Initialize() below - same reasoning
    // as MenuLanguage just above: whichever language a user currently reads,
    // they need to understand this note about the *other* one before they
    // click it, so it always shows both.
    public static readonly string LanguageRestartNote = "(재시작 필요 / Restart Required)";

    // Per-folder right-click menu's own sort submenu ("정렬 방식") reuses
    // MenuSort/MenuSortByName/etc. below - this is only the options-menu
    // ("...") copy, worded as "default" to distinguish "change how every
    // folder sorts from now on" from that per-folder one.
    // "아이콘 종류" submenu: the bundled PNG set (기본) vs. the icons Windows
    // Explorer itself shows (see ShellIconService).
    public static string MenuIconStyle = "아이콘 종류";
    public static string MenuIconStyleDefault = "기본";
    public static string MenuIconStyleShell = "Windows 탐색기";

    public static string MenuDefaultSort = "정렬 기본값";
    public static string MenuSort = "정렬 기준";
    public static string MenuSortByName = "이름";
    public static string MenuSortByDate = "수정한 날짜";
    public static string MenuSortByType = "유형";
    public static string MenuSortBySize = "크기";
    public static string MenuSortAscending = "오름차순";
    public static string MenuSortDescending = "내림차순";

    // Clears a folder's own remembered sort override (see
    // Models.FolderSortOverrideEntry) so it goes back to following
    // MenuDefaultSort - shown in the per-folder "정렬" submenu only when that
    // folder actually has one (see MainWindow.ExplorerItemContextMenu_Opened).
    public static string MenuFollowDefaultSort = "전역 정렬 따르기";

    // Tree folder right-click -> jumps to the search view with this folder
    // already set as the scope (see MainWindow.SearchInFolder_Click).
    public static string MenuSearchInFolder = "이 폴더에서 검색";

    // Both sort icons - a folder row's own override icon (MainWindow.xaml's
    // SortOverrideIconBorder) and the search view's sort button - rotate
    // through their states on click, with nothing but a small image to say
    // which one is active. Their ToolTip names the current state outright,
    // since field (color) and direction (which triangle is filled) are easy to
    // misread at that size. {0} is one of the SortMode* labels below; built by
    // FileSystemService.FormatSortTooltip/NoSortOverrideTooltip.
    public static string SortTooltipFormat = "정렬 기준: {0}";
    public static string SortModeFollowGlobal = "전역 설정 따름";
    public static string SortModeFolderGroup = "폴더별 묶기";
    public static string SortModeNameAsc = "이름 오름차순";
    public static string SortModeNameDesc = "이름 내림차순";
    public static string SortModeDateAsc = "날짜 오름차순";
    public static string SortModeDateDesc = "날짜 내림차순";
    // Carries its own shortcut in the label - this setting existed as Ctrl +/-
    // only, and a user who needed it didn't find it (see the XAML comment on
    // the row itself).
    public static string MenuFontSize = "글꼴 크기 (Ctrl +/-)";
    public static string MenuMaxItemsPerFolder = "한 번에 표시할 개수";
    public static string MenuTabSpacing = "들여쓰기 간격";
    public static string MenuRowSpacing = "행 간격";
    public static string MenuScrollBarThickness = "스크롤바 두께";
    public static string MenuExportSettings = "설정 내보내기...";
    public static string MenuImportSettings = "설정 가져오기...";
    public static string MenuResetSettings = "전체 설정 초기화...";

    // Header buttons (ToolTips) and root label

    // Which one shows depends on AppSettings.DockOnRight (see
    // MainWindow.xaml.cs's UpdatePinButtonVisibility) - PinButton's ToolTip
    // is set from code, not bound to a single static string like the others.
    public static string ToolTipPinLeft = "좌측에 고정";
    public static string ToolTipPinRight = "우측에 고정";
    public static string ToolTipPinAutoHide = "자동 숨김";
    public static string ToolTipPinStayOpen = "고정";
    public static string ToolTipCollapseAll = "모두 접기";
    public static string ToolTipRestoreExpanded = "펼침 상태 복원";
    public static string ToolTipOptions = "옵션";
    public static string ToolTipUpdateAvailable = "새 버전 {0} 다운로드 가능";
    public static string ToolTipMinimize = "트레이로 최소화";
    // The HEADER button, which is not the same action as the menu rows above:
    // those go to the tray, this puts the sidebar away in whichever way the
    // current mode offers (tray, auto-hide, or minimise). It wore the menu's
    // words until 2026-08-11 and so promised the tray in two modes out of three.
    public static string ToolTipPutAway = "사이드바 치우기";
    public static string ToolTipClose = "종료";
    public static string RootPathLabel = "내 PC";

    // Synthetic "show the rest" row appended under a folder capped at
    // FileSystemItem.DisplayCap items. {0} is the hidden count.
    public static string MenuThumbnailMaxSize = "썸네일 최대 크기";

    public static string ShowMoreFormat = "… 더 보기 ({0}개)";

    // The same trailing slot once the reveal has happened: the two states are
    // exclusive, so one row serves both and the folder always ends in a way
    // back out. {0} is the count that would be hidden again.
    public static string ShowLessFormat = "… 접기 ({0}개)";

    // File search (Ctrl+F view - see MainWindow's search-view methods and
    // Services/FileSearchService)
    public static string ToolTipSearch = "검색 (Ctrl+F)";
    public static string ToolTipViewer = "이미지 뷰어";
    // Viewer zoom strip - two chips wide, so both stay short in every language.
    public static string ViewerZoomFit = "맞춤";
    public static string ViewerZoomActual = "1:1";
    public static string ViewerNavigator = "내비게이터";
    public static string ViewerClose = "뷰어 닫기";
    // The footer's now-playing row, which only exists while the viewer is shut
    // and 배경 재생 is holding a track.
    public static string FooterNowPlayingOpen = "뷰어에서 열기";
    // Explorer's own wording for the same action, so it reads as the familiar
    // thing rather than a new feature.
    public static string MenuSetWallpaper = "바탕 화면 배경으로 설정";
    // The path bar's two chevrons. Plain 뒤로/앞으로 rather than "이전 위치":
    // this is the gesture every browser and file manager has, and naming it
    // anything else would make it read as something new.
    // The key rides in the tooltip because there is nowhere else it could be
    // said - these two are buttons, not menu rows with an InputGestureText
    // column, and a shortcut nobody can discover is one nobody uses.
    public static string TreeHistoryBack = "뒤로  (Ctrl+←)";
    public static string TreeHistoryForward = "앞으로  (Ctrl+→)";
    public static string ViewerPrevImage = "이전 이미지";
    public static string ViewerNextImage = "다음 이미지";
    // 썸네일 바, not 필름스트립: the loanword is barely used in Korean, and the
    // help had already been glossing it ("필름스트립(썸네일 바)") - which is a
    // label admitting it needs a translation. The English keeps its own name
    // rather than being translated back.
    public static string ViewerFilmstrip = "썸네일 바";
    public static string MenuImageViewer = "이미지 뷰어";
    // 캐싱 rather than 미리 불러오기, reversing the earlier choice to name it for
    // what it does instead of for the machinery: 캐싱 is the word Korean users
    // read fastest here, and "미리 불러오기" describes the act without naming the
    // thing that is left behind - which is what the row below cleans up. The two
    // rows now share a noun, so the pair reads as one subject.
    public static string MenuPrecacheThumbnails = "이미지 썸네일 캐싱";
    // The size rides in the row itself, because the only reason to press it is
    // that the number has grown - and a row that has to be pressed to find out
    // is a row that gets pressed for no reason.
    public static string MenuClearThumbnailCache = "캐싱 파일 정리";
    public static string MenuClearThumbnailCacheSized = "캐싱 파일 정리 ({0})";
    // {0} is how many are in hand so far. No total: the bar's own counter is
    // right beside it and already says how many the folder holds.
    public static string ViewerPrecaching = "캐싱 중 {0}";
    // 북마크, not 위치 기록 (2026-08-11). "기록" reads as logging something
    // rather than as leaving a marker you come back to, and the app already
    // has a word for exactly that act on a tree row. Qualified with 영상 in the
    // help so the two are told apart there; inside a video's own menu the
    // qualifier is what the menu already is.
    // QUALIFIED SINCE 2026-08-11, and the reason the old note gave for leaving
    // it bare has expired. It read "inside a video's own menu the qualifier is
    // what the menu already is" - true while this menu held only the film's own
    // rows, and false the moment the FILE's 북마크 joined it. Two rows both
    // called 북마크, one marking a second of film and one marking a row in the
    // tree, is the kind of thing nobody reads twice. The help already used this
    // longer name for it.
    public static string ViewerMarkAdd = "영상 북마크";
    public static string ViewerMarkList = "영상 북마크 목록";
    public static string ViewerRewind = "처음으로";
    // Two pairs because the transport serves sound and film from the same row,
    // and the tooltip is set to whichever the loaded file is. 곡 over a folder
    // of films would be naming the wrong thing, and "이전 항목" would be naming
    // nothing in particular.
    public static string ViewerPrevTrack = "이전 곡";
    public static string ViewerNextTrack = "다음 곡";
    public static string ViewerPrevVideo = "이전 영상";
    public static string ViewerNextVideo = "다음 영상";
    // "영상 크기" rather than "확대": the row's middle button is 맞춤, which is
    // not a magnification - the row is about what SIZE the film is shown at,
    // and one of the three answers is "as big as it fits" (2026-08-10).
    public static string ViewerZoom = "영상 크기";
    public static string ViewerSubtitles = "자막";
    public static string ViewerSubtitleSize = "자막 크기";
    public static string ViewerSubtitleSync = "자막 싱크";
    // Safe to be this blunt because of WHERE it is: inside the 북마크 목록
    // submenu, under the positions it clears. The word 삭제 means files
    // everywhere else in this app, and it would have needed qualifying if it
    // sat anywhere the file rows could be mistaken for its subject.
    public static string ViewerMarkClear = "전체 삭제";
    // The gap between pressing play and the engine reporting the file open -
    // seconds on a sleeping share, and silent until now. Said in the caption,
    // which is where this panel already talks.
    // Named for the file, not for the maths: nobody looking at a washed-out
    // film is thinking "tone mapping", they are thinking "this is an HDR one".
    public static string ViewerHdrToneMap = "HDR 색 보정";
    // 밝기 rather than 노출: the row is a dial someone turns while looking at
    // the picture, and 노출 names the maths behind it instead of the change.
    public static string ViewerHdrBrightness = "  밝기";
    public static string ViewerHdrSaturation = "  채도";
    public static string ViewerHdrContrast = "  대비";
    public static string ViewerMediaOpening = "여는 중…";
    // Nothing has failed and nothing is being cancelled; the wait is just
    // longer than a wait usually is, and saying so beats a still picture.
    public static string ViewerMediaOpeningSlow = "여는 중… 조금 오래 걸리고 있습니다";
    // Playing, but the picture has stopped moving - the file is not arriving
    // fast enough to decode. Named for the cause rather than for the symptom.
    public static string ViewerMediaStalled = "파일을 기다리는 중…";
    public static string ViewerPlay = "재생";
    public static string ViewerPause = "일시정지";
    public static string ViewerStop = "정지";
    public static string ViewerMute = "음소거";
    // The four states of the transport's leftmost switch, said plainly and in
    // the words a player uses - these are the tooltip AND the menu, so a name
    // that needed explaining would need it twice (2026-08-11).
    public static string ViewerRepeatOff = "이어서 재생 안 함";
    public static string ViewerRepeatAll = "폴더 반복";
    public static string ViewerRepeatOne = "한 곡 반복";
    public static string ViewerRepeatShuffle = "셔플 재생";
    // Appended to whichever of the four is in force. The switch turns the thing
    // on and off, and nothing about it said where the other three modes live -
    // which was the first thing asked about it (2026-08-11).
    public static string ViewerRepeatHint = "우클릭으로 변경";
    // The switch that lets sound outlive the selection. Named for what it buys
    // rather than for the mechanism: "백그라운드 재생" is what other apps call
    // it, and it says the sound keeps going while you are doing something else.
    public static string ViewerBackgroundPlay = "백그라운드 재생 · 다른 폴더로 가도 계속";
    // A selected folder's headline, in the caption slot a file's pixel size
    // uses. Counts what the PANEL can show, which is not the same number the
    // tree is listing beside it - so the sentence says which question it is
    // answering.
    public static string ViewerFolderItemCount = "볼 수 있는 파일 {0}개";
    // The folder's own play button. "전체" is the load-bearing word: pressing it
    // does not play one file, it puts the folder on and keeps it on.
    public static string ViewerFolderPlayAll = "폴더 전체 재생";
    // Shown in the caption when Windows has no codec for the file - the panel
    // says what happened and what still works, rather than sitting blank.
    public static string ViewerPlaybackUnsupported = "이 형식은 재생할 수 없습니다 · Enter로 기본 프로그램에서 열기";
    // Said only when playback had ALREADY started - "형식을 재생할 수 없다"는 말은
    // 20초를 잘 재생한 뒤에 나오면 거짓이고, 있지도 않은 코덱을 찾으러 가게 만듦.
    public static string ViewerPlaybackInterrupted = "재생이 중단되었습니다 · 다시 누르면 이어서, Enter로 기본 프로그램에서 열기";
    // Some files play their picture and nothing else - DTS and TrueHD sound
    // are both undecodable on Windows, and the engine simply drops the track.
    // Silence with no explanation reads as the app being broken, so the
    // caption says which it is. Stated, not apologised for.
    public static string ViewerNoAudio = "소리 없음";
    // The transport strip's last button. Named for where it sends the file,
    // not for the act of leaving here.
    public static string ViewerOpenExternally = "기본 프로그램에서 열기";
    public static string ToolTipExitSearch = "탐색기로 (Ctrl+E)";
    public static string SearchTooltipBrowseFolder = "검색할 폴더 선택";
    public static string SearchTooltipRefresh = "다시 인덱싱";
    public static string SearchTooltipRefreshStale = "다시 인덱싱 · 이 폴더가 바뀌었습니다";
    public static string SearchTooltipHistory = "최근 검색어";
    public static string SearchHistoryDeleteTooltip = "이 검색어 삭제";
    public static string SearchBrowseFolderDialogTitle = "검색할 폴더를 선택하세요";
    public static string SearchScopeNone = "검색할 폴더를 선택하세요 →";
    public static string SearchBoxPlaceholder = "검색";
    // Only ever seen with nothing selected (startup before the last selection
    // is restored) - the box is otherwise full of the current folder's path.
    // That one moment is where "you can type in here" has to be said, since
    // the menu label deliberately doesn't say it.
    public static string PathBarPlaceholder = "경로 입력 후 Enter";
    // {0} = files scanned so far. The "검색 가능" half is the whole point:
    // results stream in while the scan runs, but nothing said so - and the
    // pulsing bar that used to sit under this line actively implied the
    // opposite (see the note where it was removed in MainWindow.xaml).
    public static string SearchStatusScanning = "인덱싱 중… ({0}) · 검색 가능";
    // {0} = result count.
    public static string SearchStatusResults = "{0}개 결과";
    // {0} = shown, {1} = total (results were capped for display).
    public static string SearchStatusResultsCapped = "{0} / {1}개 결과 표시";
    public static string SearchStatusNoResults = "결과 없음";
    public static string SearchStatusNoResultsCached = "결과 없음 · 인덱스 {0} · 새로고침으로 갱신";
    public static string SearchStatusIndexAgeSuffix = " · 인덱스 {0}";
    // These three all have to survive a docked sidebar's width, which is far
    // narrower than a normal dialog - full sentences get clipped mid-word
    // there. Written as terse fragments on purpose; the search box's own
    // placeholder already supplies the "type something" instruction, so the
    // idle line spends its width on the matching rules instead, which are the
    // part nobody can guess.
    // Shown instead of a fresh scan's line when the index came off disk. The age
    // is the important half - a file created since that moment simply won't
    // appear, and without the age there's nothing to explain the absence (see
    // SearchIndexCache's note). {0} = a SearchAge* string below.
    public static string SearchStatusCached = "인덱스 {0} · 새로고침으로 갱신";
    public static string SearchAgeJustNow = "방금";
    // {0} = whole minutes / hours / days since the scan.
    public static string SearchAgeMinutes = "{0}분 전";
    public static string SearchAgeHours = "{0}시간 전";
    public static string SearchAgeDays = "{0}일 전";

    // A result whose file is gone - only reachable once an index can outlive
    // the files it names (see SearchIndexCache).
    public static string SearchResultMissing = "삭제되었거나 이동된 파일입니다";

    public static string SearchStatusTooBroad = "너무 광범위함 · 글자나 숫자 포함";
    public static string SearchStatusEmpty = "부분일치 · *? 와일드카드 · ↑↓ 기록";
    public static string SearchStatusScopeMissing = "폴더 없음 · 새로고침하거나 다시 선택";

    // Color settings window
    public static string ColorSettingsTitle = "색상 설정";
    public static string ColorLabelBackground = "탐색기 배경";
    // "폴더 이름"/"파일 이름", not "폴더명"/"파일명": the row menu already says
    // "이름 바꾸기", and one thing called two names reads as two things.
    public static string ColorLabelFolderNameFont = "폴더 이름";
    public static string ColorLabelFolderNameHighlightFont = "폴더 이름 강조";
    public static string ColorLabelFileNameFont = "파일 이름";
    public static string ColorLabelFileNameHighlightFont = "파일 이름 강조";
    public static string ColorLabelSelection = "선택된 항목";
    // Named after the PANEL, not favorites - the same correction 아래에 표시
    // got in the options menu, and for the same reason: this one brush paints
    // the favorites list, the bookmark list AND the search view's scope strip
    // (which borrows the panel's colour on purpose). The stored JSON key stays
    // HistoryBackgroundColorHex so nobody's customised colour resets.
    public static string ColorLabelHistory = "패널 배경";
    public static string ColorLabelHoverBackground = "마우스 오버";
    public static string ColorLabelFolderNameHoverFont = "폴더 이름 마우스 오버";
    public static string ColorLabelFileNameHoverFont = "파일 이름 마우스 오버";
    public static string ColorLabelShowMore = "더 보기";
    public static string ColorLabelGuideLine = "들여쓰기 안내선";
    public static string ColorLabelGuideLineActive = "들여쓰기 안내선 강조";
    public static string ColorLabelHeader = "제목 표시줄 배경";
    public static string ColorLabelPanelDivider = "영역 구분선";
    public static string ColorLabelViewerBackground = "뷰어 배경";
    // Names both shapes the hidden sidebar can take, because one colour covers
    // both and the option menu already calls them 손잡이 and 막대
    // (MenuAutoHideUseHandle / MenuAutoHideSliverWidth).
    public static string ColorLabelAutoHideHandle = "숨김 시 손잡이/막대";
    public static string ButtonDefaults = "기본값";
    public static string ButtonClose = "닫기";
    // Colours travel between machines; the rest of settings.json does not -
    // hidden folders, bookmarks and last-selected paths all name folders the
    // other PC may not have (2026-08-04).
    public static string ButtonExportColors = "내보내기";
    public static string ButtonImportColors = "불러오기";
    public static string ColorFileFilter = "Edgetree 색상 (*.json)|*.json";
    public static string ColorFileDefaultName = "edgetree-colors.json";
    public static string ColorImportFailedTitle = "색상 불러오기";
    public static string ColorImportFailedBody = "이 파일에는 색상이 없습니다.";
    // The moon and sun emoji that used to lead these are gone (2026-08-11):
    // they were the only pictures in a window of text buttons, they came from
    // the system's emoji font rather than from the app's own marks, and the
    // words already say which is which.
    public static string ColorThemeDarkMode = "다크 모드";
    public static string ColorThemeLightMode = "라이트 모드";
    public static string ButtonRandomColors = "랜덤";
    // Said as what it GIVES, so the pair reads as a choice rather than as one
    // button and its louder twin: this one lands on combinations that go
    // together, the other one on combinations that do not have to.
    public static string ButtonRandomColorsTip = "자연스러운 조합";
    // The second die, beside the first. "원색" says what makes it different in
    // two characters - it starts from a primary hue - where "과감" or "강렬"
    // would be describing a feeling and could mean anything.
    public static string ButtonDaringColors = "원색";
    public static string ButtonDaringColorsTip = "원색에서 출발하는 과감한 조합";
    // "되돌리기" over the more precise "랜덤 전으로", which read oddly
    // (2026-08-09); the button lives beside 랜덤, which carries the
    // context the label dropped.
    public static string ButtonUndoRandom = "되돌리기";
    // Plain (no emoji) versions for use inside a sentence - see
    // ColorResetConfirmBody.
    public static string ColorThemeDarkLabel = "다크 모드";
    public static string ColorThemeLightLabel = "라이트 모드";
    public static string ColorResetConfirmTitle = "색상 초기화";
    public static string ColorResetConfirmBody = "현재 {0}에서 설정한 색상값이 초기화됩니다. 진행하시겠습니까?";

    // About window
    public static string AboutTitle = "정보";
    public static string AboutVersionLabel = "버전";
    public static string AboutAuthorLabel = "제작자";
    public static string AboutDateLabel = "날짜";
    public static string AboutLicenseLabel = "라이선스 요약";
    public static string AboutGithubLabel = "GitHub";
    public static string AboutWebsiteLabel = "웹사이트";
    public static string AboutOtherToolLabel = "같은 개발자의 다른 도구";
    public static string AboutUpdateAvailableFormat = "새 버전 {0} 다운로드";
    public static string AboutAuthorValue = "pjh85336@gmail.com";
    public static string AboutLicenseSummary =
        "MIT 라이선스. 별도의 보증 없이 있는 그대로 제공되며, 사용에 따른 책임은 사용자 본인에게 있습니다.";
    public static string AboutIconLicenseLabel = "아이콘";
    public static string AboutIconLicenseValue =
        "파일·폴더 아이콘: Material Icon Theme (MIT)\n" +
        "화면 글리프: Material Symbols, Google (Apache License 2.0)";
    public static string AboutIconLicenseOpen = "Apache License 2.0 전문 보기";

    // Tray
    public static string TrayOpen = "열기";
    public static string TrayHide = "트레이로";
    public static string TrayAbout = "정보";
    public static string TrayExit = "종료";
    // Only present in the tray menu when there is actually a newer release -
    // no row at all otherwise, and never a balloon.
    public static string TrayUpdateAvailable = "새 업데이트 - v{0}";

    // MessageBox titles/bodies
    public static string PasteFailedTitle = "붙여넣기 실패";
    public static string MoveIntoSelfError = "폴더를 자기 자신이나 그 하위 폴더로 옮길 수 없습니다.";
    public static string CopyIntoSelfError = "폴더를 자기 자신이나 그 하위 폴더로 복사할 수 없습니다.";
    public static string NewFolderFailedTitle = "새 폴더 만들기 실패";
    public static string RenameFailedTitle = "이름 바꾸기 실패";
    // The name a newly created folder gets, and the message when a rename is
    // refused. Both used to be Korean literals sitting in FileOperationService,
    // so an English install still wrote a folder called "새 폴더" onto the
    // user's disk - reported from a German freeware forum, 2026-08-05: "bei
    // einem kurzen Test tauchte trotz englisch noch koreanischer Text auf".
    // Anything a user can SEE belongs here, including text that ends up as a
    // filename.
    public static string NewFolderDefaultName = "새 폴더";
    public static string RenameFailedBody = "이름을 바꿀 수 없습니다.";
    public static string DeleteConfirmTitle = "삭제 확인";
    public static string DeleteConfirmBody = "'{0}'을(를) 휴지통으로 보낼까요?";
    public static string DeleteConfirmBodyMultiple = "선택한 {0}개 항목을 휴지통으로 보낼까요?";
    public static string DeleteFailedTitle = "삭제 실패";
    public static string CompressFailedTitle = "압축 실패";
    public static string ExtractFailedTitle = "압축 풀기 실패";
    public static string CompressSkippedBody = "{0}개 항목을 읽을 수 없어 건너뛰었습니다.";
    public static string StartWithWindowsFailedTitle = "윈도우 시작 시 실행";
    public static string StartWithWindowsFailedBody = "시작 프로그램 등록에 실패했습니다. 관리자 정책으로 제한되어 있을 수 있습니다.";
    public static string LanguageChangeTitle = "언어 변경";
    public static string LanguageChangeBody = "언어를 변경하려면 앱을 다시 시작해야 합니다. 지금 다시 시작할까요?";
    public static string ImportFailedTitle = "가져오기 실패";
    public static string OverwriteConfirmTitle = "덮어쓰기 확인";
    public static string OverwriteConfirmBody = "'{0}'이(가) 이미 있습니다. 덮어쓸까요?";

    public static string ExportSettingsFailedTitle = "설정 내보내기 실패";
    public static string ImportSettingsFailedTitle = "설정 가져오기 실패";
    public static string SettingsImportedTitle = "설정 가져오기 완료";
    public static string SettingsImportedBody = "설정을 가져왔습니다. 적용하려면 앱을 다시 시작해야 합니다. 지금 다시 시작할까요?";

    public static string ResetSettingsConfirmTitle = "설정 초기화";
    public static string ResetSettingsConfirmBody = "모든 설정과 즐겨찾기가 앱 기본 상태로 초기화됩니다. 이 작업은 되돌릴 수 없습니다.\n\n초기화 후 적용을 위해 앱을 다시 시작합니다. 계속할까요?";

    // Which language the fields below ended up in. Almost nothing needs to ask
    // - the point of this class is that callers just read a string - but a
    // BILINGUAL subtitle file has to be told which of its two tracks to play,
    // and the app's own language is the only answer that isn't a guess (see
    // SubtitleService).
    public static bool IsEnglish { get; private set; }

    public static void Initialize(string language)
    {
        if (language != "en")
        {
#if INSTRUMENT
            // A measuring build (-p:EdgetreeInstrument=true) is Release code with the
            // DEBUG instruments compiled back in, so it would otherwise call
            // itself DEBUG and its numbers would be filed under the wrong
            // build. See the Instrument property in Edgetree.csproj.
            RootPathLabel += " (계측)";
#elif DEBUG
            // A quick, unmistakable way to tell a freshly-built Debug run
            // apart from any already-running instance the single-instance
            // mutex (see App.OnStartup) might otherwise silently defer to -
            // e.g. an old Release/tray instance from before this launch that
            // makes it look like a rebuild "didn't take effect" when it's
            // actually just not the window on screen. Compiled out entirely
            // in Release (RootPathLabel never gets this suffix there).
            RootPathLabel += " (DEBUG)";
#endif
            return;
        }

        IsEnglish = true;
        MenuAddFavorite = "Add to Favorites";
        MenuBookmark = "Bookmark";
        MenuBookmarkAdd = "Add Bookmark";
        MenuBookmarkRemove = "Remove Bookmark";
        MenuBookmarkList = "Bookmarks";
        MenuBookmarkListEmpty = "No bookmarks";
        MenuPresetAdd = "Add preset";
        MenuPresetApply = "Apply";
        MenuPresetOverwrite = "Overwrite…";
        MenuPresetRename = "Rename…";
        PresetSaveTitle = "Save current setup";
        PresetRenameTitle = "Rename preset";
        MenuPresetDelete = "Delete";
        PresetNameTitle = "Preset name";
        PresetNameHint = "Saves position, size, docking, colours, file types and the current folder";
        PresetDefaultName = "Preset {0}";

        MenuFileFilter = "Show File Types";
        FilterChipExecutable = "Programs";
        MenuFileFilterAll = "All";
        MenuFileFilterCode = "Code";
        MenuFileFilterImage = "Images";
        MenuFileFilterDocument = "Documents";
        MenuFileFilterMedia = "Media";
        MenuFileFilterArchive = "Archives";
        MenuFileFilterExecutable = "Programs & Shortcuts";
        MenuFileFilterOther = "Other";
        MenuFileFilterCustomEdit = "Custom…";
        FilterCustomTitle = "Custom Extensions";
        FilterCustomHint = "Separate with commas · psd, ai, .fig";
        FilterCustomEmptyHint = "Leaving this empty removes the custom kind";
        MenuFileFilterExcludeEdit = "Exclude…";
        FilterExcludeTitle = "Excluded Extensions";
        FilterExcludeHint = "Separate with commas · these are always hidden";
        FilterExcludeEmptyHint = "Leaving this empty removes the exclusion";
        ButtonOk = "OK";
        ButtonCancel = "Cancel";
        MenuFontWeight = "Font Weight";
        MenuFontWeightNormal = "Normal";
        MenuFontWeightBold = "Bold";
        MenuFontWeightFoldersOnly = "Bold Folders Only";
        MenuFontWeightFilesOnly = "Bold Files Only";
        MenuSidePanel = "Side Panel";
        MenuSidePanelFavorites = "Favorites";
        MenuSidePanelBookmarks = "Bookmarks";
        MenuSidePanelNone = "Hidden";
        MenuHideFolder = "Hide This Folder";
        MenuHideSelectedFolders = "Hide Selected Folders";
        MenuHiddenFolderList = "Hidden Folders";
        MenuHiddenFolderListEmpty = "No hidden folders";
        MenuUnhideFolder = "Unhide";
        MenuListRowRemove = "Remove";
        HiddenClearAllConfirmTitle = "Unhide All Folders";
        HiddenClearAllConfirmBody = "Show all {0} hidden folders again?";
        MenuBookmarkClearAll = "Clear all";
        BookmarkClearAllConfirmTitle = "Clear All Bookmarks";
        BookmarkClearAllConfirmBody = "Clear all {0} bookmarks?";
        FavoriteClearAllConfirmTitle = "Clear All Favorites";
        FavoriteClearAllConfirmBody = "Clear all {0} favorites?";
        BookmarkShortcutNext = "Next bookmark";
        BookmarkShortcutPrev = "Previous bookmark";
        MenuNewFolder = "New Folder";
        MenuRefresh = "Refresh";
        MenuAutoCollapse = "Accordion Mode";
        MenuCollapseAllExpanded = "Collapse All Expanded Folders";
        CollapseAllConfirmTitle = "Collapse All Expanded Folders";
        CollapseAllConfirmBody = "Collapse every folder that is currently expanded?";
        MenuOpen = "Open in default app";
        MenuViewHere = "View";
        MenuPlayHere = "Play";
        MenuOpenWith = "Open With";
        MenuCut = "Cut";
        MenuCopy = "Copy";
        MenuPaste = "Paste";
        MenuCompress = "Compress";
        MenuExtract = "Extract";
        MenuRename = "Rename";
        MenuDelete = "Delete";
        MenuCopyPath = "Copy Path";
        MenuMultiSelectionInfo = "{0} items selected";
        MenuOpenTerminal = "Open in Terminal";
        MenuOpenWithCode = "Open with Code";
        MenuRevealInExplorer = "Reveal in Explorer";
        MenuRevealInTree = "Show in Tree";
        GestureDoubleClick = "Double-click";
        MenuProperties = "Properties";
        MenuRemoveFavorite = "Remove from Favorites";

        MenuAlwaysOnTop = "Always on Top";
        MenuGeneralSettings = "General";
        MenuStartWithWindows = "Start with Windows";
        MenuAlwaysShowTrayIcon = "Always Show Tray Icon";
        MenuShowFolderIcons = "Show Folder Icons";
        MenuShowFileIcons = "Show File Icons";
        MenuHideTitleBarTitle = "Hide Title Bar Text";
        MenuShowPanelDividers = "Panel Dividers";
        MenuFavoritesAtBottom = "Show at Bottom";
        MenuDockOnRight = "Pin to Right Edge";
        MenuAutoHideCloseOnLeave = "Close on Mouse Leave";
        MenuAutoHideUseHandle = "Handle Instead of Full Edge";
        MenuAutoHideSlide = "Slide Animation";
        MenuAutoHideSliverWidth = "Auto-Hide Thickness";
        MenuColorSettings = "Color Settings";
        ColorSwatchTooltip = "Click: color picker · Right-click: enter a color code";
        ColorHexInputHint = "#RRGGBB · Enter to apply, Esc to cancel";
        MenuRestart = "Restart";
        MenuHelp = "Help";
        MenuAbout = "About";
        HelpTitle = "Edgetree help";
        MenuIconStyle = "Icon Style";
        MenuIconStyleDefault = "Default";
        MenuIconStyleShell = "Windows Explorer";

        MenuDefaultSort = "Default Sort";
        MenuSort = "Sort by";
        MenuSortByName = "Name";
        MenuSortByDate = "Date modified";
        MenuSortByType = "Type";
        MenuSortBySize = "Size";
        MenuSortAscending = "Ascending";
        MenuSortDescending = "Descending";
        MenuFollowDefaultSort = "Follow Default Sort";
        MenuSearchInFolder = "Search in This Folder";
        SortTooltipFormat = "Sorted by {0}";
        SortModeFollowGlobal = "Follow default";
        SortModeFolderGroup = "Group by folder";
        SortModeNameAsc = "Name ascending";
        SortModeNameDesc = "Name descending";
        SortModeDateAsc = "Date ascending";
        SortModeDateDesc = "Date descending";
        MenuFontSize = "Font Size (Ctrl +/-)";
        MenuMaxItemsPerFolder = "Items per Folder";
        MenuTabSpacing = "Indent Width";
        MenuRowSpacing = "Row Spacing";
        MenuScrollBarThickness = "Scrollbar Width";
        MenuExportSettings = "Export Settings...";
        MenuImportSettings = "Import Settings...";
        MenuResetSettings = "Reset All Settings...";

        ToolTipPinLeft = "Pin to Left";
        ToolTipPinRight = "Pin to Right";
        ToolTipPinAutoHide = "Auto Hide";
        ToolTipPinStayOpen = "Pin Open";
        ToolTipCollapseAll = "Collapse All";
        ToolTipRestoreExpanded = "Restore Expanded";
        ToolTipOptions = "Options";
        ToolTipUpdateAvailable = "Version {0} available for download";
        ToolTipMinimize = "Minimize to Tray";
        ToolTipPutAway = "Put the sidebar away";
        ToolTipClose = "Exit";
        RootPathLabel = "This PC";
        MenuThumbnailMaxSize = "Max thumbnail size";
        ShowMoreFormat = "… Show {0} more";
        ShowLessFormat = "… Show {0} less";

        ToolTipSearch = "Search (Ctrl+F)";
        ToolTipViewer = "Image Viewer";
        ViewerZoomFit = "Fit";
        ViewerZoomActual = "1:1";
        ViewerNavigator = "Navigator";
        ViewerClose = "Close viewer";
        FooterNowPlayingOpen = "Open in the viewer";
        MenuSetWallpaper = "Set as desktop background";
        TreeHistoryBack = "Back  (Ctrl+←)";
        TreeHistoryForward = "Forward  (Ctrl+→)";
        ViewerPrevImage = "Previous image";
        ViewerNextImage = "Next image";
        ViewerFilmstrip = "Thumbnail bar";
        MenuImageViewer = "Image viewer";
        MenuPrecacheThumbnails = "Preload image thumbnails";
        MenuClearThumbnailCache = "Clean up thumbnail files";
        MenuClearThumbnailCacheSized = "Clean up thumbnail files ({0})";
        ViewerPrecaching = "Preloading {0}";
        ViewerMarkAdd = "Video Bookmark";
        ViewerMarkList = "Video Bookmarks";
        ViewerRewind = "Back to start";
        ViewerPrevTrack = "Previous track";
        ViewerNextTrack = "Next track";
        ViewerPrevVideo = "Previous video";
        ViewerNextVideo = "Next video";
        ViewerZoom = "Video size";
        ViewerSubtitles = "Subtitles";
        ViewerSubtitleSize = "Subtitle size";
        ViewerSubtitleSync = "Subtitle sync";
        ViewerMarkClear = "Clear all";
        ViewerHdrToneMap = "HDR colour correction";
        ViewerHdrBrightness = "  Brightness";
        ViewerHdrSaturation = "  Saturation";
        ViewerHdrContrast = "  Contrast";
        ViewerMediaOpening = "Opening…";
        ViewerMediaOpeningSlow = "Opening… this is taking a while";
        ViewerMediaStalled = "Waiting for the file…";
        ViewerPlay = "Play";
        ViewerPause = "Pause";
        ViewerStop = "Stop";
        ViewerMute = "Mute";
        ViewerRepeatOff = "Don't continue";
        ViewerRepeatAll = "Repeat folder";
        ViewerRepeatOne = "Repeat one";
        ViewerRepeatShuffle = "Shuffle";
        ViewerRepeatHint = "right-click to change";
        ViewerBackgroundPlay = "Background play · keeps going in other folders";
        ViewerFolderItemCount = "{0} files this panel can show";
        ViewerFolderPlayAll = "Play this folder";
        ViewerPlaybackUnsupported = "This format can't be played here · Enter opens it in the default app";
        ViewerPlaybackInterrupted = "Playback stopped · press play to resume, Enter opens it in the default app";
        ViewerNoAudio = "No sound";
        ViewerOpenExternally = "Open in default app";
        ToolTipExitSearch = "Back to Explorer (Ctrl+E)";
        SearchTooltipBrowseFolder = "Choose folder to search";
        SearchTooltipRefresh = "Reindex";
        SearchTooltipRefreshStale = "Reindex · this folder has changed";
        SearchTooltipHistory = "Recent searches";
        SearchHistoryDeleteTooltip = "Remove this search";
        SearchBrowseFolderDialogTitle = "Choose a folder to search";
        SearchScopeNone = "Choose a folder to search →";
        SearchBoxPlaceholder = "Search";
        PathBarPlaceholder = "Type a path, press Enter";
        SearchStatusScanning = "Indexing… ({0}) · you can search now";
        SearchStatusResults = "{0} results";
        SearchStatusResultsCapped = "Showing {0} of {1} results";
        SearchStatusNoResults = "No results";
        SearchStatusNoResultsCached = "No results · index from {0} · refresh to update";
        SearchStatusIndexAgeSuffix = " · index from {0}";
        SearchStatusCached = "Index from {0} · refresh to update";
        SearchAgeJustNow = "just now";
        SearchAgeMinutes = "{0} min ago";
        SearchAgeHours = "{0}h ago";
        SearchAgeDays = "{0}d ago";

        SearchResultMissing = "That file was deleted or moved";

        SearchStatusTooBroad = "Too broad · add a letter or digit";
        SearchStatusEmpty = "Substring · * ? wildcards · ↑↓ history";
        SearchStatusScopeMissing = "Folder missing · refresh or re-pick";

        ColorSettingsTitle = "Color Settings";
        ColorLabelBackground = "Explorer Background";
        ColorLabelFolderNameFont = "Folder Name";
        ColorLabelFolderNameHighlightFont = "Folder Name Highlight";
        ColorLabelFileNameFont = "File Name";
        ColorLabelFileNameHighlightFont = "File Name Highlight";
        ColorLabelSelection = "Selected Item";
        ColorLabelHistory = "Panel Background";
        ColorLabelHoverBackground = "Mouse Hover";
        ColorLabelFolderNameHoverFont = "Folder Name Mouse Hover";
        ColorLabelFileNameHoverFont = "File Name Mouse Hover";
        ColorLabelShowMore = "Show More";
        ColorLabelGuideLine = "Guide Line";
        ColorLabelGuideLineActive = "Guide Line Highlight";
        ColorLabelHeader = "Title Bar Background";
        ColorLabelPanelDivider = "Panel Divider";
        ColorLabelViewerBackground = "Viewer Background";
        ColorLabelAutoHideHandle = "Auto-Hide Handle/Bar";
        ButtonDefaults = "Defaults";
        ButtonClose = "Close";
        ButtonExportColors = "Export";
        ButtonImportColors = "Import";
        ColorFileFilter = "Edgetree colours (*.json)|*.json";
        ColorFileDefaultName = "edgetree-colors.json";
        ColorImportFailedTitle = "Import Colours";
        ColorImportFailedBody = "That file holds no colours.";
        ColorThemeDarkMode = "Dark Mode";
        ColorThemeLightMode = "Light Mode";
        ButtonRandomColors = "Random";
        ButtonRandomColorsTip = "Combinations that go together";
        ButtonDaringColors = "Bold";
        ButtonDaringColorsTip = "Bolder combinations, starting from a primary hue";
        ButtonUndoRandom = "Undo";
        ColorThemeDarkLabel = "Dark Mode";
        ColorThemeLightLabel = "Light Mode";
        ColorResetConfirmTitle = "Reset Colors";
        ColorResetConfirmBody = "This will reset the colors you've set in {0}. Continue?";

        AboutTitle = "About";
        AboutVersionLabel = "Version";
        AboutAuthorLabel = "Author";
        AboutDateLabel = "Date";
        AboutLicenseLabel = "License Summary";
        AboutWebsiteLabel = "Website";
        AboutOtherToolLabel = "Another tool by the same maker";
        AboutUpdateAvailableFormat = "Download update {0}";
        AboutLicenseSummary =
            "MIT License. Provided as-is, without warranty; use is at your own discretion.";
        AboutIconLicenseLabel = "Icons";
        AboutIconLicenseValue =
            "File and folder icons: Material Icon Theme (MIT)\n" +
            "Interface glyphs: Material Symbols, Google (Apache License 2.0)";
        AboutIconLicenseOpen = "Read the Apache License 2.0";

        TrayOpen = "Open";
        TrayHide = "Send to Tray";
        TrayAbout = "About";
        TrayExit = "Exit";
        TrayUpdateAvailable = "New update - v{0}";

        PasteFailedTitle = "Paste Failed";
        MoveIntoSelfError = "A folder can't be moved into itself or into one of its own subfolders.";
        CopyIntoSelfError = "A folder can't be copied into itself or into one of its own subfolders.";
        NewFolderFailedTitle = "Failed to Create Folder";
        RenameFailedTitle = "Rename Failed";
        NewFolderDefaultName = "New Folder";
        RenameFailedBody = "Could not rename this item.";
        DeleteConfirmTitle = "Confirm Delete";
        DeleteConfirmBody = "Send '{0}' to the Recycle Bin?";
        DeleteConfirmBodyMultiple = "Send {0} selected items to the Recycle Bin?";
        DeleteFailedTitle = "Delete Failed";
        CompressFailedTitle = "Compress Failed";
        ExtractFailedTitle = "Extract Failed";
        CompressSkippedBody = "{0} items could not be read and were skipped.";
        StartWithWindowsFailedTitle = "Start with Windows";
        StartWithWindowsFailedBody = "Failed to register as a startup program. It may be restricted by administrator policy.";
        LanguageChangeTitle = "Language Changed";
        LanguageChangeBody = "Changing the language requires restarting the app. Restart now?";
        ImportFailedTitle = "Import Failed";
        OverwriteConfirmTitle = "Confirm Overwrite";
        OverwriteConfirmBody = "'{0}' already exists. Overwrite it?";

        ExportSettingsFailedTitle = "Failed to Export Settings";
        ImportSettingsFailedTitle = "Failed to Import Settings";
        SettingsImportedTitle = "Settings Imported";
        SettingsImportedBody = "Settings were imported. Restarting the app is required to apply them. Restart now?";

        ResetSettingsConfirmTitle = "Reset Settings";
        ResetSettingsConfirmBody = "All settings and favorites will be reset to the app's default state. This cannot be undone.\n\nThe app will restart afterward to apply it. Continue?";

#if INSTRUMENT
        RootPathLabel += " (INSTRUMENTED)";
#elif DEBUG
        RootPathLabel += " (DEBUG)";
#endif
    }
}
