import { ref, computed } from 'vue'

export type Lang = 'ko' | 'en'

export const lang = ref<Lang>('ko')

export function toggleLang() {
  lang.value = lang.value === 'ko' ? 'en' : 'ko'
}

const dict = {
  ko: {
    nav: {
      howto: '사용법',
      screenshots: '스크린샷',
      features: '기능',
      download: '다운로드',
      github: 'GitHub',
    },
    hero: {
      eyebrow: 'Windows 폴더/파일 탐색기 유틸',
      title: 'Edgetree',
      tagline: '화면 가장자리에 항상 붙어있는, VS Code 스타일의 가벼운 탐색기',
      description:
        '폴더 구조를 빠르게 훑어보고 파일 위치로 바로 이동하기 위한 보조 도구입니다.',
      ctaDownloadStandalone: '독립 실행형 다운로드',
      ctaDownloadStandard: '일반 버전 다운로드',
      ctaGithub: 'GitHub에서 보기',
    },
    howto: {
      title: '사용 방법',
      subtitle: '제목표시줄의 아이콘 몇 개로 대부분의 동작을 제어합니다',
      steps: [
        { title: '도킹 / 자동접기/펼치기', desc: '제목표시줄 아이콘 클릭 한 번으로 화면 가장자리에 얇게 숨고, 마우스를 가장자리로 가져가면 다시 펼쳐집니다.' },
        { title: '자동접기/펼치기 고정 해제', desc: '펼쳐진 상태에서 핀을 클릭하면 자동접기/펼치기가 꺼지고 항상 펼쳐진 상태로 고정됩니다.' },
        { title: '도킹 / 창모드', desc: '제목바를 드래그하면 자유롭게 움직이는 창모드로 전환되고, 핀을 클릭하면 다시 화면 가장자리에 도킹됩니다.' },
        { title: '전체 펼침/접기', desc: '제목바의 ▲ 아이콘으로 펼쳐진 폴더 트리 전체를 한 번에 접었다 복원할 수 있습니다.' },
      ],
    },
    screenshots: {
      title: '주요 기능',
      subtitle: '화면 어디에 있든, 익숙한 스타일',
      items: [
        {
          title: '도킹/자동 접기',
          desc: '화면의 좌/우에 고정시키거나 자동 접기 토글로 마우스를 움직여 펴거나 감출 수 있습니다.',
        },
        {
          title: '창모드 전환',
          desc: '헤더를 드래그하면 자유롭게 움직이고 크기를 바꿀 수 있는 일반 창이 됩니다.',
        },
        {
          title: '우클릭 메뉴',
          desc: '새 폴더, 복사/붙여넣기, 이름 바꾸기, 삭제, 터미널 열기 등을 한 곳에서.',
        },
        {
          title: '옵션 메뉴',
          desc: '자동 접기, 항상 위, 트레이 아이콘, 정렬, 언어 등 세세한 동작을 조절합니다.',
        },
        {
          title: '색상 커스터마이징',
          desc: '폴더명, 파일명, 배경, 선택 영역 등 14가지 색상을 원하는 대로 바꿀 수 있습니다.',
        },
        {
          title: '폴더 즐겨찾기',
          desc: '자주 사용하는 폴더를 탐색기의 상/하에 고정하고 클릭 한번으로 바로 펼쳐줍니다.',
        },
      ],
    },
    features: {
      title: '전체 기능',
      subtitle: '현재까지 구현된 기능 명세',
      items: [
        { title: '폴더 트리뷰', desc: 'VS Code 스타일 트리 탐색 — 전체 접기/복원, 자동 접기 토글, 폴더당 표시 개수 제한으로 큰 폴더도 빠르게 열립니다.' },
        { title: '도킹 및 창모드', desc: '화면 좌/우 도킹과 자유 이동 창모드를 오가고, 바깥 라인 더블클릭으로 창 크기를 자동으로 맞출 수 있습니다.' },
        { title: '자동 숨김', desc: '아이콘 클릭 한 번으로 화면 가장자리에 숨고, 마우스를 올리면 펼쳐집니다. 핀으로 고정도 가능합니다.' },
        { title: '기본 옵션', desc: '윈도우 시작 시 자동 실행, 항상 위에 표시, 트레이로 최소화 등을 옵션으로 조절합니다.' },
        { title: '파일 관리', desc: '다른 앱으로 파일을 바로 드래그하고, 복사/붙여넣기/이름변경/속성 등을 지원하며 외부 변경사항도 실시간 반영됩니다.' },
        { title: '즐겨찾기', desc: '자주 쓰는 폴더를 등록해 클릭 한 번으로 이동, 영역 위치도 상/하로 바꿀 수 있습니다.' },
        { title: '폴더/파일 아이콘 토글', desc: '폴더/파일 아이콘을 숨겨서 더 심플하게 볼 수 있습니다.' },
        { title: '탭 간격 조정', desc: '들여쓰기 폭을 취향대로 좁히거나 넓힐 수 있습니다.' },
        { title: '정렬 옵션', desc: '이름·날짜 기준 오름차순/내림차순, 폴더별로 즉시 재정렬됩니다.' },
        { title: '색상 커스터마이징', desc: '거의 모든 화면 요소의 색상을 원하는 대로 지정할 수 있습니다.' },
        { title: '폰트 크기 조정', desc: 'Ctrl +/- 로 트리 글자 크기 조절, Ctrl+0으로 기본값 복원.' },
        { title: '설정 내보내기/가져오기/초기화', desc: 'JSON으로 저장·복원하고, 전체 초기화 시 레지스트리까지 깔끔하게 정리됩니다.' },
        { title: '한국어/영어 지원', desc: '옵션 메뉴에서 언제든 언어를 전환할 수 있습니다.' },
      ],
    },
    info: {
      requirementsTitle: '시스템 요구사항',
      requirements: [
        'Windows 10 / 11',
        '일반 버전: .NET 8 데스크톱 런타임 필요',
        '독립 실행형 버전: 추가 설치 없이 바로 실행',
      ],
      licenseTitle: '라이선스',
      licenseBody:
        'MIT 라이선스로 배포됩니다 — 자유롭게 사용, 수정, 배포할 수 있으며 별도의 보증 없이 있는 그대로 제공됩니다.',
      licenseLink: 'LICENSE.md 전문 보기',
    },
    download: {
      title: '지금 다운로드',
      subtitle: '설치 프로그램 없이, exe 파일 하나면 충분합니다',
      standardTitle: '일반 버전',
      standardDesc: '~1 MB · .NET 8 데스크톱 런타임 필요',
      standaloneTitle: '독립 실행형',
      standaloneDesc: '~160 MB · 설치 없이 바로 실행',
      button: '다운로드',
      note: '어떤 버전을 받아야 할지 모르겠다면 독립 실행형을 선택하세요.',
    },
    footer: {
      contact: '문의',
      copyright: `© ${new Date().getFullYear()} Edgetree. MIT License.`,
    },
  },
  en: {
    nav: {
      howto: 'How to Use',
      screenshots: 'Screenshots',
      features: 'Features',
      download: 'Download',
      github: 'GitHub',
    },
    hero: {
      eyebrow: 'Windows Folder & File Explorer Utility',
      title: 'Edgetree',
      tagline: 'A lightweight explorer that lives at your screen edge, VS Code style',
      description:
        "A quick way to glance at a folder structure and jump straight to a file.",
      ctaDownloadStandalone: 'Download Standalone',
      ctaDownloadStandard: 'Download Standard',
      ctaGithub: 'View on GitHub',
    },
    howto: {
      title: 'How to Use',
      subtitle: 'A few titlebar controls handle most of it',
      steps: [
        { title: 'Dock / Auto-Hide', desc: 'One click on the titlebar icon tucks it away to a thin sliver at the screen edge; move your mouse there to bring it back.' },
        { title: 'Unpin Auto-Hide', desc: 'While expanded, click the pin to turn off auto-hide and keep it open for good.' },
        { title: 'Dock / Floating Window', desc: 'Drag the titlebar to undock into a free-floating window; click the pin again to dock it back.' },
        { title: 'Collapse / Restore All', desc: 'The ▲ icon on the titlebar collapses or restores every expanded folder in the tree at once.' },
      ],
    },
    screenshots: {
      title: 'Edgetree in action',
      subtitle: 'Wherever it sits on screen, it feels familiar',
      items: [
        {
          title: 'Docking',
          desc: 'Anchors to the left or right edge of your screen, always ready — switch sides anytime from the options menu.',
        },
        {
          title: 'Floating window mode',
          desc: 'Drag the header to undock into a normal, freely movable window.',
        },
        {
          title: 'Right-click menu',
          desc: 'New folder, copy/paste, rename, delete, open terminal, and more — all in one place.',
        },
        {
          title: 'Options menu',
          desc: 'Fine-tune Auto Collapse, always-on-top, tray icon, sort order, language, and more.',
        },
        {
          title: 'Color customization',
          desc: '14 customizable colors — folder names, file names, backgrounds, selection, and more.',
        },
        {
          title: 'Items per folder',
          desc: 'Adjust how many items each folder shows at once, from 1 to 50.',
        },
      ],
    },
    features: {
      title: 'And more',
      subtitle: "What didn't fit in a screenshot",
      items: [
        { title: 'Folder Tree View', desc: "VS Code-style tree browsing - collapse/restore all, Auto Collapse, and a per-folder item cap keep even huge folders fast." },
        { title: 'Docking & Window Mode', desc: "Dock to either screen edge or float as a free window - double-click the edge to auto-fit the width." },
        { title: 'Auto-Hide', desc: 'One click tucks it away to a thin sliver at the screen edge; hover to reveal, pin to keep it open.' },
        { title: 'Core Options', desc: "Launch at Windows startup, stay always-on-top, minimize to tray - all toggleable from the options menu." },
        { title: 'File Management', desc: 'Drag files straight into other apps, plus copy/paste/rename/properties, with live updates when files change externally.' },
        { title: 'Favorites', desc: 'Pin frequently used folders and jump to them in one click; switch the panel to the top or bottom.' },
        { title: 'Folder/File Icon Toggle', desc: 'Hide folder and file icons for a cleaner look.' },
        { title: 'Indent Spacing', desc: "Adjust the tree's indent width to taste." },
        { title: 'Sort Options', desc: "Sort by name or date, ascending or descending - re-sorts instantly per folder." },
        { title: 'Color Customization', desc: 'Recolor nearly every visible element to your liking.' },
        { title: 'Font Size', desc: 'Ctrl +/- resizes the tree text, Ctrl+0 resets it.' },
        { title: 'Export / Import / Reset Settings', desc: "Save and restore settings as JSON - a full reset also cleans up the Windows registry entry." },
        { title: 'Korean / English UI', desc: 'Switch languages anytime from the options menu.' },
      ],
    },
    info: {
      requirementsTitle: 'System Requirements',
      requirements: [
        'Windows 10 / 11',
        'Standard build: requires the .NET 8 Desktop Runtime',
        'Standalone build: nothing else to install',
      ],
      licenseTitle: 'License',
      licenseBody:
        'Released under the MIT License — free to use, modify, and distribute, provided as-is without warranty.',
      licenseLink: 'Read the full LICENSE.md',
    },
    download: {
      title: 'Download Now',
      subtitle: 'No installer — just one exe file',
      standardTitle: 'Standard',
      standardDesc: '~1 MB · requires .NET 8 Desktop Runtime',
      standaloneTitle: 'Standalone',
      standaloneDesc: '~160 MB · runs with nothing else installed',
      button: 'Download',
      note: "Not sure which one? Go with the standalone build.",
    },
    footer: {
      contact: 'Contact',
      copyright: `© ${new Date().getFullYear()} Edgetree. MIT License.`,
    },
  },
} as const

export const t = computed(() => dict[lang.value])
