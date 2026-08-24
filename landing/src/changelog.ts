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
// A RENAME IS A LIST ITEM, NOT A STORY (v2.4.1, the author's call: 사소한걸
// 너무 풀어서 설명해 준 느낌이 강해서 민망스럽다). "X → Y" is the whole line.
// WHY that name was picked is our side of it, not the reader's: 계열을 맞췄다,
// 국문과 겹치는 낱말이 없었다 - all of it stays in the commit message and TODO,
// where the reasoning is actually useful. The same goes for the GitHub release
// notes, which had grown two paragraphs around a one-line fix; they are a list
// now too, and a one-line fix does not get a section heading of its own.
//
// The exception is narrow: keep the explanation only where WITHOUT it nobody
// can tell what was fixed - a window that had been contradicting itself, or a
// dialog title that said something had already happened when it had not.
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
    // ONE LINE, AND THE MARGIN FIX IS NOT THE SECOND ONE. The caption's bottom
    // gap not following the text size after a full screen is 사소함 by the
    // v2.3.0 test - nobody reading this would recognise it, and it ships
    // either way. The line that stays is the one someone could have SEEN.
    version: 'v2.5.3',
    ko: [
      '앱 전체화면을 사용한 뒤 멀티미디어 패널의 파일 정보가 썸네일 바 위에 겹쳐 보이고 패널 높이가 흔들리던 문제를 수정했습니다.',
    ],
    en: [
      'Fixed the multimedia panel’s file details overlapping the thumbnail bar, and the panel height shifting, after using full screen.',
    ],
  },
  {
    version: 'v2.5.2',
    ko: [
      '폴더에 지정한 정렬이 하위 폴더에도 적용됩니다. 하위 폴더에 개별 정렬을 지정한 경우에는 그대로 유지됩니다.',
      '제목 표시줄의 `전체 접기` 아이콘을 `Shift`+클릭하면 펼침 상태를 저장하지 않고 접습니다.',
      'USB, 클라우드 드라이브를 연결하거나 해제하면 트리에 자동으로 반영됩니다.',
      '썸네일 목록에서 선택된 항목의 가시성을 높였습니다.',
      '음악을 분리 재생 중일 때 제목 우측의 X로 재생을 종료할 수 있습니다.',
      '`숨김·시스템 항목 표시`가 `옵션 → 기본 설정`으로 이동했습니다.',
      '트리에서 `더 보기`에 가려진 항목을 썸네일에서 클릭해도 선택되지 않던 문제를 수정했습니다.',
    ],
    en: [
      'A folder’s sort order now applies to its subfolders. A subfolder with its own sort keeps it.',
      'Shift-clicking the Collapse All icon in the title bar folds without storing the expanded state.',
      'Connecting or removing a USB or cloud drive updates the tree automatically.',
      'The selected cell in the thumbnail list is easier to pick out.',
      'An X beside the title ends detached audio playback.',
      'Show Hidden and System Items moved to Options → General.',
      'Clicking a thumbnail for an item hidden behind Show More in the tree now selects it.',
    ],
  },
  {
    // v2.5.1 CARRIES THE ROUND'S WHOLE CARD, because v2.5.0 lived two hours
    // and reached two downloads before this superseded it - a two-line patch
    // card on top would have buried the feature release the author actually
    // shipped today (their report, 2026-08-22: "새 기능들 랜딩카드가 다
    // 숨겨져서"). The crash fix is deliberately NOT a line: it repaired
    // something introduced the same day that effectively nobody had, and the
    // v2.4.2 rule applies - listing it says the new list shipped broken. The
    // refresh line stays: the strip (and the bar before it) never re-asked
    // about a changed file, so it is new behaviour, not a same-day repair.
    // The v2.5.0 entry below stays as history for the arrows.
    version: 'v2.5.1',
    ko: [
      '썸네일 바에 세로로 스크롤되는 `썸네일 목록` 배치가 추가되었으며, 기본값으로 적용됩니다.',
      '썸네일 크기는 `Ctrl`+휠, 표시되는 줄 수는 경계 드래그로 조정할 수 있습니다.',
      '썸네일 목록에서 여러 파일을 선택해 복사, 잘라내기, 삭제, 밖으로 드래그할 수 있습니다.',
      '썸네일이 없는 파일은 종류 아이콘으로 표시됩니다.',
      '새로고침 시 내용이 변경된 파일의 썸네일이 갱신됩니다.',
      '썸네일이 간혹 거꾸로 표시되던 문제를 수정했습니다.',
      '썸네일을 클릭해도 이미지가 바뀌지 않던 문제를 수정했습니다.',
    ],
    en: [
      'The thumbnail bar can lay its pictures out as a scrolling list, now the default.',
      'Ctrl+wheel sizes the thumbnails; dragging the edge shows more rows.',
      'Select several pictures and copy, cut, delete or drag them out together.',
      'Files with no picture of their own show their file-type icon.',
      'Refreshing renews the thumbnails of files whose content changed.',
      'Thumbnails no longer come up upside down now and then.',
      'A thumbnail click no longer fails to change the picture.',
    ],
  },
  // v2.5.0 HAS NO ENTRY OF ITS OWN - the one above is the merged card for the
  // 2026-08-22 pair (the author's call: "랜딩카드를 합칠까요 이번엔? 랜딩에서만").
  // It lived two hours before v2.5.1 superseded it, so a separate entry would
  // show the arrows two near-identical cards. The label stays exactly 'v2.5.1'
  // because UpdateNotes.vue hides the whole card unless the newest entry's
  // version EQUALS the tag GitHub reports as latest - a "~v2.5.1" range label
  // would read nicely and blank the card. The per-release record lives in the
  // READMEs' changelog and the GitHub release notes, which keep both.
  {
    // ELEVEN LINES IN THE RELEASE NOTES, SIX HERE. What came out is what a
    // person finds while using the app rather than while deciding to download
    // it: the panel folding on Backspace, the thumbnail bar stepping aside in
    // full screen, the play button following the pointer. All three are real
    // and none of them would move anyone's hand toward the button.
    //
    // The three subtitle items are one line. Size, sync and position arrived
    // together and are read together; three lines would have made a card about
    // subtitles.
    //
    // NO FIX LINES THIS TIME, and that is not an omission. Everything repaired
    // in this round was a repair to something else in this round - the docked
    // full screen, the thumbnail bar, the subtitle scale - so none of it ever
    // reached anyone. Listing them would say the new features shipped broken.
    //
    // The two lists are the same length here, unlike v2.4.1: every line is
    // true on both screens this time.
    version: 'v2.4.2',
    ko: [
      '시청 중이던 영상이 트리 하단에 표시되며, 선택하면 이전 재생 위치부터 재생됩니다.',
      '자막 크기가 영상 크기에 연동됩니다. `<` `>` 로 싱크를, 메뉴에서 위치를 조정할 수 있습니다.',
      '고정 상태에서도 `바탕화면 채우기`를 사용할 수 있습니다.',
      '`F` 키로 창 크기를 영상 비율에 맞게 조정할 수 있습니다.',
      '마우스 앞·뒤 버튼으로 폴더 이력과 이전·다음 이미지를 이동합니다.',
      '재생 볼륨이 저장됩니다.',
    ],
    en: [
      'The film you were watching stays at the foot of the tree, and picks up where you left it.',
      'Subtitles scale with the film. < and > shift the sync; the menu sets their position.',
      'Fill the desktop without undocking first.',
      'F fits the window to the film, so the black bands go.',
      'The mouse thumb buttons move through folders, and through pictures.',
      'The playback volume is remembered.',
    ],
  },
  {
    // A PATCH RELEASE MADE ALMOST ENTIRELY OF WORDS, which is the awkward case
    // for this file: only one line here changes what the app DOES. The rest are
    // labels and sentences, and a card is read by someone deciding whether to
    // download - so the test each line still has to pass is whether a person
    // would notice.
    //
    // THE TWO LISTS ARE NOT THE SAME LENGTH, on purpose. Four of these fixes
    // only exist on the English screen (Korean text showing there, one spelling,
    // a dialog title, counts of one), and a Korean reader gets nothing from
    // being told about them line by line - so Korean carries them as one line
    // at the end. Padding either list to match the other would mean inventing a
    // line or dropping a true one.
    //
    // THE AUTHOR'S OWN WORDING for the tray line, used as given (2026-08-18):
    // the draft said the tray answers once, and theirs names what was actually
    // happening - the message kept coming. English follows theirs rather than
    // the draft, so the two sides describe the same thing.
    version: 'v2.4.1',
    ko: [
      '프리셋을 연속으로 저장했을 때 트레이 메시지가 계속 나오는 것을 방지했습니다.',
      '`표시할 파일 종류`가 `표시할 파일 형식`으로 변경되었습니다.',
      '`바탕화면 전체`가 `바탕화면 채우기`로, `제목 표시줄 타이틀`이 `제목 표시줄 텍스트`로 변경되었습니다.',
      '영문 UI의 표기와 문장을 정리했습니다.',
    ],
    en: [
      'Saving presets one after another no longer leaves the tray popping.',
      'Two help rows no longer show Korean text on the English screen.',
      'One spelling throughout: colors, minimize, grayscale.',
      'The language dialog now asks to change the language instead of saying it already changed.',
      'Counts of one read correctly.',
      'Accordion Mode is now Auto-Collapse Folders, the name its Korean row already carried.',
    ],
  },
  {
    // THE AUTHOR'S OWN WORDING, used verbatim (2026-08-17). The draft handed to
    // them was rewritten line by line into one register - plain declaratives,
    // none of the em-dash asides the draft leaned on - and that register is the
    // point rather than a preference: seven lines read as one list instead of
    // seven separate remarks. When the author hands back a list, it goes in as
    // given.
    //
    // TWO LINES FOR THE MERGE, and the first of them exists for the fear rather
    // than the feature. "즐겨찾기가 북마크로 통합" on its own reads as favourites
    // being GONE, and this card sits directly above a download button - that one
    // misreading is the most expensive thing on the page. So the carry-over is
    // on the same line as the merge, and the reorder gets its own; folding them
    // together buries the half that reassures.
    //
    // SEVEN IS THE UPPER END of what this card should carry. It is a glance, and
    // the ceiling held because line 5 absorbed a second item: the picture size
    // persisting was drafted as its own line and failed 당연함 - a reader who has
    // not used the app assumes it already did that, and saying so invites the
    // thought that it could not. Merged into the album-art line, where it reads
    // as the same subject.
    //
    // LINE 7 IS THE ONLY ONE THAT NAMES A SYMPTOM, which this file's rules allow
    // for an intermittent fix: the person who hit it knows it by "폴더가 접혀
    // 있다" and by nothing else. It stays because it frightens nobody who did not
    // hit it - nothing is lost, a view is.
    version: 'v2.4.0',
    ko: [
      '즐겨찾기가 북마크로 통합되었습니다. 기존에 저장한 항목은 그대로 유지됩니다.',
      '북마크 패널에서 항목을 드래그해 순서를 변경할 수 있습니다.',
      '프리셋에 창 모드가 저장되며, 종료할 때의 모드로 실행됩니다.',
      '전체 화면 전환 시 창 크기를 그대로 유지하는 옵션이 추가되었습니다.',
      '앨범아트에 맞춤 · 1:1 · 채우기 옵션이 추가되었으며, 선택한 크기는 재시작 후에도 유지됩니다.',
      '이미 열려 있는 폴더를 클릭하면 선택만 되고, 접기는 한 번 더 클릭해야 동작합니다.',
      '폴더가 임의로 접히거나 트리가 C: 드라이브로 초기화되던 문제를 수정했습니다.',
    ],
    en: [
      'Favorites are now merged into Bookmarks, and your saved items carry over.',
      'Drag rows in the Bookmarks panel to reorder them.',
      'Presets now store the window mode, and the app reopens in the mode it was closed in.',
      'Added an option to keep the current window size when entering full screen.',
      'Album art now supports Fit, 1:1, and Fill, and the selected size persists across restarts.',
      'Clicking an already-open folder now only selects it; a second click collapses it.',
      'Fixed folders collapsing on their own and the tree resetting to C:.',
    ],
  },
  {
    // THREE LINES, and a patch release is where the rules below are easiest to
    // keep: there is no room to pad. Each of these changes what a person can do
    // or removes something they ran into, which is the whole test.
    //
    // The help gaining a line about the three icon switches is NOT here. It
    // changes what the app explains, not what it does - the same cut the
    // renames took in the entry below.
    version: 'v2.3.1',
    ko: [
      '북마크·즐겨찾기·검색 결과로 이동할 때도 폴더 자동 접기가 적용됨',
      '트리 빈 곳 우클릭에서 프리셋 사용',
      '앱 전체화면에서 창을 화면 끝까지 넓힐 수 없던 문제 수정',
    ],
    en: [
      'Folder auto-collapse now applies to bookmark, favourite and search jumps too',
      'Presets on the tree\'s empty-space right-click menu',
      'Fixed: the window could not be widened to the screen edge in full screen',
    ],
  },
  {
    // THE AUTHOR CUT THIS LIST FROM 21 LINES TO 13 (2026-08-16), and the
    // reasons given for the cuts are worth more than the list itself, because
    // they name a test this file did not have. Three words did all the work:
    // 당연함, 미약함, 사소함.
    //
    // 당연함 IS THE SHARPEST OF THE THREE - a line describing something the
    // reader assumed the app already did. It costs a slot and returns nothing,
    // and worse, it invites the thought that the app could not do it until now.
    // Four lines went out on it: the clock's size ("크기도 조절"), slideshows
    // filling the panel, films cropping to fill it, and the full cover reaching
    // its own controls. All four were genuine work; none of them is news to
    // someone who has not used the app. Ask of every line: would a reader be
    // surprised this needed saying?
    //
    // 미약함 took the 8pt font step, which had LED the card - and the round's
    // theme was a smaller screen. A settings range is not a reason to download
    // anything, even carrying its consequence. Being a round's theme does not
    // qualify a line for the top of the card; being what someone WANTS does.
    //
    // 사소함 took the footer chips going bold, the missing-folder notice, and
    // three fixes narrow enough that naming them describes a fault more than a
    // remedy (the window widening once after a full cover, the panel opening
    // shorter than its list, the tree creeping upward). Two of those are
    // absorbed rather than lost: the 더 보기·접기 line now carries the drift as
    // well, and the expand-arrow line carries the gap beside it.
    //
    // WORDING, same pass: 대폭 came out of the formats line - the count is the
    // argument, an intensifier only weakens it. And two internal names were
    // replaced by what a reader would say: 전체 덮기 → 앱 전체화면, 조작 막대 →
    // 컨트롤 패널. The card is not where the app teaches its own vocabulary.
    // (English keeps "the playback controls" there instead of a literal
    // "control panel", which is Windows' own thing.)
    //
    // THE SEARCH FREEZE SHOWED THERE IS A THIRD WAY OUT, and it is worth
    // stating as a rule (author, 2026-08-16). This file's header sets up what
    // looked like a closed bind: a symptom that frightens whoever never hit it
    // stays out, and softening it into vagueness ("반응이 느려지던") is equally
    // forbidden. The freeze is the round's most valuable fix and both doors
    // were shut on it.
    //
    // The way through is to QUALIFY THE SCOPE at the front of the line - 일부,
    // 부분적으로, 특정 경우. It is not a softener: the sentence still says the
    // app stopped responding, in the reader's own words. What it removes is the
    // implication that this is what the app does, which was the frightening
    // part all along. The precedent was already in the entry below - "일부
    // 북마크 즐겨찾기 이동 시" - written by the author for exactly this reason.
    //
    // Reach for it whenever a true line would read as a general property of the
    // app rather than as a case someone ran into.
    //
    // ONE 더 보기·접기 LINE, not the two it started as. The drift someone SAW
    // and the folder selection that answers "which of these two identically
    // named lists am I opening" are one sentence to a reader, and the fix is
    // only interesting as the reason the other half works.
    //
    // NAMED FIXES ARE WELCOME HERE (author, 2026-08-16) - what is not welcome
    // is the trivia. Renames go out on that instruction: 셔플 → 셔플 반복, and
    // the app calling itself 앱 rather than 사이드바. So do the edits with no
    // gesture behind them - the colour list gaining dividers, presets gaining
    // their own heading, the separator that kept appearing at the top of a
    // menu, a settings file that cannot be written now saying so once. All
    // real work; none of it changes what a person can do, which is the line
    // between this card and the release notes.
    //
    // THE ORDER IS THE ONLY STRUCTURE THIS LIST HAS - it renders flat, with no
    // headings - so it has to be arranged rather than appended to. Three runs:
    // what is new, then the tree and its lists, then the fixes. Fixes last
    // because a card above a download button should open on what the app does,
    // and because a run of 수정 lines reads as a list of what was broken when it
    // sits at the top.
    version: 'v2.3.0',
    ko: [
      'PSD·RAW·JXL 등 패널에서 볼 수 있는 그림 형식 추가',
      '패널 위에 시계 표시(F9)',
      '프리셋을 Ctrl+1~5로 바꾸고, Ctrl+Shift+S로 덮어씀',
      '파일을 선택하면 멀티미디어 패널이 자동으로 열림(옵션)',
      '드라이브 아이콘 표시 옵션 추가',
      '색상 설정에 펼침 화살표와 하단 칩 색 추가',
      '재생중인 음악과 별개로 선택된 음악이 더 쉽게 구분됨',
      '더 보기·접기 시 부모폴더 선택, 들여쓰기 안내선 바로 적용',
      '펼침 기호 수정 및 들여쓰기 안내선을 중심에 맞춤',
      '북마크·즐겨찾기를 추가하면 패널이 그 목록으로 바뀜',
      '일부 대용량·네트워크 폴더에서 인덱싱 도중 검색어를 고칠 때 멈추던 문제 수정',
      '썸네일 바가 PSD·RAW처럼 큰 파일까지 미리 읽어 느려지던 문제 수정',
      '음악 재생 중 앱 전체화면에서 컨트롤 패널이 가리키는 동안 사라지던 문제 수정',
    ],
    en: [
      'PSD, RAW, JXL and more picture formats in the panel',
      'A clock over the panel (F9)',
      'Switch presets with Ctrl+1-5, overwrite with Ctrl+Shift+S',
      'The multimedia panel opens when you select a file (option)',
      'An option to show drive icons',
      'Colour settings for the expand arrow and the footer chips',
      'The track you picked reads apart from the one that is playing',
      'Show more / Show less selects the parent folder, and the indent guides follow at once',
      'A reworked expand arrow, with the indent guide through its centre',
      'Adding a bookmark or favourite opens the list it went into',
      'Fixed: in some large or network folders, editing the search box while it indexed left the app unresponsive',
      'Fixed: the thumbnail bar read ahead into large files like PSD and RAW',
      'Fixed: in full screen, the playback controls hid themselves while being pointed at',
    ],
  },
  {
    // THE SWAP LEADS THE CARD, the landing fix leads the release NOTES - the
    // author's own ordering, and the two are doing different jobs. A glance
    // above a download button opens with what the app can now DO; the notes
    // below it open with the fault that took four rounds to close.
    //
    // That fix is named by the SYMPTOM for the reason this file already states -
    // whoever hit it knows it as "the tree was at the bottom", not as a
    // calculation - and it says SOME jumps, which is the truth: it depended on
    // what the panel was still holding from earlier, so plenty of jumps always
    // landed correctly.
    //
    // The next line carries its CONDITION rather than the bare symptom. Said
    // plainly it read as the app folding itself shut at random, which is the
    // shape this file warns about; with the situation attached it is
    // recognisable to whoever saw it and unalarming to whoever did not. What
    // came out with it: the selection moving to a drive, which was the frightening
    // half and is only a consequence of the fold.
    //
    // The right-dock resize wobble is NOT here, and that is the author's own
    // call: it is better, not gone, and a card sitting above a download button
    // is the wrong place to claim a fix someone can still see happening.
    version: 'v2.2.0',
    ko: [
      '멀티미디어 패널과 트리 위치를 서로 바꿀 수 있음(옵션)',
      '일부 북마크 즐겨찾기 이동 시 트리 맨 아래에 붙던 현상 수정',
      '멀티미디어 패널을 열고 창 크기를 조절할 때 트리가 저절로 접히던 것',
      '자름맞춤 — 그림을 패널에 꽉 채워 보기',
      '그림을 볼 때 Ctrl·Shift+휠로 정밀 확대·축소',
      '폴더를 우클릭해 그 안의 음악·영상 이어 재생',
      '경로 표시줄에 방문한 폴더 목록',
      '앱 크기를 더 작게 축소할 수 있음',
      '색상 설정에 체인·모노, 목록 끝 그림자 적용',
    ],
    en: [
      'Swap the multimedia panel and the tree (option)',
      'Fixed: some jumps to a bookmark or favourite left the tree at the bottom',
      'The tree folding itself shut while the window was resized',
      'Fill — crop a picture to the panel',
      'Fine zoom on a picture with Ctrl or Shift and the wheel',
      'Right-click a folder to play the music and video in it',
      'The folders you have been in, listed on the path bar',
      'The app can be made smaller',
      'Colour chains and a greyscale roll, and shading at the ends of a list',
    ],
  },
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
      'Set a different wallpaper on each monitor (whichever one the app is on)',
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
      '앱이 펼쳐지는 순간 드물게 종료될 수 있던 문제를 수정했습니다',
    ],
    en: [
      'A stability follow-up to v1.7.0 — see below for what that release added',
      'Fixed the app closing unexpectedly in rare cases as it slides open',
    ],
  },
  {
    version: 'v1.7.0',
    ko: [
      '앱이 화면 높이를 다 쓰지 않아도 됩니다 — 위/아래 가장자리를 끌어 조정',
      '처음 설치하면 가장자리 가운데의 손잡이로 시작합니다',
      '버그 수정 및 성능 최적화',
    ],
    en: [
      'The app no longer has to fill the screen — drag its top or bottom edge',
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
      '색상 피커 — 선택하는 대로 앱에 바로 적용',
      '색상만 따로 내보내고 불러오기 — 다른 PC에서도 같은 색으로',
      '북마크 표시를 눌러 바로 해제',
      '파일 종류나 표시 개수를 바꿔도 보던 자리 그대로',
    ],
    en: [
      'A colour picker that applies to the app as you drag it',
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
