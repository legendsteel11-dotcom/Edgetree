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
    public static string MenuNewFolder = "새 폴더";
    public static string MenuRefresh = "새로고침";
    public static string MenuOpen = "열기";
    public static string MenuOpenWith = "연결 프로그램";
    public static string MenuCopy = "복사";
    public static string MenuPaste = "붙여넣기";
    public static string MenuRename = "이름 바꾸기";
    public static string MenuDelete = "삭제";
    public static string MenuCopyPath = "경로 복사";
    public static string MenuOpenTerminal = "터미널에서 열기";
    public static string MenuOpenWithCode = "Code로 열기";
    public static string MenuRevealInExplorer = "탐색기에서 위치 열기";
    public static string MenuProperties = "속성";
    public static string MenuRemoveFavorite = "즐겨찾기에서 제거";

    // Options ("...") menu
    public static string MenuAutoCollapse = "폴더 자동 접기";
    public static string MenuCollapseAllExpanded = "모든 펼친 폴더 접기";
    public static string CollapseAllConfirmTitle = "모든 펼친 폴더 접기";
    public static string CollapseAllConfirmBody = "모든 펼쳐졌던 폴더를 접겠습니까?";
    public static string MenuAlwaysOnTop = "항상 위에 표시";
    public static string MenuStartWithWindows = "부팅 후 자동 시작";
    public static string MenuAlwaysShowTrayIcon = "트레이 아이콘";
    public static string MenuShowFolderIcons = "폴더 아이콘";
    public static string MenuShowFileIcons = "파일 아이콘";
    public static string MenuHideTitleBarTitle = "제목 표시줄 타이틀 제거";
    public static string MenuFavoritesAtBottom = "즐겨찾기를 아래에 표시";
    public static string MenuDockOnRight = "고정 위치 오른쪽";
    public static string MenuAutoHideCloseOnLeave = "마우스 이탈 시 닫기";
    public static string MenuAutoHideSliverWidth = "숨김 시 막대 두께";
    public static string MenuColorSettings = "색상 설정";
    public static string MenuAbout = "앱 정보";

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
    public static string MenuDefaultSort = "정렬 기본값";
    public static string MenuSort = "이 폴더의 정렬";
    public static string MenuSortByName = "이름순";
    public static string MenuSortByDate = "날짜순";
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
    public static string SortTooltipFormat = "정렬: {0} (클릭하여 전환)";
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
    public static string MenuTabSpacing = "탭 간격";
    public static string MenuRowSpacing = "행 간격";
    public static string MenuScrollBarThickness = "스크롤바 두께";
    public static string MenuExportSettings = "설정 내보내기...";
    public static string MenuImportSettings = "설정 가져오기...";
    public static string MenuResetSettings = "전체 설정 초기화...";

    // Header buttons (ToolTips) and root label
    public static string ToolTipToggle = "자동 숨김";

    // Which one shows depends on AppSettings.DockOnRight (see
    // MainWindow.xaml.cs's UpdatePinButtonVisibility) - PinButton's ToolTip
    // is set from code, not bound to a single static string like the others.
    public static string ToolTipPinLeft = "좌측에 고정";
    public static string ToolTipPinRight = "우측에 고정";
    public static string ToolTipCollapseAll = "모두 접기";
    public static string ToolTipRestoreExpanded = "펼침 상태 복원";
    public static string ToolTipOptions = "옵션";
    public static string ToolTipMinimize = "트레이로 최소화";
    public static string ToolTipClose = "종료";
    public static string RootPathLabel = "내 PC";

    // Synthetic "show the rest" row appended under a folder capped at
    // FileSystemItem.DisplayCap items. {0} is the hidden count.
    public static string ShowMoreFormat = "… 더 보기 ({0}개)";

    // File search (Ctrl+F view - see MainWindow's search-view methods and
    // Services/FileSearchService)
    public static string ToolTipSearch = "검색 (Ctrl+F)";
    public static string ToolTipExitSearch = "탐색기로 (Ctrl+E)";
    public static string SearchTooltipBrowseFolder = "검색할 폴더 선택";
    public static string SearchTooltipRefresh = "다시 인덱싱";
    public static string SearchTooltipHistory = "최근 검색어";
    public static string SearchHistoryDeleteTooltip = "이 검색어 삭제";
    public static string SearchBrowseFolderDialogTitle = "검색할 폴더를 선택하세요";
    public static string SearchScopeNone = "검색할 폴더를 선택하세요 →";
    public static string SearchBoxPlaceholder = "검색";
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
    public static string ColorLabelFolderNameFont = "폴더명";
    public static string ColorLabelFolderNameHighlightFont = "폴더명 하이라이트";
    public static string ColorLabelFileNameFont = "파일명";
    public static string ColorLabelFileNameHighlightFont = "파일명 하이라이트";
    public static string ColorLabelSelection = "선택된 항목";
    public static string ColorLabelHistory = "즐겨찾기 배경";
    public static string ColorLabelHoverBackground = "마우스 hover";
    public static string ColorLabelFolderNameHoverFont = "폴더명 마우스 hover";
    public static string ColorLabelFileNameHoverFont = "파일명 마우스 hover";
    public static string ColorLabelShowMore = "더 보기";
    public static string ColorLabelGuideLine = "탭 구분선";
    public static string ColorLabelGuideLineActive = "탭 구분선 하이라이트";
    public static string ColorLabelHeader = "제목 표시줄 배경";
    public static string ColorLabelPanelDivider = "영역 구분선";
    public static string ButtonDefaults = "기본값";
    public static string ButtonClose = "닫기";
    public static string ColorThemeDarkMode = "🌙 다크 모드";
    public static string ColorThemeLightMode = "☀️ 라이트 모드";
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
    public static string AboutLicenseLabel = "라이센스 요약";
    public static string AboutGithubLabel = "GitHub";
    public static string AboutWebsiteLabel = "웹사이트";
    public static string AboutAuthorValue = "pjh85336@gmail.com";
    public static string AboutLicenseSummary =
        "MIT 라이선스. 번들된 아이콘은 Material Icon Theme 프로젝트(MIT)에서 가져왔습니다. " +
        "별도의 보증 없이 있는 그대로 제공되며, 사용에 따른 책임은 사용자 본인에게 있습니다.";

    // Tray
    public static string TrayOpen = "열기";
    public static string TrayHide = "트레이로";
    public static string TrayAbout = "정보";
    public static string TrayExit = "종료";

    // MessageBox titles/bodies
    public static string PasteFailedTitle = "붙여넣기 실패";
    public static string NewFolderFailedTitle = "새 폴더 만들기 실패";
    public static string RenameFailedTitle = "이름 바꾸기 실패";
    public static string DeleteConfirmTitle = "삭제 확인";
    public static string DeleteConfirmBody = "'{0}'을(를) 휴지통으로 보낼까요?";
    public static string DeleteFailedTitle = "삭제 실패";
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

    public static void Initialize(string language)
    {
        if (language != "en")
        {
#if DEBUG
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

        MenuAddFavorite = "Add to Favorites";
        MenuNewFolder = "New Folder";
        MenuRefresh = "Refresh";
        MenuAutoCollapse = "Accordion Mode";
        MenuCollapseAllExpanded = "Collapse All Expanded Folders";
        CollapseAllConfirmTitle = "Collapse All Expanded Folders";
        CollapseAllConfirmBody = "Collapse every folder that is currently expanded?";
        MenuOpen = "Open";
        MenuOpenWith = "Open With";
        MenuCopy = "Copy";
        MenuPaste = "Paste";
        MenuRename = "Rename";
        MenuDelete = "Delete";
        MenuCopyPath = "Copy Path";
        MenuOpenTerminal = "Open in Terminal";
        MenuOpenWithCode = "Open with Code";
        MenuRevealInExplorer = "Reveal in Explorer";
        MenuProperties = "Properties";
        MenuRemoveFavorite = "Remove from Favorites";

        MenuAlwaysOnTop = "Always on Top";
        MenuStartWithWindows = "Start with Windows";
        MenuAlwaysShowTrayIcon = "Always Show Tray Icon";
        MenuShowFolderIcons = "Show Folder Icons";
        MenuShowFileIcons = "Show File Icons";
        MenuHideTitleBarTitle = "Hide Title Bar Text";
        MenuFavoritesAtBottom = "Show Favorites at Bottom";
        MenuDockOnRight = "Pin to Right Edge";
        MenuAutoHideCloseOnLeave = "Close on Mouse Leave";
        MenuAutoHideSliverWidth = "Auto-Hide Thickness";
        MenuColorSettings = "Color Settings";
        MenuAbout = "About";
        MenuDefaultSort = "Default Sort";
        MenuSort = "Sort";
        MenuSortByName = "By Name";
        MenuSortByDate = "By Date";
        MenuSortAscending = "Ascending";
        MenuSortDescending = "Descending";
        MenuFollowDefaultSort = "Follow Default Sort";
        MenuSearchInFolder = "Search in This Folder";
        SortTooltipFormat = "Sort: {0} (click to cycle)";
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

        ToolTipToggle = "Auto-Hide";
        ToolTipPinLeft = "Pin to Left";
        ToolTipPinRight = "Pin to Right";
        ToolTipCollapseAll = "Collapse All";
        ToolTipRestoreExpanded = "Restore Expanded";
        ToolTipOptions = "Options";
        ToolTipMinimize = "Minimize to Tray";
        ToolTipClose = "Exit";
        RootPathLabel = "This PC";
        ShowMoreFormat = "… Show {0} more";

        ToolTipSearch = "Search (Ctrl+F)";
        ToolTipExitSearch = "Back to Explorer (Ctrl+E)";
        SearchTooltipBrowseFolder = "Choose folder to search";
        SearchTooltipRefresh = "Reindex";
        SearchTooltipHistory = "Recent searches";
        SearchHistoryDeleteTooltip = "Remove this search";
        SearchBrowseFolderDialogTitle = "Choose a folder to search";
        SearchScopeNone = "Choose a folder to search →";
        SearchBoxPlaceholder = "Search";
        SearchStatusScanning = "Indexing… ({0}) · you can search now";
        SearchStatusResults = "{0} results";
        SearchStatusResultsCapped = "Showing {0} of {1} results";
        SearchStatusNoResults = "No results";
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
        ColorLabelHistory = "Favorites Background";
        ColorLabelHoverBackground = "Mouse Hover";
        ColorLabelFolderNameHoverFont = "Folder Name Mouse Hover";
        ColorLabelFileNameHoverFont = "File Name Mouse Hover";
        ColorLabelShowMore = "Show More";
        ColorLabelGuideLine = "Guide Line";
        ColorLabelGuideLineActive = "Guide Line Highlight";
        ColorLabelHeader = "Title Bar Background";
        ColorLabelPanelDivider = "Panel Divider";
        ButtonDefaults = "Defaults";
        ButtonClose = "Close";
        ColorThemeDarkMode = "🌙 Dark Mode";
        ColorThemeLightMode = "☀️ Light Mode";
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
        AboutLicenseSummary =
            "MIT License. Bundled icons are from the Material Icon Theme project (MIT). " +
            "Provided as-is, without warranty; use is at your own discretion.";

        TrayOpen = "Open";
        TrayHide = "Send to Tray";
        TrayAbout = "About";
        TrayExit = "Exit";

        PasteFailedTitle = "Paste Failed";
        NewFolderFailedTitle = "Failed to Create Folder";
        RenameFailedTitle = "Rename Failed";
        DeleteConfirmTitle = "Confirm Delete";
        DeleteConfirmBody = "Send '{0}' to the Recycle Bin?";
        DeleteFailedTitle = "Delete Failed";
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

#if DEBUG
        RootPathLabel += " (DEBUG)";
#endif
    }
}
