namespace SidebarExplorer.App.Services;

// What F1 shows: the app explained once, by section, with the gesture beside
// the thing it does.
//
// BOTH LANGUAGES LIVE IN THE SAME FILE, side by side on the same line. Two
// files would drift the first time a row was added in a hurry, and the drift
// would be invisible until someone switched language. Here a row that gains a
// Korean half and no English one does not compile.
//
// Rows rather than prose because a gesture and its meaning are two columns of
// the same fact, and because nobody reads a paragraph looking for a key. The
// paragraphs that do exist earn their place: each says a RULE that the rows
// underneath would otherwise have to repeat.
//
// This is a document about behaviour, so it goes stale when behaviour changes
// and nothing here can notice. The keys are declared inline in MainWindow's
// key handler, so there is no table to read them off - which means the release
// checklist has to carry "read the help through" until there is.
public static class HelpContent
{
    public sealed record Row(string Gesture, string Meaning);

    // Note is a line of prose under the rows; SubTitle starts a group inside a
    // section. Both are optional and both are ignored when empty.
    public sealed record Group(string SubTitle, IReadOnlyList<Row> Rows, string Note);

    public sealed record Section(string Title, string Intro, IReadOnlyList<Group> Groups);

    private static Row R(string koGesture, string koMeaning, string enGesture, string enMeaning)
        => Strings.IsEnglish ? new Row(enGesture, enMeaning) : new Row(koGesture, koMeaning);

    private static string T(string ko, string en) => Strings.IsEnglish ? en : ko;

    // The box at the top, before any of the reference below it.
    //
    // Everything under it answers "how do I do X", which is only useful once
    // someone knows what X is worth doing. This is the other half: the handful
    // of settings that turn the app from a tree on the edge of the screen into
    // one shaped for the person using it. Seven lines, in the order they are
    // worth doing - shape first, then what is in it, then how it looks.
    public static string TipsTitle
        => T("처음 사용자 TIP", "New here? Start with these");

    public static IReadOnlyList<string> Tips() => new[]
    {
        T("숨는 방식과 크기(높이) 정하기", "Choose how it hides, and its size"),
        T("사용하지 않는 폴더 감추기", "Hide the folders you never open"),
        T("자주 쓰는 파일 종류 지정하기 (제외도 함께)", "Set the file kinds you work with - and the ones to exclude"),
        T("자주 가는 폴더를 북마크나 즐겨찾기로", "Bookmark the folders you keep going back to"),
        T("이미지가 많은 폴더는 썸네일 미리 불러오기", "Preload thumbnails for folders full of images"),
        T("색상을 원하는 대로 맞추기", "Set the colours the way you want them"),
        T("검색 결과는 폴더별 묶기로 보기", "Read search results grouped by folder"),
    };

    public static IReadOnlyList<Section> Build() => new[]
    {
        new Section(
            T("사이드바", "The sidebar"),
            T("화면 가장자리에 붙어 있다가 필요할 때만 나옵니다. 창으로 떼어내 쓸 수도 있습니다.",
              "It lives at the edge of the screen and comes out when you need it. It can also be pulled off as an ordinary window."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("가장자리에 마우스 대기", "숨어 있을 때 다시 꺼내기",
                      "Point at the screen edge", "Bring it back while hidden"),
                    R("핀 클릭", "펼친 채로 두기 ↔ 자동 숨김",
                      "Click the pin", "Stay open ↔ auto-hide"),
                    R("머리글 우클릭", "트레이로 최소화 · 다시 시작 · 종료",
                      "Right-click the header", "Minimise to tray · Restart · Quit"),
                    R("X", "사이드바 치우기",
                      "X", "Put the sidebar away"),
                    R("가장자리 드래그", "너비 조절",
                      "Drag the outer edge", "Resize"),
                    R("가장자리 더블클릭", "내용에 맞춰 너비 맞춤",
                      "Double-click the outer edge", "Fit the width to the contents"),
                    R("옵션 → 고정 위치 오른쪽", "왼쪽 · 오른쪽 가장자리",
                      "Options → Dock on the right", "Left or right edge"),
                    R("위 · 아래 가장자리로", "짧은 도킹 밴드로",
                      "Drag to the top or bottom", "Dock as a short band"),
                }, T("X는 앱을 끝내지 않습니다 - 실수로 눌러도 잃을 것이 없게 하기 위해서입니다. 종료는 머리글 우클릭과 트레이 아이콘에 있습니다.",
                     "The X does not quit - hitting it by accident costs nothing. Quitting lives in the header's right-click menu and in the tray icon.")),
            }),

        new Section(
            T("창 모드", "Window mode"),
            T("가장자리에서 떼어내면 보통 창처럼 씁니다. 다시 가장자리로 끌면 붙습니다.",
              "Pulled off the edge, it behaves like any other window. Drag it back to an edge to dock it again."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("머리글 드래그", "창으로 떼어내기 · 옮기기",
                      "Drag the header", "Pull it off, and move it"),
                    R("머리글 더블클릭", "최대화 · 되돌리기",
                      "Double-click the header", "Maximise · restore"),
                    R("가장자리 · 모서리 드래그", "크기 조절",
                      "Drag an edge or corner", "Resize"),
                    R("가장자리로 드래그", "다시 붙이기",
                      "Drag to a screen edge", "Dock it again"),
                    R("핀 클릭", "붙어 있는 상태로 되돌리기",
                      "Click the pin", "Go back to being docked"),
                    R("옵션 → 항상 위에 표시", "다른 창 위에 두기",
                      "Options → Always on top", "Keep it above other windows"),
                }, T("창 모드에서는 작업 표시줄 단추와 Alt+Tab이 생깁니다. 붙어 있을 때는 둘 다 없고, 그래서 X가 하는 일도 달라집니다 - 트레이나 자동 숨김으로 갑니다.",
                     "In window mode it has a taskbar button and a place in Alt+Tab. Docked it has neither, which is why the X does something different there - it goes to the tray, or into hiding.")),
            }),

        new Section(
            T("트리", "The tree"),
            T("드라이브부터 폴더를 펼쳐 내려갑니다. 자주 가는 곳은 즐겨찾기와 북마크로 표시해 둘 수 있습니다.",
              "Drives at the top, folders opening downwards. Places you keep going back to can be marked as favourites or bookmarks."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("↑ ↓", "위 · 아래 행", "↑ ↓", "Previous · next row"),
                    R("← →", "접기 · 펼치기", "← →", "Collapse · expand"),
                    R("Ctrl + 휠", "빠르게 스크롤", "Ctrl + wheel", "Scroll about five times faster"),
                    R("Ctrl + / −", "글자 크기", "Ctrl + / −", "Text size"),
                    R("Enter", "열기", "Enter", "Open"),
                    R("F2", "이름 바꾸기", "F2", "Rename"),
                    R("F5", "새로고침", "F5", "Refresh"),
                    R("F7", "새 폴더", "F7", "New folder"),
                    R("Del", "삭제", "Del", "Delete"),
                    R("Ctrl+C · Ctrl+X · Ctrl+V", "복사 · 잘라내기 · 붙여넣기",
                      "Ctrl+C · Ctrl+X · Ctrl+V", "Copy · cut · paste"),
                    R("Ctrl+Shift+C", "경로 복사", "Ctrl+Shift+C", "Copy path"),
                    R("드래그", "옮기기 (다른 앱으로도)", "Drag", "Move it - to another app as well"),
                }, string.Empty),

                new Group(T("표시해 두기", "Marking places"), new[]
                {
                    R("Ctrl+Alt+K", "북마크 표시 · 해제", "Ctrl+Alt+K", "Bookmark, or take it back"),
                    R("Ctrl+Alt+L · J", "다음 · 이전 북마크로", "Ctrl+Alt+L · J", "Next · previous bookmark"),
                    R("우클릭 → 즐겨찾기", "옆 패널에 담기", "Right-click → Add to favourites", "Keep it in the side panel"),
                    R("우클릭 → 폴더 숨기기", "트리에서 빼기", "Right-click → Hide this folder", "Take it out of the tree"),
                }, T("숨긴 폴더도 검색으로는 찾힙니다. 검색은 일부러 찾아보는 일이라, 분명히 있는 파일이 안 나오는 쪽이 더 곤란합니다.",
                     "A hidden folder is still searchable. Searching is a deliberate act of looking, and a file that is plainly there but cannot be found is the worse surprise.")),
            }),

        new Section(
            T("필터와 정렬", "Filtering and sorting"),
            T("폴더 하나에 파일이 많을 때, 트리를 다 뒤지지 않고 줄이는 방법입니다.",
              "Ways to cut a crowded folder down without hunting through it."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("아래쪽 칩 줄", "종류별로 걸러 보기", "The chip strip at the bottom", "Show only some kinds of file"),
                    R("칩 줄 → 사용자 지정", "확장자를 직접 적기", "Chips → Custom", "Type the extensions yourself"),
                    R("폴더 행의 정렬 아이콘", "그 폴더만 따로 정렬", "The sort icon on a folder row", "Sort that one folder its own way"),
                    R("옵션 → 정렬 기준", "전체 기본 정렬", "Options → Sort by", "The default for everything"),
                    R("옵션 → 표시 개수", "한 폴더에 몇 개까지", "Options → Items per folder", "How many rows before \"더 보기\""),
                    R("더 보기", "나머지 마저 펼치기", "Show more", "Reveal the rest"),
                }, string.Empty),
            }),

        new Section(
            T("검색", "Search"),
            T("선택한 폴더 아래를 이름으로 찾습니다. 한 번 훑은 결과는 저장해 두므로 다시 열 때는 기다리지 않습니다.",
              "Finds files by name under a folder you choose. A scan is kept, so opening it again does not wait."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("머리글 돋보기", "검색 열기 · 닫기", "The magnifier in the header", "Open · close search"),
                    R("폴더 아이콘", "검색 범위 선택", "The folder icon", "Choose where to look"),
                    R("결과 클릭", "트리에서 그 자리로", "Click a result", "Go to it in the tree"),
                    R("Esc", "검색 닫기", "Esc", "Close search"),
                }, T("결과 아래에 색인을 언제 만들었는지 적혀 있습니다. 그 뒤에 생긴 파일은 다시 훑기 전까지 나오지 않습니다.",
                     "The age of the index is written under the results. A file created since then will not appear until it is scanned again.")),
            }),

        new Section(
            T("이미지 뷰어", "The image viewer"),
            T("트리에서 선택한 파일을 옆 패널이 그대로 보여줍니다. 더블클릭은 기본 프로그램으로 넘겨주고, 패널은 그 자리에서 확인하는 곳입니다.",
              "The panel beside the tree shows whatever is selected. Double-click hands the file to the app that owns it; the panel is for looking at it where you are."),
            new[]
            {
                new Group(T("이미지", "Images"), new[]
                {
                    R("휠", "확대 · 축소", "Wheel", "Zoom in · out"),
                    R("드래그", "확대했을 때 이동", "Drag", "Move around, when zoomed in"),
                    R("좌우로 길게 드래그", "이전 · 다음 이미지", "Drag sideways, a good push", "Previous · next image"),
                    R("더블클릭", "맞춤 ↔ 1:1", "Double-click", "Fit ↔ actual size"),
                    R("가운데 클릭 · Enter", "전체 화면", "Middle-click · Enter", "Full screen"),
                    R("Esc", "전체 화면 나가기", "Esc", "Leave full screen"),
                    R("↑ ↓", "이전 · 다음 항목", "↑ ↓", "Previous · next item"),
                    R("← →", "필름스트립이 켜져 있을 때 이전 · 다음",
                      "← →", "Previous · next, while the filmstrip is open"),
                }, T("↑↓는 항목 사이를, ←→는 항목 안을 움직입니다. 영상에는 안(타임라인)이 있어 ←→가 탐색이고, 이미지에는 없어 다음 장으로 갑니다.",
                     "↑↓ move BETWEEN items, ←→ move INSIDE one. A film has an inside - its timeline - so there ←→ seek; an image has none, so they turn the page.")),

                new Group(T("영상", "Film"), new[]
                {
                    R("Space · 화면 클릭", "재생 · 정지", "Space · click the video", "Play · pause"),
                    R("← →", "5초 뒤 · 앞", "← →", "Back · forward 5 seconds"),
                    R("마우스 앞 · 뒤 버튼", "10초 뒤 · 앞", "Mouse back · forward buttons", "Back · forward 10 seconds"),
                    R("Home", "처음으로", "Home", "Back to the start"),
                    R("P · Insert", "지금 위치 기록 · 해제", "P · Insert", "Mark this position, or take it back"),
                    R("우클릭 → 자막", "자막 켜기 · 크기 · 싱크", "Right-click → Subtitles", "Turn them on, size them, shift them"),
                    R("전체 화면에서 아래쪽", "재생 막대 꺼내기",
                      "Full screen, point at the bottom", "Bring the transport bar back"),
                }, T("보던 자리는 묻지 않고 기억합니다. 같은 파일을 다시 재생하면 그 자리에서 이어지고, 처음부터 보려면 Home입니다. 재생 중에는 ↑↓가 듣지 않습니다 - 한 번 잘못 누르면 보던 자리를 잃기 때문이고, 일시정지하면 돌아옵니다.",
                     "Where you left off is remembered without being asked: play the same file again and it carries on from there, and Home is how you start over. ↑↓ are refused while a film is playing - one mis-hit used to throw the place away - and they come back the moment it is paused.")),

                new Group(T("필름스트립(썸네일 바)", "The filmstrip (thumbnail bar)"), new[]
                {
                    R("캐러셀 옆 ▤ 칩", "아래 줄에 폴더의 이미지들", "The ▤ chip beside the counter", "The folder's images as a row"),
                    R("칸을 밖으로 드래그", "다른 앱에 떨어뜨리기", "Drag a cell out", "Drop it into another app"),
                }, T("옵션 → 이미지 뷰어 → 이미지 썸네일 미리 불러오기를 켜면 폴더 전체를 미리 받아 둡니다. 이미지가 많은 폴더를 계속 훑는 사람에게 맞고, 그만큼 시간과 메모리를 씁니다.",
                     "Options → Image viewer → Preload image thumbnails fetches the whole folder ahead of you. It suits working through a big folder of photographs, and it costs the time and memory to do so.")),
            }),

        new Section(
            T("모양과 설정", "Appearance and settings"),
            T("옵션 메뉴(···)에 다 있습니다. 색은 라이트/다크가 따로 저장됩니다.",
              "All in the options menu (···). Colours are stored separately for the light and dark themes."),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("옵션 → 색상 설정", "직접 선택 · 랜덤 지정", "Options → Colours", "Pick them, or roll the dice"),
                    R("옵션 → 기본 설정", "자동 시작 · 트레이 · 아이콘 · 자동 숨김", "Options → General", "Autostart · tray · icons · auto-hide"),
                }, string.Empty),
            }),
    };
}
