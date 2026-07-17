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
    public static string MenuRevealInExplorer = "탐색기에서 위치 열기";
    public static string MenuProperties = "속성";
    public static string MenuRemoveFavorite = "즐겨찾기에서 제거";

    // Options ("...") menu
    public static string MenuAutoCollapse = "다른 폴더 자동 접기";
    public static string MenuAlwaysOnTop = "항상 위에 표시";
    public static string MenuStartWithWindows = "윈도우 시작 시 실행";
    public static string MenuAlwaysShowTrayIcon = "트레이 아이콘 항상 표시";
    public static string MenuShowFolderIcons = "폴더 아이콘 표시";
    public static string MenuShowFileIcons = "파일 아이콘 표시";
    public static string MenuFavoritesAtBottom = "즐겨찾기를 아래에 표시";
    public static string MenuDockOnRight = "화면 우측에 고정";
    public static string MenuAutoHideCloseOnLeave = "즉시 자동 숨김";
    public static string MenuAutoHideSliverWidth = "자동 숨김 두께";
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
    public static string MenuDefaultSort = "기본 정렬";
    public static string MenuSort = "정렬 방식";
    public static string MenuSortByName = "이름순";
    public static string MenuSortByDate = "날짜순";
    public static string MenuSortAscending = "오름차순";
    public static string MenuSortDescending = "내림차순";
    public static string MenuMaxItemsPerFolder = "폴더의 표시 개수";
    public static string MenuTabSpacing = "탭 간격";
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

    // About window
    public static string AboutTitle = "정보";
    public static string AboutVersionLabel = "버전";
    public static string AboutAuthorLabel = "제작자";
    public static string AboutDateLabel = "날짜";
    public static string AboutLicenseLabel = "라이센스 요약";
    public static string AboutGithubLabel = "GitHub";
    public static string AboutAuthorValue = "pjh85336@gmail.com";
    public static string AboutLicenseSummary =
        "MIT 라이선스. 번들된 아이콘은 Material Icon Theme 프로젝트(MIT)에서 가져왔습니다. " +
        "별도의 보증 없이 있는 그대로 제공되며, 사용에 따른 책임은 사용자 본인에게 있습니다.";

    // Tray
    public static string TrayOpen = "열기";
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
            return;
        }

        MenuAddFavorite = "Add to Favorites";
        MenuNewFolder = "New Folder";
        MenuRefresh = "Refresh";
        MenuAutoCollapse = "Auto Collapse";
        MenuOpen = "Open";
        MenuOpenWith = "Open With";
        MenuCopy = "Copy";
        MenuPaste = "Paste";
        MenuRename = "Rename";
        MenuDelete = "Delete";
        MenuCopyPath = "Copy Path";
        MenuOpenTerminal = "Open in Terminal";
        MenuRevealInExplorer = "Reveal in Explorer";
        MenuProperties = "Properties";
        MenuRemoveFavorite = "Remove from Favorites";

        MenuAlwaysOnTop = "Always on Top";
        MenuStartWithWindows = "Start with Windows";
        MenuAlwaysShowTrayIcon = "Always Show Tray Icon";
        MenuShowFolderIcons = "Show Folder Icons";
        MenuShowFileIcons = "Show File Icons";
        MenuFavoritesAtBottom = "Show Favorites at Bottom";
        MenuDockOnRight = "Pin to Right Edge";
        MenuAutoHideCloseOnLeave = "Close Instantly on Mouse Leave";
        MenuAutoHideSliverWidth = "Auto-Hide Thickness";
        MenuColorSettings = "Color Settings";
        MenuAbout = "About";
        MenuDefaultSort = "Default Sort";
        MenuSort = "Sort";
        MenuSortByName = "By Name";
        MenuSortByDate = "By Date";
        MenuSortAscending = "Ascending";
        MenuSortDescending = "Descending";
        MenuMaxItemsPerFolder = "Items per Folder";
        MenuTabSpacing = "Indent Spacing";
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

        AboutTitle = "About";
        AboutVersionLabel = "Version";
        AboutAuthorLabel = "Author";
        AboutDateLabel = "Date";
        AboutLicenseLabel = "License Summary";
        AboutLicenseSummary =
            "MIT License. Bundled icons are from the Material Icon Theme project (MIT). " +
            "Provided as-is, without warranty; use is at your own discretion.";

        TrayOpen = "Open";
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
    }
}
