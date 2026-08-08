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
    public static string BookmarkShortcutNext = "다음 북마크";
    public static string BookmarkShortcutPrev = "이전 북마크";
    public static string MenuNewFolder = "새 폴더";
    public static string MenuRefresh = "새로고침";
    public static string MenuOpen = "열기";
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
    // shoulder to shoulder (user, 2026-08-08).
    public static string MenuGeneralSettings = "기본 설정";
    public static string MenuStartWithWindows = "부팅 후 자동 시작";
    public static string MenuAlwaysShowTrayIcon = "트레이 아이콘";
    public static string MenuShowFolderIcons = "폴더 아이콘";
    public static string MenuShowFileIcons = "파일 아이콘";
    public static string MenuHideTitleBarTitle = "제목 표시줄 타이틀 제거";
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
    public static string ToolTipClose = "종료";
    public static string RootPathLabel = "내 PC";

    // Synthetic "show the rest" row appended under a folder capped at
    // FileSystemItem.DisplayCap items. {0} is the hidden count.
    public static string ShowMoreFormat = "… 더 보기 ({0}개)";

    // File search (Ctrl+F view - see MainWindow's search-view methods and
    // Services/FileSearchService)
    public static string ToolTipSearch = "검색 (Ctrl+F)";
    public static string ToolTipViewer = "이미지 뷰어";
    // Viewer zoom strip - two chips wide, so both stay short in every language.
    public static string ViewerZoomFit = "맞춤";
    public static string ViewerZoomActual = "1:1";
    public static string ViewerNavigator = "내비게이터";
    public static string ViewerClose = "뷰어 닫기";
    public static string ToolTipExitSearch = "탐색기로 (Ctrl+E)";
    public static string SearchTooltipBrowseFolder = "검색할 폴더 선택";
    public static string SearchTooltipRefresh = "다시 인덱싱";
    public static string SearchTooltipRefreshStale = "다시 인덱싱 · 이 폴더가 바뀌었습니다";
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
    // other PC may not have (user, 2026-08-04).
    public static string ButtonExportColors = "내보내기";
    public static string ButtonImportColors = "불러오기";
    public static string ColorFileFilter = "Edgetree 색상 (*.json)|*.json";
    public static string ColorFileDefaultName = "edgetree-colors.json";
    public static string ColorImportFailedTitle = "색상 불러오기";
    public static string ColorImportFailedBody = "이 파일에는 색상이 없습니다.";
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
        MenuBookmark = "Bookmark";
        MenuBookmarkAdd = "Add Bookmark";
        MenuBookmarkRemove = "Remove Bookmark";
        MenuBookmarkList = "Bookmarks";
        MenuBookmarkListEmpty = "No bookmarks";
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
        BookmarkShortcutNext = "Next bookmark";
        BookmarkShortcutPrev = "Previous bookmark";
        MenuNewFolder = "New Folder";
        MenuRefresh = "Refresh";
        MenuAutoCollapse = "Accordion Mode";
        MenuCollapseAllExpanded = "Collapse All Expanded Folders";
        CollapseAllConfirmTitle = "Collapse All Expanded Folders";
        CollapseAllConfirmBody = "Collapse every folder that is currently expanded?";
        MenuOpen = "Open";
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
        MenuProperties = "Properties";
        MenuRemoveFavorite = "Remove from Favorites";

        MenuAlwaysOnTop = "Always on Top";
        MenuGeneralSettings = "General";
        MenuStartWithWindows = "Start with Windows";
        MenuAlwaysShowTrayIcon = "Always Show Tray Icon";
        MenuShowFolderIcons = "Show Folder Icons";
        MenuShowFileIcons = "Show File Icons";
        MenuHideTitleBarTitle = "Hide Title Bar Text";
        MenuFavoritesAtBottom = "Show at Bottom";
        MenuDockOnRight = "Pin to Right Edge";
        MenuAutoHideCloseOnLeave = "Close on Mouse Leave";
        MenuAutoHideUseHandle = "Handle Instead of Full Edge";
        MenuAutoHideSlide = "Slide Animation";
        MenuAutoHideSliverWidth = "Auto-Hide Thickness";
        MenuColorSettings = "Color Settings";
        ColorSwatchTooltip = "Click: color picker · Right-click: enter a color code";
        ColorHexInputHint = "#RRGGBB · Enter to apply, Esc to cancel";
        MenuAbout = "About";
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
        ToolTipClose = "Exit";
        RootPathLabel = "This PC";
        ShowMoreFormat = "… Show {0} more";

        ToolTipSearch = "Search (Ctrl+F)";
        ToolTipViewer = "Image Viewer";
        ViewerZoomFit = "Fit";
        ViewerZoomActual = "1:1";
        ViewerNavigator = "Navigator";
        ViewerClose = "Close viewer";
        ToolTipExitSearch = "Back to Explorer (Ctrl+E)";
        SearchTooltipBrowseFolder = "Choose folder to search";
        SearchTooltipRefresh = "Reindex";
        SearchTooltipRefreshStale = "Reindex · this folder has changed";
        SearchTooltipHistory = "Recent searches";
        SearchHistoryDeleteTooltip = "Remove this search";
        SearchBrowseFolderDialogTitle = "Choose a folder to search";
        SearchScopeNone = "Choose a folder to search →";
        SearchBoxPlaceholder = "Search";
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

#if DEBUG
        RootPathLabel += " (DEBUG)";
#endif
    }
}
