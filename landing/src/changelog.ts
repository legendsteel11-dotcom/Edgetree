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
    version: 'v1.4.1',
    ko: [
      '왼쪽 패널 표시 옵션 — 북마크 / 즐겨찾기 / 표시 안 함',
      '여러 폴더를 한 번에 골라서 숨기기',
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
      '검색 정렬을 메뉴에서 고르기',
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
