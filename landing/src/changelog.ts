// What the "업데이트 내역" card above the download buttons shows, newest first.
//
// Kept here rather than fetched from the GitHub release body on purpose: those
// notes run several paragraphs per item in two languages, which is the wrong
// shape for a card someone glances at on the way to the download button. Three
// short lines per version is the shape to aim for; a release that genuinely
// carries a fourth thing worth stopping for can take a fourth line (v1.4.0,
// the user's call). It is a glance, not a list - don't let it grow past that.
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
    // Four lines, which the note above allows for a release that earns it: the
    // viewer is what this version is, and the other three are each a different
    // reason someone would want it.
    version: 'v2.0.0',
    ko: [
      '이미지 뷰어가 사이드바 안에 들어왔습니다 — 사진·영상·음악을 그 자리에서',
      '필름스트립으로 폴더를 훑고, 클릭 한 번으로 그 파일 자리에 섭니다',
      '지나온 자리를 되짚는 트리 히스토리 (Ctrl+←, Ctrl+→)',
      '자주 쓰는 배치를 프리셋 다섯 개로 저장해 두고 바꿔 씁니다',
    ],
    en: [
      'An image viewer now lives inside the sidebar — photos, video and music in place',
      'Skim a folder on the filmstrip, and one click puts you on that file',
      'Step back through where you have been (Ctrl+←, Ctrl+→)',
      'Keep five layouts as presets and switch between them',
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
