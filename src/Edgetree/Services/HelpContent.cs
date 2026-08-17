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
// it does - 우클릭 → 북마크 doing "keep it in the side panel" - the row is
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
        T("고정/자동 숨김 선택 및 크기(높이) 조정", "Pin it or let it auto-hide, and set the size"),
        T("안 쓰는 폴더 감추기", "Hide the folders you never open"),
        // Which of the two wins is the part nobody guesses, and it is the part
        // that makes the pair usable at all.
        T("자주 쓰는 파일 종류(확장자) 지정하기 (제외 확장자 입력시 최우선 적용)",
          "Set the file kinds you work with - anything you type as excluded wins over the rest"),
        T("자주 가는 곳은 북마크로", "Bookmark the places you keep going back to"),
        T("썸네일 캐싱 켜고 이미지 많은 폴더 빠르게 관리",
          "Turn thumbnail caching on and move through big image folders fast"),
        T("원하는 색상 선택", "Pick the colours you want"),
        T("설정한 상태를 프리셋으로 저장 (제목 표시줄 우클릭 메뉴)",
          "Save a setup as a preset (right-click the title bar)"),
    };

    public static IReadOnlyList<Section> Build() => new[]
    {
        new Section(
            // "사이드바"였다가 2026-08-16에 바꿈 - 공식 명칭이 아님. 바로 다음
            // 절이 창 모드이므로, 이 절은 그 반대편(도킹된 기본 모습)이 아니라
            // 앱 자체를 다루는 자리로 읽히는 것이 맞음 - 트레이·핀·너비·높이가
            // 다 여기 있음.
            T("앱", "The app"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("가장자리 혹은 손잡이에 커서 이동", "다시 펼치기",
                      "Point at the screen edge, or at the handle", "Bring it back"),
                    // THE ROW SOMEONE LOOKS UP IN A PANIC, added 2026-08-14
                    // after a report of exactly that: two monitors, the sidebar
                    // auto-hidden onto the edge they share, and a handle a
                    // cursor crosses to the next display instead of resting on.
                    // The help had one way back and it was the one that had
                    // stopped working for them. Both of these open the sidebar
                    // fully, not to the handle, which is why the row says 펼치기
                    // rather than 표시.
                    R("트레이 아이콘 클릭 · 프로그램 다시 실행", "숨은 앱 다시 펼치기",
                      "Click the tray icon, or run the app again", "Opens a hidden app back up"),
                    R("핀 클릭", "고정하기 ↔ 자동 숨김",
                      "Click the pin", "Stay open ↔ auto-hide"),
                    // Three answers under one button, so the row is split rather
                    // than made to carry an if-clause: what it does depends on
                    // the tray option AND on which mode the window is in, and a
                    // single sentence covering both read as a paragraph.
                    R("제목 표시줄 최소화 아이콘", "트레이 옵션과 모드에 따라 다름",
                      "The minimise icon in the title bar", "Depends on the tray option and the mode"),
                    // Indented AND bulleted: the indent alone left them reading
                    // as three ordinary rows that happened to start further in,
                    // and the dash is what says they belong to the row above.
                    R("  - 트레이 옵션 켜짐", "트레이로 최소화",
                      "  - Tray option on", "Minimise to the tray"),
                    // NOT 앱 here, unlike the rows around it: this half of the
                    // row is a CONDITION, and the condition is which mode the
                    // window is in - the English side has said 도킹 all along
                    // and only the Korean was calling it 사이드바.
                    R("  - 트레이 꺼짐 · 도킹 상태", "숨김",
                      "  - Tray off, docked", "Hide it"),
                    R("  - 트레이 꺼짐 · 창 모드", "작업 표시줄로 최소화",
                      "  - Tray off, window mode", "Minimise to the taskbar"),
                    R("제목 표시줄 우클릭", "도움말 · 트레이로 최소화 · 다시 시작 · 종료",
                      "Right-click the title bar", "Help · Minimise to tray · Restart · Quit"),
                    R("가장자리 드래그", "너비 조절",
                      "Drag the outer edge", "Resize"),
                    R("가장자리 더블클릭", "내용에 맞춰 너비 맞춤",
                      "Double-click the outer edge", "Fit the width to the contents"),
                    R("위 · 아래 가장자리 드래그", "높이와 위치 (짧은 밴드)",
                      "Drag the top or bottom edge", "Height, and where the band sits"),
                    R("그 가장자리 더블클릭", "화면 높이 전체로 복원",
                      "Double-click that edge", "Back to the full height"),
                    R("드래그 중 Shift · Ctrl", "격자에 맞춰 스냅 (Ctrl이 더 작은 격자)",
                      "Shift · Ctrl while dragging", "Snap to a grid - Ctrl uses a smaller one"),
                }),
            }),

        new Section(
            T("창 모드", "Window mode"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("제목 표시줄 드래그", "창으로 분리 · 이동",
                      "Drag the title bar", "Pull it off, and move it"),
                    R("제목 표시줄 더블클릭", "최대화 · 복원",
                      "Double-click the title bar", "Maximise · restore"),
                    R("가장자리 · 모서리 드래그", "크기 조절",
                      "Drag an edge or corner", "Resize"),
                    R("가장자리로 드래그", "다시 도킹",
                      "Drag to a screen edge", "Dock it again"),
                    R("핀 클릭", "도킹 상태로 복귀",
                      "Click the pin", "Go back to being docked"),
                }),
            }),

        // A SECTION OF ITS OWN since 2026-08-16, having been a group inside
        // 창 모드 - put there because what a preset holds is mostly the window
        // and one group seemed enough for the colours and the filter riding
        // along.
        //
        // It has outgrown that twice over. What a preset holds now reaches the
        // whole palette, the panel's own switches and the folder it was saved
        // in, none of which is a window mode; and it has keys, which is what
        // a reader scans these headings for. A subject nested under another
        // one is a subject a reader has to already know the location of.
        new Section(
            T("프리셋", "Presets"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("제목 표시줄 우클릭", "저장된 프리셋 목록 · 프리셋 추가 (최대 5개)",
                      "Right-click the title bar", "The setups you saved, and 프리셋 추가 - up to five"),
                    // The list this row used to carry (위치 · 크기 · 도킹 · 자동
                    // 숨김 · 색상 · 파일 종류 · 현재 폴더) came out on the author's
                    // call (2026-08-16): seven items read as seven things to
                    // check, when the answer a reader wants is that they do not
                    // have to check any of them. It also went stale twice as
                    // AppPreset.Fields grew, which a sentence cannot.
                    R("저장 항목", "현재 앱 설정 모두를 그대로 저장",
                      "What it holds", "Everything you have set up, exactly as it stands"),
                    R("이름 옆 ›", "적용 · 덮어쓰기 · 이름 바꾸기 · 삭제",
                      "The › beside a name", "Apply · overwrite · rename · delete"),
                    // The two keys, which is what this section is for - the rows
                    // above name a menu, and these are the reason not to open it.
                    R("Ctrl+1 ~ 5", "그 번호의 프리셋으로 전환 (없으면 그 자리에 저장할지 물음)",
                      "Ctrl+1 ~ 5",
                      "Go to that preset - and if there is none yet, offers to save this setup there"),
                    R("Ctrl+Shift+S", "현재 선택된 프리셋에 확인 없이 바로 덮어씌움",
                      "Ctrl+Shift+S",
                      "Overwrites the preset you are in, straight away, without asking"),
                }),
            }),

        new Section(
            T("트리", "The tree"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("↑ ↓", "위 · 아래 행 이동", "↑ ↓", "Previous · next row"),
                    R("← →", "접기 · 펼치기", "← →", "Collapse · expand"),
                    // 2026-08-17: 열린 폴더를 눌러도 안 접히게 바뀌었으므로 이
                    // 두 줄이 필요해졌다. 접는 방법이 둘(다시 누르기 · 꺽쇠)이고
                    // 어느 쪽도 화면에 안 적혀 있다.
                    R("폴더 클릭", "접힌 폴더는 펼치기 · 열린 폴더는 선택만, 다시 누르면 접기",
                      "Clicking a folder",
                      "A closed folder opens; an open one is only selected - press it again to close"),
                    R("꺽쇠 클릭", "누를 때마다 바로 접기 · 펼치기",
                      "Clicking the chevron", "Opens and closes on every press"),
                    // Named for the places rather than for the rows: it moves
                    // between folders you have been in, not between every row
                    // you clicked, and someone who expects the second one will
                    // read the first press as a miss.
                    R("Ctrl+← · Ctrl+→", "사용한 폴더로 뒤로 · 앞으로 (경로 표시줄의 < > 와 같음)",
                      "Ctrl+← · Ctrl+→",
                      "Back · forward through the folders you have used - the same as the path bar's < >"),
                    R("경로 표시줄의 목록 버튼", "사용한 폴더들을 확인하고 바로 이동",
                      "The list button on the path bar",
                      "See the folders you have used, and go straight to one"),
                    R("Ctrl + 휠", "빠른 스크롤", "Ctrl + wheel", "Scroll about five times faster"),
                    R("Ctrl + / −", "글자 크기 조절", "Ctrl + / −", "Text size"),
                    R("Enter", "열기", "Enter", "Open"),
                    R("F2", "이름 바꾸기", "F2", "Rename"),
                    R("F5", "새로고침", "F5", "Refresh"),
                    R("F7", "새 폴더", "F7", "New folder"),
                    R("Del", "휴지통으로 삭제", "Del", "Delete to the Recycle Bin"),
                    R("Shift+Del", "완전 삭제", "Shift+Del", "Delete permanently"),
                    // The one place a delete still asks, so it is the one place
                    // the help has to say why.
                    R("네트워크 위치에서 삭제", "휴지통이 없어 확인 후 완전 삭제",
                      "Deleting from a network location", "There is no Recycle Bin there, so it asks first"),
                    R("Ctrl+C · Ctrl+X · Ctrl+V", "복사 · 잘라내기 · 붙여넣기",
                      "Ctrl+C · Ctrl+X · Ctrl+V", "Copy · cut · paste"),
                    R("Ctrl+Shift+C", "경로 복사", "Ctrl+Shift+C", "Copy path"),
                    // Said as COPY since 2026-08-13. It always was one - the row
                    // had claimed 이동 since the drag shipped, which is the kind
                    // of line that sends someone looking for a feature that is
                    // there and a behaviour that is not.
                    R("드래그", "같은 드라이브는 이동, 다른 드라이브는 복사",
                      "Drag", "Move within a drive, copy between drives"),
                    R("다른 탐색기로 드래그", "복사", "Drag to another explorer", "Copy"),
                    // The KEY column carries the timing, because that is the
                    // part that gets missed: Shift+클릭 is already 범위 선택, so
                    // holding it first selects instead of dragging.
                    R("드래그 중 Shift · Ctrl", "이동 · 복사 고정",
                      "Shift · Ctrl while dragging", "Force a move · force a copy"),
                    R("Ctrl+Alt+K", "북마크 표시 · 해제", "Ctrl+Alt+K", "Bookmark, or take it back"),
                    R("Ctrl+Alt+L · J", "다음 · 이전 북마크로 이동", "Ctrl+Alt+L · J", "Next · previous bookmark"),
                    // 번호가 화면에 있으니 "순서가 바뀐다"는 라벨이 말해 준다.
                    // 여기 적을 값어치가 있는 것은 그 순서가 단축키의 순서이기도
                    // 하다는 것 - 목록만 보면 알 길이 없다.
                    R("북마크 패널에서 행 드래그", "순서 바꾸기 · 번호가 곧 Ctrl+Alt+L 순서",
                      "Drag a row in the bookmark panel",
                      "Reorders it - the numbers are the Ctrl+Alt+L order"),
                    // The parenthesis is the part a menu label cannot say, and
                    // it is what a whole paragraph used to say instead.
                    R("우클릭 → 폴더 숨기기", "트리에서 폴더를 감춤, 검색에서는 보이나 작업 시 폴더 숨김 해제 필요",
                      "Right-click → Hide this folder",
                      "Hidden from the tree; search still finds it, but working on it means unhiding it first"),
                    // THE MENU WITH NO ROW UNDER IT. Everything else in this
                    // section acts on the row that was clicked, so a menu that
                    // needs the absence of one is the gesture nobody arrives at
                    // by accident - and the four lists behind it are reachable
                    // no other way without opening the options menu.
                    //
                    // Listed rather than summarised, because the list IS the
                    // reason to go there. 새로고침 is called 전체 here for the
                    // one thing this menu's version does that the row's does
                    // not: the row refreshes that folder, this one the drive
                    // list and every folder open in the tree.
                    R("빈 곳 우클릭",
                      "전체 새로고침 · 새 폴더 · 북마크 · 숨긴 폴더 · 네트워크 위치 · 표시할 파일 종류",
                      "Right-click empty space",
                      "Refresh everything · New Folder · Bookmark · Hidden Folders · Network Locations · Show File Types"),
                }),
            }),

        new Section(
            T("필터와 정렬", "Filtering and sorting"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("아래쪽 필터 버튼 줄", "종류별 필터링",
                      "The filter buttons at the bottom", "Show only some kinds of file"),
                    R("필터 버튼 → 사용자 지정", "확장자 직접 입력 ( *. 없이 쉼표로 구분해서 여러 개 입력)",
                      "A filter button → Custom",
                      "Type the extensions yourself - no *. , and comma-separated for several"),
                    // The same list twice, and it earns the second line: this
                    // section is where someone hunting the filter looks, and
                    // they will not scroll up to 트리 to find out that the
                    // gesture exists. The row there answers "what is that menu";
                    // this one answers "where else can I set this".
                    R("트리 빈 곳 우클릭 → 표시할 파일 종류", "필터 버튼 줄과 같은 목록",
                      "Right-click empty tree space → Show File Types",
                      "The same list the filter buttons carry"),
                    R("폴더 행의 정렬 아이콘", "그 폴더만 개별 정렬", "The sort icon on a folder row", "Sort that one folder its own way"),
                    R("옵션 → 정렬 기준", "전체 기본 정렬", "Options → Sort by", "The default for everything"),
                    // 접기 belongs on the same row as 더 보기, and the selection
                    // is the point of the row rather than a side effect: a long
                    // list puts the 더 보기 row at the bottom of the screen with
                    // its folder scrolled off, so two identically named folders
                    // are told apart by which guide line lights up.
                    R("더 보기 · 접기", "나머지 항목 펼치기 · 되접기 (그 부모 폴더가 선택됨)",
                      "Show more · Show less", "Reveal the rest, or fold it back - its parent folder gets selected"),
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
                    R("결과 클릭 · ↑ ↓ (멀티미디어 패널 열림)", "검색 결과 간 이동",
                      "Click a result · ↑ ↓", "(with the multimedia panel open) Move through the results"),
                    R("결과 더블클릭 (멀티미디어 패널 열림)", "트리의 해당 항목으로 이동",
                      "Double-click a result", "(with the multimedia panel open) Go to it in the tree"),
                    R("결과 표시 수", "기본 1,000개 · 목록 끝 더 보기로 1,000개씩 추가",
                      "How many results show", "1,000 at a time - \"Show more\" at the end adds another 1,000"),
                    // The dot is the half a label cannot say: the button names
                    // the act, nothing names the reason to press it.
                    R("상태줄 오른쪽 ↻", "다시 인덱싱 — 변경된 내용이 있을 경우 파란 점 표시",
                      "The ↻ on the status line", "Reindex - a blue dot means the folder has changed"),
                    R("Esc · Ctrl+E", "검색 닫기", "Esc · Ctrl+E", "Close search"),
                }),
            }),

        new Section(
            // "이미지 뷰어" until 2026-08-11, when the panel stopped being one:
            // it plays film and sound now, and two of the four groups under
            // this heading are about things that are not images. This heading
            // went first and the rest of the app followed on 2026-08-12 - the
            // panel is called 멀티미디어 패널 everywhere now, so the heading names
            // it outright rather than naming the subject it covers.
            T("멀티미디어 패널", "Multimedia Panel"),
            new[]
            {
                // HOW THE PANEL OPENS AT ALL, which this section did not say
                // (2026-08-16). Every group under it described what to do once
                // it is open, and 검색 has had the equivalent row - 제목 표시줄
                // 돋보기 - since it shipped. The two options below it are here
                // rather than in 모양과 설정 because they are ways IN, and
                // someone reading this heading is looking for exactly that.
                new Group(string.Empty, new[]
                {
                    R("제목 표시줄 이미지 아이콘", "멀티미디어 패널 열기 · 닫기",
                      "The picture icon in the title bar", "Open · close the multimedia panel"),
                    R("옵션 → 멀티미디어 패널 → 더블클릭으로 열기",
                      "트리에서 더블클릭한 파일을 기본 프로그램 대신 패널에서 엶",
                      "Options → Multimedia panel → Open on double-click",
                      "A double-click in the tree opens the file here instead of in your usual program"),
                    // The condition is the row, and it is the author's own
                    // wording: what it does for a folder is nothing, which
                    // reads as the option being broken unless it is said.
                    R("옵션 → 멀티미디어 패널 → 자동 펼치기",
                      "미디어 파일을 선택할 경우에만 패널이 펼쳐짐 (폴더는 동작하지 않음)",
                      "Options → Multimedia panel → Expand on selection",
                      "Expands the panel when a media file is selected - folders do nothing"),
                    // The cover takes the header away, so this says where its
                    // menu went. Its companion - the line that names the file
                    // at the foot on a mouse move - had a row here too and it
                    // came out (2026-08-16): the line shows ITSELF the moment
                    // the cover goes up, so a row telling someone to move the
                    // mouse explains a thing that has already explained itself.
                    // Its gesture also ran long enough to wrap the column.
                    //
                    // What the row lists came back once (2026-08-16). Folding
                    // away everything but the show left a picture with no way
                    // to reach its own size, navigator or thumbnail bar, since
                    // the chip row those live on went up with the header. Only
                    // the FILE items fold now, so the row names both halves.
                    R("전체 화면에서 우클릭", "파일 항목 대신 그림 크기 · 내비게이터 · 썸네일 바와 제목 표시줄 메뉴 (프리셋 · 도움말 · 다시 시작 · 종료)",
                      "Right-click in full screen",
                      "Picture size, navigator and thumbnail bar instead of the file items, plus the title bar's menu - presets, help, restart, quit"),
                    // 2026-08-17: 창 모드에서만 나오는 줄이라 조건을 앞에 적었다 -
                    // 부착 상태에서 찾다가 없다고 읽으면 그게 더 나쁘다.
                    R("창 모드 · 전체 화면에서 우클릭 → 바탕화면 전체",
                      "끄면 창 크기 그대로 그림만 꽉 채움 (기본은 창을 화면 전체로)",
                      "Window mode · right-click in full screen → Fill the desktop",
                      "Off keeps the window's own size and fills that instead; on (the default) grows the window to the screen"),
                    // 이미 되는 것을 적는 줄. 헤더가 사라져도 그 자리는 제목
                    // 표시줄이라 창이 끌리는데, 화면에 아무 표시가 없어서 아는
                    // 방법이 없었음 (2026-08-17, 창 크기를 유지하는 전체화면이
                    // 생기면서 실제로 필요해짐). 대가를 같은 줄에 적은 이유는 그것이
                    // "위에서는 휠이 안 먹는다"의 답이기도 하기 때문.
                    R("창 모드 · 전체 화면에서 위쪽 띠 드래그", "창 이동 — 그 띠는 제목 표시줄이라 그림에는 닿지 않음",
                      "Window mode · drag the top strip in full screen",
                      "Moves the window - that strip is still the title bar, so it does not reach the picture"),
                }),

                new Group(T("이미지", "Images"), new[]
                {
                    R("휠", "확대 · 축소", "Wheel", "Zoom in · out"),
                    R("Ctrl+휠 · Shift+휠", "정밀 확대 축소 - Shift 조합시 더 세밀하게",
                      "Ctrl+wheel · Shift+wheel", "Precision zoom - finer with Shift"),
                    // 영상·음악에는 "재생 형식" 줄이 있는데 그림에는 없었다
                    // (2026-08-16). 목록이 아니라 규칙으로 적는 것은 실제로
                    // 규칙이기 때문 - 이 앱은 그림을 디코드하지 않고 전부 셸에
                    // 넘기므로, 볼 수 있는 것과 탐색기가 미리보기를 만드는 것이
                    // 정확히 같은 집합이다. 바로 아래 SVG 줄이 그 규칙의 뒷면
                    // (PC마다 다른 이유)을 이미 말하고 있어 둘이 짝이 된다.
                    R("표시 형식", "Windows가 미리보기를 만들 수 있는 그림 (PSD · RAW · JXL 등 포함)",
                      "Formats it shows",
                      "Any picture Windows can make a thumbnail of - PSD, RAW, JXL and the rest"),
                    // SVG has no decoder in Windows at all, so this one is the
                    // shell's answer or nothing - which is why the same file
                    // shows on one PC and not another. Said here rather than
                    // left as a mystery; the app cannot promise it either way.
                    R("SVG", "Windows에 렌더링 기능 설치돼 있을 때만 표시",
                      "SVG", "Shows only where Windows has something installed that can draw it"),
                    R("드래그", "확대 상태에서 화면 이동", "Drag", "Move around, when zoomed in"),
                    // What the plate IS, which nothing on screen says - it
                    // appears on its own once there is more picture than panel,
                    // and a small dark square in a corner explains nothing by
                    // itself.
                    R("내비게이터", "확대 시 우측 하단에 표시",
                      "The navigator", "Bottom right, once zoomed in"),
                    R("좌우로 길게 드래그", "이전 · 다음 이미지", "Drag sideways, a good push", "Previous · next image"),
                    R("더블클릭", "맞춤 ↔ 1:1 전환", "Double-click", "Fit ↔ actual size"),
                    // Says what it gives up, because that is the whole choice
                    // between it and 맞춤: one shows all of the picture, the
                    // other fills the panel.
                    R("자름맞춤", "패널을 꽉 채우고 넘치는 부분은 자름 (다음 그림에도 이어짐)",
                      "Fill", "Fills the panel and crops the overflow - and stays on for the next picture"),
                    R("가운데 클릭 · Enter", "전체 화면", "Middle-click · Enter", "Full screen"),
                    R("Esc", "전체 화면 종료", "Esc", "Leave full screen"),
                    R("↑ ↓", "이전 · 다음 항목", "↑ ↓", "Previous · next item"),
                    R("← →", "썸네일 바 켜져 있을 때 이전 · 다음",
                      "← →", "Previous · next, while the thumbnail bar is open"),
                    // Where someone looks for it: the menu row only appears
                    // over a picture with others beside it, so a folder of one
                    // photo gives no clue the feature exists at all.
                    R("F8 · 우클릭 → 슬라이드 쇼", "폴더의 이미지를 차례로 표시 (2장 이상일 때)",
                      "F8 · Right-click → Slideshow", "Turn the folder's images over, one by one (needs two or more)"),
                    R("슬라이드 쇼 중 클릭 · ↑ ↓ · 트리 선택", "쇼 종료 — 보고 있던 사진에서 멈춤",
                      "Click, ↑ ↓, or pick a row while it runs", "Ends the show, staying on the picture you were looking at"),
                    R("F9 · ⋯ → 멀티미디어 패널 → 시계와 날짜", "패널 위에 시각·날짜·요일을 표시",
                      "F9 · ⋯ → Multimedia panel → Clock and date", "Puts the time, date and day over the panel"),
                    // The size is a SHARE of the panel, not a point size, and
                    // the row says so - otherwise a percentage reads as a fixed
                    // size and widening the window looks like it lost the setting.
                    R("⋯ → 멀티미디어 패널 → 시계 크기", "50% ~ 150% (패널 크기에 대한 비율)",
                      "⋯ → Multimedia panel → Clock size", "50% to 150%, as a share of the panel"),
                    // The wallpaper item had NO row at all until now - it has
                    // been in the picture's menu for weeks. Worth a line on its
                    // own account, and more so since 2.1.0: which monitor it
                    // lands on is a rule someone has to be told, because
                    // nothing on screen says the sidebar's own display is the
                    // one being set.
                    R("우클릭 → 배경 설정", "앱이 위치한 모니터의 배경화면으로 지정",
                      "Right-click → Set as wallpaper", "Sets it on the monitor the app is on"),
                }),

                new Group(T("영상", "Film"), new[]
                {
                    R("Space · 화면 클릭", "재생 · 정지", "Space · click the video", "Play · pause"),
                    R("← →", "5초 뒤 · 앞", "← →", "Back · forward 5 seconds"),
                    R("마우스 앞 · 뒤 버튼", "10초 뒤 · 앞", "Mouse back · forward buttons", "Back · forward 10 seconds"),
                    R("같은 영상 재생 시", "마지막 위치부터 이어보기",
                      "Play the same file again", "It carries on from where you left off"),
                    R("Home", "처음부터", "Home", "Back to the start"),
                    R("P · Insert", "영상 북마크 표시 · 해제",
                      "P · Insert", "Bookmark this moment, or take it back"),
                    R("우클릭 → 영상 북마크 목록", "표시 위치로 이동 · 전체 삭제",
                      "Right-click → Video Bookmarks", "Jump to one, or clear them all"),
                    R("재생 중 ↑ ↓", "볼륨 조절 (일시정지하면 항목 이동으로 복귀)",
                      "↑ ↓ while playing", "Volume - they walk the folder again once it is paused"),
                    R("재생 중 M", "음소거", "M while playing", "Mute"),
                    // The film's own 자름맞춤, which the picture's chip row has
                    // had all along. Says it does NOT carry over, because the
                    // picture's does and someone who met it there will expect
                    // the same.
                    R("우클릭 → 영상 크기 → 자름맞춤", "패널을 꽉 채우고 넘치는 부분은 자름 (다음 영상은 맞춤으로 시작)",
                      "Right-click → Video size → Fill",
                      "Fills the panel and crops the overflow - the next film starts fitted again"),
                    R("우클릭 → 자막", "켜기 · 크기 · 싱크 조절", "Right-click → Subtitles", "On, size, and sync"),
                    // 컨트롤 패널, not 재생 막대 (2026-08-16). The strip is what
                    // a reader calls it, and the app had two names for it in
                    // its own text - this row and the music group below. The
                    // English stays "playback controls" rather than a literal
                    // control panel, which is Windows' own thing.
                    R("전체 화면에서 아래쪽", "컨트롤 패널 표시",
                      "Full screen, point at the bottom", "Bring the playback controls back"),
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
                    R("재생 형식",
                      "mp4 · m4v · mov · avi · wmv · mkv · webm · mpg · mpeg · ts · m2ts · mts · flv · 3gp · asf · wm · qt · 3g2 · m2t",
                      "Formats it plays",
                      "mp4 · m4v · mov · avi · wmv · mkv · webm · mpg · mpeg · ts · m2ts · mts · flv · 3gp · asf · wm · qt · 3g2 · m2t"),
                    R("코덱", "외부 코덱 사용 (필요 시 직접 설치)",
                      "Codecs", "Whatever is installed on the PC - add one if a file needs it"),
                    // AV1 by name, because the row above cannot be searched for.
                    // Named as OBSERVED rather than as a rule (video.log,
                    // 2026-08-11: two attempts, no `opened` event at all,
                    // hr=0xC00D11B1) - the codec story is per-machine, so what
                    // is honest is "does not open here", not "never plays".
                    R("AV1", "현재 미지원 (코덱에 따라 다를 수 있음)",
                      "AV1", "Not supported at present - may differ with the codecs installed"),
                    // "트리에서" is not padding: Enter INSIDE the panel is full
                    // screen (see the images group above), and this row sits in
                    // the viewer's own section.
                    R("재생 오류 시", "기본 프로그램으로 열기 권장 (트리에서 더블클릭 · Enter)",
                      "When it will not play", "Better opened in your usual player (double-click or Enter, in the tree)"),
                }),

                // Its own group under 영상 rather than rows inside it: the
                // transport is the same strip, but everything that is
                // DIFFERENT about sound is different because there is no
                // picture - it keeps playing when you look elsewhere, and it
                // does not remember where you stopped.
                new Group(T("음악", "Music"), new[]
                {
                    R("재생 형식", "mp3 · wav · flac · m4a · m4b · aac · wma",
                      "Formats it plays", "mp3 · wav · flac · m4a · m4b · aac · wma"),
                    R("앨범아트 클릭", "재생 · 정지", "Click the album art", "Play · pause"),
                    // 2026-08-17: 앨범아트도 그림과 같은 조절 줄을 받게 되면서
                    // 필요해진 줄. 앞의 줄이 "클릭은 재생"이라고 말하므로 크기를
                    // 어디서 바꾸는지는 바로 옆에 있는 것이 맞다.
                    R("앨범아트 크기", "그림과 같은 줄에서 맞춤 · 1:1 · 자름맞춤 · 확대 축소",
                      "Album art size",
                      "The same row a picture gets - fit, 1:1, crop to fill, and the zoom stepper"),
                    R("컨트롤 패널 왼쪽 두 칩", "백그라운드 재생 · 이어서 재생",
                      "The two chips at the left of the playback controls", "Background play · keep playing"),
                    R("백그라운드 재생", "다른 폴더 이동해도 계속 (끄면 정지 · 파일 해제)",
                      "Background play", "Carries on in other folders - switching it off stops it and frees the file"),
                    R("이어서 재생 우클릭", "폴더 반복 · 한 곡 반복 · 셔플 반복",
                      "Right-click the keep-playing chip", "Repeat folder · repeat one · repeat shuffled"),
                    R("음악 폴더 선택", "▶ 폴더 전체 재생 · 셔플 반복",
                      "Select a folder of music", "▶ play the folder, or repeat it shuffled"),
                    // The condition is the row's reason for existing: without
                    // it, right-clicking a folder that has never been opened
                    // shows nothing and reads as the feature being missing.
                    R("폴더 우클릭 → 재생", "한 번이라도 펼쳐 본 폴더에 나타남",
                      "Right-click a folder → Play", "Appears for folders you have opened at least once"),
                    R("재생 위치", "저장 안 됨 (이어보기는 영상만 지원)",
                      "Where you stopped", "Not remembered - only film carries on from where it was"),
                }),

                new Group(T("썸네일 바", "The thumbnail bar"), new[]
                {
                    R("장수 표시 옆 ▤ 버튼", "아래에 해당 폴더 이미지 목록 표시 — 검색 중이면 검색 결과",
                      "The ▤ button beside the counter", "The folder's images as a row - or the results, while searching"),
                    R("칸을 밖으로 드래그", "다른 앱으로 드롭", "Drag a cell out", "Drop it into another app"),
                    R("바 위쪽 가장자리 드래그", "칸 크기 조절", "Drag the bar's top edge", "Cell size"),
                    R("옵션 → 멀티미디어 패널", "이미지 썸네일 캐싱 · 캐싱 파일 정리",
                      "Options → Multimedia panel", "Preload thumbnails · clean the cache up"),
                }),
            }),

        new Section(
            T("모양과 설정", "Appearance and settings"),
            new[]
            {
                new Group(string.Empty, new[]
                {
                    R("옵션 → 색상 설정", "직접 선택 · 랜덤 지정", "Options → Colours", "Pick them, or roll the dice"),
                    // THE THREE THINGS IN THAT WINDOW A LABEL CANNOT TEACH
                    // (2026-08-16). The chain is the clearest case this file's
                    // own rule has: a small link mark sits on all seventeen
                    // rows, so it is always on screen and means nothing until
                    // someone is told - including the half that catches people
                    // out, that BOTH ends have to be lit.
                    //
                    // 그림자 is here for WHERE it is. It is a behaviour toggle
                    // living in the colour window on purpose (among the
                    // behaviour toggles the word reads as a shadow around the
                    // whole app), and the cost of that choice is that anyone
                    // hunting the options menu for it never finds it.
                    R("색상 설정 → 행의 고리", "색을 묶어 함께 변경 — 함께 켠 줄끼리 같은 색",
                      "Colours → the link on a row", "Ties rows together - the ones lit together share a colour"),
                    R("색상 설정 → 모노", "팔레트 전체를 회색톤으로 한 번에",
                      "Colours → Mono", "The whole palette to greyscale in one press"),
                    R("색상 설정 → 그림자", "트리와 북마크 패널의 위아래 끝을 옅게 덮음",
                      "Colours → Shading", "Veils the top and bottom ends of the tree and the bookmark panel"),
                    R("옵션 → 기본 설정", "자동 시작 · 트레이 · 아이콘 · 자동 숨김", "Options → General", "Autostart · tray · icons · auto-hide"),
                    // "아이콘" in the row above reads as ONE switch, and it is
                    // three. The drive one arrived last (2026-08-16) and the
                    // release notes say it is separate from the folder's, so the
                    // help has to say the same - a reader who took the row above
                    // at its word would go looking for a switch that is already
                    // there under a name it does not use.
                    R("옵션 → 기본 설정 → 아이콘", "폴더 · 파일 · 드라이브를 따로따로 켜고 끔",
                      "Options → General → Icons", "Folders, files and drives switch on and off separately"),
                    R("옵션 → 기본 설정 → 드래그로 이동", "끄면 드래그는 항상 복사, 이동은 Shift",
                      "Options → General → Drag Moves", "Off, a drag always copies and Shift is the way to move"),
                    // The cost goes on the row, not in a paragraph: it is the
                    // one setting here that can make the tree slower.
                    R("옵션 → 한 번에 표시할 개수 → 전체 표시", "더 보기 없이 전부 — 큰 폴더에서는 느려짐",
                      "Options → Items per Folder → Show All", "No 더 보기 row at all - a big folder will feel it"),
                    R("옵션 → 네트워크 위치", "드라이브 문자 없는 공유 추가 (\\\\서버\\공유)",
                      "Options → Network Locations", "Add a share with no drive letter (\\\\server\\share)"),
                }),
            }),
    };
}
