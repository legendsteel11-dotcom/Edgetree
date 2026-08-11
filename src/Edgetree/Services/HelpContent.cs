namespace SidebarExplorer.App.Services;

// What F1 shows: the app as a list of things you can do and how to do them.
//
// ROWS ONLY, no prose (2026-08-11). Every section used to open with a sentence
// and close with a note, and both went: nobody reads them, the register never
// quite matched the words people actually use, and a page of paragraphs made a
// simple app look hard to learn. Whatever in those notes was worth keeping
// became a ROW, which is the shape that gets read.
//
// AND A ROW HAS TO EARN ITS LINE. If the menu item's own name already says what
// it does - 우클릭 → 즐겨찾기 doing "keep it in the side panel" - the row is
// reading the label back and it goes. What belongs here is what the label
// cannot say: keys, gestures, and the consequences nobody would guess.
//
// BOTH LANGUAGES LIVE ON THE SAME LINE. Two files would drift the first time a
// row was added in a hurry, and the drift would be invisible until someone
// switched language; here a half-written row does not compile.
//
// This is a document about behaviour, so it goes stale when behaviour changes
// and nothing here can notice. The keys are declared inline in MainWindow's key
// handler, so there is no table to read them off - which means the release
// checklist has to carry "read the help through" until there is.
public static class HelpContent
{
    public sealed record Row(string Gesture, string Meaning);

    // SubTitle starts a group inside a section; empty means the rows follow the
    // section title directly.
    public sealed record Group(string SubTitle, IReadOnlyList<Row> Rows);

    public sealed record Section(string Title, IReadOnlyList<Group> Groups);

    private static Row R(string koGesture, string koMeaning, string enGesture, string enMeaning)
        => Strings.IsEnglish ? new Row(enGesture, enMeaning) : new Row(koGesture, koMeaning);

    private static string T(string ko, string en) => Strings.IsEnglish ? en : ko;

    // The box at the top. The one place here that is not a gesture and its
    // meaning, and it earns that: everything below answers "how do I do X",
    // which only helps once you know what X is worth doing.
    public static string TipsTitle
        => T("처음 사용자 TIP", "New here? Start with these");

    public static IReadOnlyList<string> Tips() => new[]
    {
        T("숨는 방식과 크기(높이) 정하기", "Choose how it hides, and its size"),
        T("사용하지 않는 폴더 감추기", "Hide the folders you never open"),
        T("자주 쓰는 파일 종류 지정하기 (제외도 함께)", "Set the file kinds you work with - and the ones to exclude"),
        T("자주 가는 폴더를 북마크나 즐겨찾기로", "Bookmark the folders you keep going back to"),
        T("썸네일 캐싱 옵션을 켜고 많은 이미지를 빠르게 관리",
          "Turn thumbnail caching on and move through big image folders fast"),
        T("색상을 원하는 대로 맞추기", "Set the colours the way you want them"),
        T("검색 결과는 폴더별 묶기로 보기", "Read search results grouped by folder"),
    };

    public static IReadOnlyList<Section> Build() => new[]
    {
        new Section(
            T("사이드바", "The sidebar"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("가장자리 혹은 손잡이에 커서 이동", "숨어 있을 때 다시 꺼내기",
                      "Point at the screen edge, or at the handle", "Bring it back while hidden"),
                    R("핀 클릭", "펼친 채로 두기 ↔ 자동 숨김",
                      "Click the pin", "Stay open ↔ auto-hide"),
                    R("제목 표시줄 오른쪽 끝 ─", "사이드바 치우기 (종료 아님)",
                      "The ─ at the end of the title bar", "Put the sidebar away - it does not quit"),
                    R("제목 표시줄 우클릭", "도움말 · 트레이로 최소화 · 다시 시작 · 종료",
                      "Right-click the title bar", "Help · Minimise to tray · Restart · Quit"),
                    R("가장자리 드래그", "너비 조절",
                      "Drag the outer edge", "Resize"),
                    R("가장자리 더블클릭", "내용에 맞춰 너비 맞춤",
                      "Double-click the outer edge", "Fit the width to the contents"),
                    R("위 · 아래 가장자리 드래그", "높이와 위치 (짧은 밴드)",
                      "Drag the top or bottom edge", "Height, and where the band sits"),
                    R("그 가장자리 더블클릭", "화면 높이 전체로 되돌리기",
                      "Double-click that edge", "Back to the full height"),
                    R("드래그 중 Shift · Ctrl", "격자에 맞춰 붙이기 (Ctrl이 더 촘촘)",
                      "Shift · Ctrl while dragging", "Snap to a grid - Ctrl is the finer one"),
                }),
            }),

        new Section(
            T("창 모드", "Window mode"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("제목 표시줄 드래그", "창으로 떼어내기 · 옮기기",
                      "Drag the title bar", "Pull it off, and move it"),
                    R("제목 표시줄 더블클릭", "최대화 · 되돌리기",
                      "Double-click the title bar", "Maximise · restore"),
                    R("가장자리 · 모서리 드래그", "크기 조절",
                      "Drag an edge or corner", "Resize"),
                    R("가장자리로 드래그", "다시 붙이기",
                      "Drag to a screen edge", "Dock it again"),
                    R("핀 클릭", "붙어 있는 상태로 되돌리기",
                      "Click the pin", "Go back to being docked"),
                }),
            }),

        new Section(
            T("트리", "The tree"),
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
                    R("Ctrl+Alt+K", "북마크 표시 · 해제", "Ctrl+Alt+K", "Bookmark, or take it back"),
                    R("Ctrl+Alt+L · J", "다음 · 이전 북마크로", "Ctrl+Alt+L · J", "Next · previous bookmark"),
                    // The parenthesis is the part a menu label cannot say, and
                    // it is what a whole paragraph used to say instead.
                    R("우클릭 → 폴더 숨기기", "검색으로는 그대로 찾힘",
                      "Right-click → Hide this folder", "Search still finds what is inside"),
                }),
            }),

        new Section(
            T("필터와 정렬", "Filtering and sorting"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("아래쪽 필터 버튼 줄", "종류별로 걸러 보기",
                      "The filter buttons at the bottom", "Show only some kinds of file"),
                    R("필터 버튼 → 사용자 지정", "확장자를 직접 적기",
                      "A filter button → Custom", "Type the extensions yourself"),
                    R("폴더 행의 정렬 아이콘", "그 폴더만 따로 정렬", "The sort icon on a folder row", "Sort that one folder its own way"),
                    R("옵션 → 정렬 기준", "전체 기본 정렬", "Options → Sort by", "The default for everything"),
                    R("더 보기", "나머지 마저 펼치기", "Show more", "Reveal the rest"),
                }),
            }),

        new Section(
            T("검색", "Search"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("Ctrl+F", "검색 열기", "Ctrl+F", "Open search"),
                    R("제목 표시줄 돋보기", "검색 열기 · 닫기",
                      "The magnifier in the title bar", "Open · close search"),
                    R("폴더 아이콘", "검색 범위 선택", "The folder icon", "Choose where to look"),
                    // Three rows for what is one conditional behaviour, and the
                    // condition is what each row turns on: with the panel shut
                    // a click can only mean "take me there", and with it open
                    // that meaning moves to the double click so the single one
                    // can show the file without collapsing the list. Stated as
                    // three plain outcomes rather than as one row with an
                    // if-clause in it.
                    R("결과 클릭", "트리의 해당 항목으로 이동",
                      "Click a result", "Go to it in the tree"),
                    R("결과 클릭 · ↑ ↓", "(이미지 뷰어 연 상태) 검색 결과 항목 이동",
                      "Click a result · ↑ ↓", "(with the image viewer open) Move through the results"),
                    R("결과 더블클릭", "(이미지 뷰어 연 상태) 트리의 해당 항목으로 이동",
                      "Double-click a result", "(with the image viewer open) Go to it in the tree"),
                    R("결과 표시 수", "기본 1,000개 · 목록 끝의 더 보기로 1,000개씩 더",
                      "How many results show", "1,000 at a time - \"Show more\" at the end adds another 1,000"),
                    // The dot is the half a label cannot say: the button names
                    // the act, nothing names the reason to press it.
                    R("상태줄 오른쪽 ↻", "다시 인덱싱 — 파란 점은 폴더가 바뀌었다는 표시",
                      "The ↻ on the status line", "Reindex - a blue dot means the folder has changed"),
                    R("Esc · Ctrl+E", "검색 닫기", "Esc · Ctrl+E", "Close search"),
                }),
            }),

        new Section(
            T("이미지 뷰어", "The image viewer"),
            new[]
            {
                new Group(T("이미지", "Images"), new[]
                {
                    R("휠", "확대 · 축소", "Wheel", "Zoom in · out"),
                    R("드래그", "확대했을 때 이동", "Drag", "Move around, when zoomed in"),
                    // What the plate IS, which nothing on screen says - it
                    // appears on its own once there is more picture than panel,
                    // and a small dark square in a corner explains nothing by
                    // itself.
                    R("내비게이터", "확대하면 오른쪽 아래에 — 전체에서 지금 보는 자리, 눌러서 이동 (▣ 버튼으로 켜고 끔)",
                      "The navigator",
                      "Bottom right, once zoomed in - where you are in the whole image; click to go there (the ▣ button turns it off)"),
                    R("좌우로 길게 드래그", "이전 · 다음 이미지", "Drag sideways, a good push", "Previous · next image"),
                    R("더블클릭", "맞춤 ↔ 1:1", "Double-click", "Fit ↔ actual size"),
                    R("가운데 클릭 · Enter", "전체 화면", "Middle-click · Enter", "Full screen"),
                    R("Esc", "전체 화면 나가기", "Esc", "Leave full screen"),
                    R("↑ ↓", "이전 · 다음 항목", "↑ ↓", "Previous · next item"),
                    R("← →", "썸네일 바가 켜져 있을 때 이전 · 다음",
                      "← →", "Previous · next, while the thumbnail bar is open"),
                }),

                new Group(T("영상", "Film"), new[]
                {
                    R("Space · 화면 클릭", "재생 · 정지", "Space · click the video", "Play · pause"),
                    R("← →", "5초 뒤 · 앞", "← →", "Back · forward 5 seconds"),
                    R("마우스 앞 · 뒤 버튼", "10초 뒤 · 앞", "Mouse back · forward buttons", "Back · forward 10 seconds"),
                    R("같은 영상 다시 재생", "보던 자리에서 이어보기",
                      "Play the same file again", "It carries on from where you left off"),
                    R("Home", "처음부터", "Home", "Back to the start"),
                    R("P · Insert", "영상 북마크 표시 · 해제",
                      "P · Insert", "Bookmark this moment, or take it back"),
                    R("우클릭 → 북마크 목록", "표시해 둔 자리로 이동 · 전체 삭제",
                      "Right-click → Bookmarks", "Jump to one, or clear them all"),
                    R("재생 중 ↑ ↓", "안 받음 (일시정지하면 돌아옴)",
                      "↑ ↓ while playing", "Refused - they come back when it is paused"),
                    R("우클릭 → 자막", "켜기 · 크기 · 싱크", "Right-click → Subtitles", "On, size, and sync"),
                    R("전체 화면에서 아래쪽", "재생 막대 꺼내기",
                      "Full screen, point at the bottom", "Bring the transport bar back"),
                    // The rows someone asks about rather than discovers, and the
                    // help is where that answer belongs: this is the page people
                    // are already looking for something on, so saying what does
                    // not work here reads as an answer instead of as a warning
                    // (which is why it is NOT on the download page).
                    //
                    // The extension list is VideoExtensions. WHAT THE ROWS DO
                    // NOT DO is name a codec: a first draft said "mkv does not
                    // play" and "install the Store's HEVC/AV1 extensions", and
                    // the first was falsified within the hour (2026-08-11 - mkv
                    // plays fine on a machine with a codec pack). Playback is
                    // MediaElement, the OLD pipeline, so it uses the DirectShow
                    // filters installed on the PC and not the Store's Media
                    // Foundation extensions - which is exactly why the same file
                    // plays on one machine and not another, and why the honest
                    // row is the one that says so.
                    R("재생되는 형식",
                      "mp4 · m4v · mov · avi · wmv · mkv · webm · mpg · mpeg · ts · m2ts · mts · flv · 3gp",
                      "Formats it plays",
                      "mp4 · m4v · mov · avi · wmv · mkv · webm · mpg · mpeg · ts · m2ts · mts · flv · 3gp"),
                    R("코덱", "외부 코덱 사용 (필요 시 사용자 설치)",
                      "Codecs", "Whatever is installed on the PC - add one if a file needs it"),
                    // "트리에서" is not padding: Enter INSIDE the panel is full
                    // screen (see the images group above), and this row sits in
                    // the viewer's own section.
                    R("재생이 안 되면", "기본 프로그램 사용 권장 (트리에서 더블클릭 · Enter)",
                      "When it will not play", "Better opened in your usual player (double-click or Enter, in the tree)"),
                }),

                new Group(T("썸네일 바", "The thumbnail bar"), new[]
                {
                    R("장수 표시 옆 ▤ 버튼", "아래 줄에 그 폴더의 이미지들 — 검색 중이면 결과",
                      "The ▤ button beside the counter", "The folder's images as a row - or the results, while searching"),
                    R("칸을 밖으로 드래그", "다른 앱에 떨어뜨리기", "Drag a cell out", "Drop it into another app"),
                    R("바 위쪽 가장자리 드래그", "칸 크기", "Drag the bar's top edge", "Cell size"),
                    R("옵션 → 이미지 뷰어", "이미지 썸네일 캐싱 · 캐싱 파일 정리",
                      "Options → Image viewer", "Preload thumbnails · clean the cache up"),
                }),
            }),

        new Section(
            T("모양과 설정", "Appearance and settings"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("옵션 → 색상 설정", "직접 선택 · 랜덤 지정", "Options → Colours", "Pick them, or roll the dice"),
                    R("옵션 → 기본 설정", "자동 시작 · 트레이 · 아이콘 · 자동 숨김", "Options → General", "Autostart · tray · icons · auto-hide"),
                }),
            }),
    };
}
