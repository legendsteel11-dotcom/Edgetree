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
        T("이미지가 많은 폴더는 썸네일 미리 불러오기", "Preload thumbnails for folders full of images"),
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
                    R("가장자리에 마우스 대기", "숨어 있을 때 다시 꺼내기",
                      "Point at the screen edge", "Bring it back while hidden"),
                    R("핀 클릭", "펼친 채로 두기 ↔ 자동 숨김",
                      "Click the pin", "Stay open ↔ auto-hide"),
                    R("머리글 오른쪽 끝 ─", "사이드바 치우기 (종료 아님)",
                      "The ─ at the end of the header", "Put the sidebar away - it does not quit"),
                    R("머리글 우클릭", "도움말 · 트레이로 최소화 · 다시 시작 · 종료",
                      "Right-click the header", "Help · Minimise to tray · Restart · Quit"),
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
                    R("아래쪽 칩 줄", "종류별로 걸러 보기", "The chip strip at the bottom", "Show only some kinds of file"),
                    R("칩 줄 → 사용자 지정", "확장자를 직접 적기", "Chips → Custom", "Type the extensions yourself"),
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
                    R("머리글 돋보기", "검색 열기 · 닫기", "The magnifier in the header", "Open · close search"),
                    R("폴더 아이콘", "검색 범위 선택", "The folder icon", "Choose where to look"),
                    R("결과 클릭", "트리에서 그 자리로", "Click a result", "Go to it in the tree"),
                    R("결과 아래 색인 날짜", "그 뒤에 생긴 파일은 다시 훑어야 나옴",
                      "The index date under the results", "Anything newer needs another scan"),
                    R("Esc", "검색 닫기", "Esc", "Close search"),
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
                    R("좌우로 길게 드래그", "이전 · 다음 이미지", "Drag sideways, a good push", "Previous · next image"),
                    R("더블클릭", "맞춤 ↔ 1:1", "Double-click", "Fit ↔ actual size"),
                    R("가운데 클릭 · Enter", "전체 화면", "Middle-click · Enter", "Full screen"),
                    R("Esc", "전체 화면 나가기", "Esc", "Leave full screen"),
                    R("↑ ↓", "이전 · 다음 항목", "↑ ↓", "Previous · next item"),
                    R("← →", "필름스트립이 켜져 있을 때 이전 · 다음",
                      "← →", "Previous · next, while the filmstrip is open"),
                }),

                new Group(T("영상", "Film"), new[]
                {
                    R("Space · 화면 클릭", "재생 · 정지", "Space · click the video", "Play · pause"),
                    R("← →", "5초 뒤 · 앞", "← →", "Back · forward 5 seconds"),
                    R("마우스 앞 · 뒤 버튼", "10초 뒤 · 앞", "Mouse back · forward buttons", "Back · forward 10 seconds"),
                    R("같은 영상 다시 재생", "보던 자리에서 이어보기",
                      "Play the same file again", "It carries on from where you left off"),
                    R("Home", "처음부터", "Home", "Back to the start"),
                    R("P · Insert", "지금 위치 기록 · 해제", "P · Insert", "Mark this position, or take it back"),
                    R("재생 중 ↑ ↓", "안 받음 (일시정지하면 돌아옴)",
                      "↑ ↓ while playing", "Refused - they come back when it is paused"),
                    R("우클릭 → 자막", "켜기 · 크기 · 싱크", "Right-click → Subtitles", "On, size, and sync"),
                    R("전체 화면에서 아래쪽", "재생 막대 꺼내기",
                      "Full screen, point at the bottom", "Bring the transport bar back"),
                }),

                new Group(T("필름스트립(썸네일 바)", "The filmstrip (thumbnail bar)"), new[]
                {
                    R("캐러셀 옆 ▤ 칩", "아래 줄에 폴더의 이미지들", "The ▤ chip beside the counter", "The folder's images as a row"),
                    R("칸을 밖으로 드래그", "다른 앱에 떨어뜨리기", "Drag a cell out", "Drop it into another app"),
                    R("옵션 → 이미지 뷰어", "썸네일 미리 불러오기 · 썸네일 파일 정리",
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
