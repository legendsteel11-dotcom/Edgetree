// What the "업데이트 내역" card above the download buttons shows, newest first.
//
// Kept here rather than fetched from the GitHub release body on purpose: those
// notes run several paragraphs per item in two languages, which is the wrong
// shape for a card someone glances at on the way to the download button. Three
// short lines per version was the shape to aim for; a release that genuinely
// carries a fourth thing worth stopping for can take a fourth line (v1.4.0,
// the user's call). It is a glance, not a list - don't let it grow past that.
//
// FROM v2.1.0 the ceiling is softer, because fixes are named now rather than
// summed up in one line (see that entry for why). A round that fixed five
// things a person could actually have hit gets five lines; a round that fixed
// one gets one. Every line is still a GLANCE - a few words, never a paragraph.
//
// A fix line may name the PROBLEM where that is what makes it recognisable -
// an intermittent one especially, since the person who hit it knows it by the
// symptom and by nothing else. Two judgements go with that. It is written from
// the outside, in what someone SAW, never in the app's internals. And a
// symptom that would frighten a reader who never hit it stays out of the card
// altogether rather than being softened into vagueness: the fix ships either
// way, and this list sits directly above a download button.
//
// One rule when releasing: add an entry here in the same pass that bumps the
// csproj. If this list falls behind, the card notices - the section only shows
// its lines when the newest entry matches the version GitHub reports as latest
// (see UpdateNotes.vue), so a forgotten entry costs a hidden card rather than a
// landing page claiming the wrong thing.
export interface ChangelogEntry {
  version: string
  ko: string[]
  en: string[]
}

export const changelog: ChangelogEntry[] = [
  {
    // FIXES GET NAMED FROM HERE ON (author's call, 2026-08-14). Every entry
    // above closes with "버그 수정 및 안정성 개선", which says a round happened
    // and nothing about it. What changed the mind was the reply thread on the
    // community post: people answered warmly to being told what specifically
    // had been fixed, and the app is long past the launch weeks where a tidy
    // headline mattered more than a record. Naming a fix is not the app
    // talking itself down - it is the one line that shows someone is still
    // holding the thing.
    //
    // Which still leaves the card a GLANCE - one line each, and only for what
    // a person could have hit. Anything invisible from outside stays out and
    // lives in the release notes.
    //
    // THE LAST TWO NAME THE PROBLEM, not the working state, and that is the
    // author's own edit - they wrote both lines. A fix worth listing is worth
    // being recognised by the person who hit it, and "여러 개 선택 후 하나를
    // 해제할 때 간혹 발생하던 전체 해제" is a sentence that finds them where
    // "나머지 선택 유지" does not. Note it is the shape used for the two
    // INTERMITTENT ones: the reliable fix above them still reads as what now
    // works, because nobody needs help recognising a click that never worked.
    //
    // THE AUTO-HIDE LINES CAME OUT, also the author's edit. They were the two
    // that read as "this app can lose itself and you may not find it again" -
    // true of the version being replaced, and directly above a download
    // button. The fix ships; the sentence does not. Same call as v1.2.0's
    // 행 사라짐 - see [[release-notes-tone]] - and worth re-reading before
    // writing any card line about something going missing.
    //
    // The slideshow names WHERE it lives, because it only appears on a picture
    // with others beside it and would otherwise be a feature nobody finds.
    version: 'v2.1.0',
    ko: [
      '이미지 슬라이드 쇼(이미지 우클릭 · F8)',
      '모니터별로 다른 배경화면 지정(해당 모니터 위에서 지정할 때마다)',
      '패널 아래쪽에 반쯤 걸친 항목도 한 번에 클릭',
      '여러 개 선택 후 하나를 해제할 때 간혹 발생하던 전체 해제 문제 수정',
      '즐겨찾기·북마크로 이동한 뒤 부분적으로 해당 항목이 항상 상단으로 오지 않던 문제 수정',
    ],
    en: [
      'Image slideshow (right-click a picture, or F8)',
      'Set a different wallpaper on each monitor (whichever one the sidebar is on)',
      'A row half-clipped at the bottom of the panel now takes one click',
      'Fixed: un-picking one of several selected rows could sometimes clear the whole selection',
      'Fixed: a favorite or bookmark did not always land at the top after the jump',
    ],
  },
  {
    // The author's own four lines, used as written. The first groups what the
    // release notes list one by one - drag to move, Shift+Delete, a folder
    // copied beside itself - under the thing they have in common, which is the
    // gesture someone already knows from Explorer. The second names where to
    // find the new item rather than describing it.
    //
    // 일치시킴, NOT 통합, and the author's own correction: "통합" reads as all of
    // Explorer's features being in here, which is a promise this app does not
    // make and does not want to be measured against. What is true is narrower -
    // the file gestures it already had now behave the way Explorer's do.
    version: 'v2.0.5',
    ko: [
      '앱의 탐색기 기능과 윈도우 탐색기 기능을 일치시킴',
      '네트워크 위치 추가 기능(빈 곳에 우클릭 메뉴)',
      '폴더 내 파일 전체 펼치기 옵션',
      '버그 수정 및 안정성 개선',
    ],
    en: [
      "The app's file operations brought in line with Windows Explorer's",
      'Add a network location (right-click the empty area)',
      'Option to show every file in a folder at once',
      'Bug fixes and stability improvements',
    ],
  },
  {
    version: 'v2.0.4',
    ko: [
      '트리 위치 표시 보완',
      '음악 재생 플레이어 기능 정렬',
    ],
    en: [
      'Tree positioning refinements',
      "The music player's controls tidied up",
    ],
  },
  {
    // One line, and the shortcut is not named. Naming it would tell everyone
    // which gesture to be wary of, right above the download buttons, for a
    // fault that is already gone in the build those buttons hand out.
    version: 'v2.0.3',
    ko: [
      '버그 수정 — 특정 단축키 문제 해결',
    ],
    en: [
      'Bug fix — resolved an issue with a particular keyboard shortcut',
    ],
  },
  {
    // Back to three, which the two entries below both had reasons to exceed.
    // The panel's rename is not one of them: it matters to someone already
    // using the app and looking for it in the options menu, and that person is
    // reading the release notes or the help, not a card on the way to a
    // download button.
    version: 'v2.0.2',
    // Options saving the moment they are clicked is NOT in these lines, and it
    // is the better-known half of this release. Saying it here would tell
    // someone who never lost a setting that settings used to be lost - a line
    // that costs more in doubt than it earns in credit, right above the
    // download buttons. It is in the README's changelog, where the reader has
    // already decided to look.
    ko: [
      '이미지·음악·영상을 더블클릭하면 앱 안에서 바로 열도록 설정할 수 있습니다',
      '현재 재생 중인 곡과 선택한 다른 곡이 구분됩니다',
      '즐겨찾기 전체 해제, 색상 설정 창 정리',
    ],
    en: [
      'Images, music and video can open in the app itself on a double-click',
      'The track that is playing and the other one you have selected are told apart',
      'Clear all favorites, a tidier colour window',
    ],
  },
  {
    // A pointer line first, the way v1.7.1 does it below. 2.0 is the release
    // that says what the app now is, and a patch landing on top of it puts that
    // list one arrow away - so this entry says where it went rather than
    // standing in front of it.
    version: 'v2.0.1',
    ko: [
      'v2.0.0에 이어진 다듬기입니다 — 2.0의 새 기능은 아래 v2.0.0 항목을 봐 주세요',
      '트리에 표시되는 위치 관련 로직을 강화했습니다',
      '드라이브 행에 드라이브 종류에 맞는 아이콘이 표시됩니다',
      '라이트 테마에서 전체화면 재생 컨트롤이 또렷하게 보입니다',
    ],
    en: [
      'Polish on top of v2.0.0 — what 2.0 added is in the v2.0.0 entry below',
      'Stronger logic for where the tree lands and what it puts on screen',
      'Drive rows carry the icon for what kind of drive they are',
      'Full-screen playback controls read clearly in the light theme',
    ],
  },
  {
    // EIGHT lines, past the three the note above asks for, and deliberately:
    // the author's call for this release. 2.0 is the version where the app
    // stopped being a tree, and the card carrying the same list as the release
    // notes was judged worth more here than the glance the shape usually aims
    // for. Read that as an exception earned by the release, not as the rule
    // moving - the next version starts from three again.
    version: 'v2.0.0',
    ko: [
      '경로 직접 입력 및 히스토리 기능(Ctrl+←, Ctrl+→) 추가',
      '이미지 뷰어(썸네일 바 및 내비게이션)',
      '영상 재생(HDR 보정 및 자막 지원)',
      // Breaks itself: the examples are a second thought, not more of the
      // first, and on one line they pushed the entry to two rows anyway. The
      // indent is non-breaking spaces because pre-line collapses ordinary ones
      // (see UpdateNotes.vue).
      '음악 재생(앱 내에서 전역 플레이어로 설정하고 다른 작업으로 이동 가능)\n   예: 이미지 뷰어 또는 파일 관리, 검색 등',
      '사용자가 정한 앱 형태 및 설정 등을 프리셋으로 저장하고 그대로 불러올 수 있는 기능(5개까지)',
      '더 다양해진 랜덤 색상 모드',
      '메모리 관리 및 성능 최적화, 버그 수정, 앱 속도 향상',
      'F1 도움말',
    ],
    en: [
      'Type a path directly, and step back and forward through where you have been (Ctrl+←, Ctrl+→)',
      'Image viewer (thumbnail bar and navigator)',
      'Video playback (HDR correction and subtitles)',
      'Music playback (set it as the app\'s player and carry on elsewhere)\n   e.g. viewing images, managing files, searching',
      'Keep the app\'s shape and settings as presets and bring them back exactly (up to five)',
      'More varied random colour modes',
      'Memory and performance work, bug fixes, a faster app',
      'F1 help',
    ],
  },
  {
    version: 'v1.7.1',
    ko: [
      'v1.7.0에 이어진 안정성 수정입니다 — 새 기능은 아래 v1.7.0 항목을 봐 주세요',
      '사이드바가 펼쳐지는 순간 드물게 앱이 종료될 수 있던 문제를 수정했습니다',
    ],
    en: [
      'A stability follow-up to v1.7.0 — see below for what that release added',
      'Fixed the app closing unexpectedly in rare cases as the sidebar slides open',
    ],
  },
  {
    version: 'v1.7.0',
    ko: [
      '사이드바가 화면 높이를 다 쓰지 않아도 됩니다 — 위/아래 가장자리를 끌어 조정',
      '처음 설치하면 가장자리 가운데의 손잡이로 시작합니다',
      '버그 수정 및 성능 최적화',
    ],
    en: [
      'The sidebar no longer has to fill the screen — drag its top or bottom edge',
      'A fresh install starts with the handle at the middle of the screen edge',
      'Refinements and bug fixes',
    ],
  },
  {
    // The installer is deliberately NOT one of these lines. It is the biggest
    // thing in this release, and the download cards right below already lead
    // with it - a line here would spend a quarter of the card repeating what
    // the reader is about to look at. It belongs in the release notes, which is
    // where someone arriving from the app's update mark lands.
    version: 'v1.6.0',
    ko: [
      '펼칠 때 가장자리 전체 또는 손잡이를 선택하고, 색도 지정할 수 있습니다',
      '파일 종류 필터에 원하는 확장자를 직접 넣을 수 있습니다',
      '버그 수정 및 성능 최적화',
    ],
    en: [
      'Pick what opens it back up — the whole screen edge or just a handle — and give it a colour',
      'Put your own extensions into the file type filter',
      'Refinements and bug fixes',
    ],
  },
  {
    version: 'v1.5.0',
    ko: [
      '색상 피커 — 선택하는 대로 사이드바에 바로 적용',
      '색상만 따로 내보내고 불러오기 — 다른 PC에서도 같은 색으로',
      '북마크 표시를 눌러 바로 해제',
      '파일 종류나 표시 개수를 바꿔도 보던 자리 그대로',
    ],
    en: [
      'A colour picker that applies to the sidebar as you drag it',
      'Export and import the colours on their own — same palette on another PC',
      "Click a bookmark's ribbon to release it",
      'Changing the file filter or the row count keeps your place in the tree',
    ],
  },
  {
    version: 'v1.4.2',
    ko: [
      '하단 바에서 파일 종류를 눌러 걸러 보기 — 코드 · 이미지 · 문서 · 미디어',
      '글꼴 굵기 — 보통 / 굵게 / 폴더만 / 파일만',
      '검색 결과에 커서를 올리면 전체 경로 표시',
      '들여쓰기를 잘못 눌러 폴더가 접히던 문제 수정',
    ],
    en: [
      'Filter by file type from the bottom bar — code, images, documents, media',
      'Font weight — normal, bold, folders only, files only',
      'Hover a search result to see its full path',
      'Fixed folders collapsing when the indent was clicked by mistake',
    ],
  },
  {
    version: 'v1.4.1',
    ko: [
      '왼쪽 패널 표시 옵션 — 북마크 / 즐겨찾기 / 표시 안 함',
      '여러 폴더를 한 번에 선택해서 숨기기',
      '항목이 많은 메뉴를 스크롤해서 볼 수 있음',
      '폴더 복사와 북마크 이동에서 생기던 문제 수정',
    ],
    en: [
      'Side panel option — bookmarks, favorites, or hidden',
      'Hide several folders in one go',
      'Long menus scroll instead of running off the screen',
      'Fixed issues in folder copy and bookmark jumps',
    ],
  },
  {
    version: 'v1.4.0',
    ko: [
      '안 쓰는 폴더·드라이브를 트리에서 숨기기',
      '색상 설정에 색상 코드(#RRGGBB) 직접 입력',
      '즐겨찾기·북마크·검색 결과로 이동하면 대상이 맨 위로',
      '트리 행에 커서를 올리면 전체 경로 표시',
    ],
    en: [
      'Hide folders and drives you never use',
      'Type a colour code (#RRGGBB) in the colour settings',
      'Favorites, bookmarks and search results land at the top',
      'Hover a tree row to see its full path',
    ],
  },
  {
    version: 'v1.3.5',
    ko: [
      '잘라내기 추가 — Ctrl+X로 옮기기, 탐색기와 함께 사용 가능',
      '북마크를 우클릭 메뉴에서 지정·이동',
      '검색 목록이 최신이 아닐 때 알려줌',
    ],
    en: [
      'Cut added — move with Ctrl+X, works with Explorer',
      'Bookmarks set and browsed from the right-click menu',
      'The search list says when it is out of date',
    ],
  },
  {
    version: 'v1.3.3',
    ko: [
      '북마크 목록을 옵션 메뉴에서 한눈에',
      '검색 정렬을 메뉴에서 선택',
      '색상 설정·앱 정보 창이 글꼴 크기를 따라감',
    ],
    en: [
      'Every bookmark listed in the options menu',
      'Search sorting picked from a menu',
      'Color Settings and About follow the font size',
    ],
  },
  {
    version: 'v1.3.2',
    ko: [
      '네트워크 드라이브(NAS 등)가 잠들어 있어도 멈추지 않음',
      '즐겨찾기 드래그로 순서 바꾸기',
      '정렬 기준에 유형·크기 추가',
    ],
    en: [
      'A sleeping network drive no longer stalls the tree',
      'Favorites reordered by dragging',
      'Sort by type and size',
    ],
  },
]
