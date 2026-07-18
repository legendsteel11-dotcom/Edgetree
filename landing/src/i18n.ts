import { ref, computed } from 'vue'

export type Lang = 'ko' | 'en'

export const lang = ref<Lang>('ko')

export function toggleLang() {
  lang.value = lang.value === 'ko' ? 'en' : 'ko'
}

const dict = {
  ko: {
    nav: {
      howto: '사용방법',
      screenshots: '주요기능',
      features: '기타기능',
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
      title: '사용방법',
      steps: [
        { title: '도킹 / 자동접기 / 창모드 전환', desc: '제목표시줄 아이콘 클릭 한 번으로 화면 가장자리에 얇게 숨고, 마우스를 가장자리로 가져가면 다시 펼쳐집니다. 제목표시줄을 드래그하여 창모드로 전환할 수 있습니다.' },
        { title: '간단한 파일 검색', desc: '제목표시줄의 검색 아이콘 혹은 Ctrl + F / Ctrl + E 로 검색창과 탐색기 창을 전환하고 간단한 파일 검색을 할 수 있습니다.' },
        { title: '컨텍스트 메뉴 및 파일 액세스', desc: '탐색기에서 폴더나 파일을 우클릭하여 기본적인 기능 등을 편리하게 사용할 수 있습니다. 파일을 창 외부로 복사하거나, VS Code의 탐색기에 바로 등록할 수 있습니다.' },
        { title: '전체 펼침/접기, 자동 아코디언', desc: '제목바의 ▲ 아이콘으로 펼쳐진 폴더 트리 전체를 한 번에 접었다 복원할 수 있습니다. 또 필요한 폴더만 펼치고 나머지는 자동으로 접히게 할 수 있습니다.' },
      ],
    },
    screenshots: {
      title: '주요기능',
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
          desc: '폴더명, 파일명, 배경, 선택 영역 등 색상을 원하는 대로 바꿀 수 있고, 다크/라이트 모드를 각각 따로 지정할 수 있습니다.',
        },
        {
          title: '폴더 즐겨찾기',
          desc: '자주 사용하는 폴더를 탐색기의 상/하에 고정하고 클릭 한번으로 바로 펼쳐줍니다.',
        },
      ],
    },
    features: {
      title: '기타기능',
      items: [
        { title: '폴더 트리뷰', desc: 'VS Code 스타일 트리 탐색 — 전체 접기/복원, 자동 접기 토글, 폴더당 표시 개수 제한으로 큰 폴더도 빠르게 열립니다.' },
        { title: '도킹 및 창모드', desc: '화면 좌/우 도킹과 자유 이동 창모드를 오가고, 바깥 라인 더블클릭으로 창 크기를 자동으로 맞출 수 있습니다.' },
        { title: '자동 숨김', desc: '아이콘 클릭 한 번으로 화면 가장자리에 숨고, 마우스를 올리면 펼쳐집니다. 핀으로 고정도 가능합니다.' },
        { title: '기본 옵션', desc: '윈도우 시작 시 자동 실행, 항상 위에 표시, 트레이로 최소화 등을 옵션으로 조절합니다.' },
        { title: '파일 관리', desc: '다른 앱으로 파일을 바로 드래그하고, 복사/붙여넣기/이름변경/속성 등을 지원하며 외부 변경사항도 실시간 반영됩니다.' },
        { title: '파일/폴더 검색', desc: '가벼운 인덱싱으로 원하는 파일과 폴더의 위치로 바로 이동할 수 있습니다.' },
        { title: '즐겨찾기', desc: '자주 쓰는 폴더를 등록해 클릭 한 번으로 이동, 영역 위치도 상/하로 바꿀 수 있습니다.' },
        { title: '폴더/파일 아이콘 토글', desc: '폴더/파일 아이콘을 숨겨서 더 심플하게 볼 수 있습니다.' },
        { title: '탭 간격 및 행 간격 조정', desc: '들여쓰기 폭과 행 간격을 각각 취향대로 좁히거나 넓힐 수 있습니다.' },
        { title: '정렬 옵션', desc: '이름·날짜 기준 오름차순/내림차순. 폴더별로 전역 설정과 별개로 정렬을 지정하고, 언제든 해제할 수 있습니다.' },
        { title: '색상 커스터마이징', desc: '거의 모든 화면 요소의 색상을 원하는 대로 지정할 수 있고, 다크/라이트 모드를 지원합니다.' },
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
      title: '다운로드',
      standardTitle: '일반 버전',
      standardDesc: '~1 MB · .NET 8 데스크톱 런타임 필요',
      standaloneTitle: '독립 실행형',
      standaloneDesc: '~160 MB · 설치 없이 바로 실행',
      button: '다운로드',
      note: '어떤 버전을 받아야 할지 모르겠다면 독립 실행형을 선택하세요.',
      smartscreenNote:
        '실행 파일에 정식 코드 서명 인증서가 없어 Windows에서 "알 수 없는 게시자" 경고가 뜰 수 있습니다 —\n"추가 정보" → "실행"을 누르시면 정상적으로 실행됩니다.',
      virustotalNote:
        'VirusTotal 등에서 일부 백신이 휴리스틱(패턴 기반) 오탐을 표시할 수 있습니다. 서명되지 않은 소규모 개인 개발 프로그램에서 흔히 나타나는 현상이며, 소스 코드가 GitHub에 전부 공개되어 있어 언제든 직접 확인하실 수 있습니다.',
    },
    footer: {
      contact: '문의',
      copyright: `© ${new Date().getFullYear()} Edgetree. MIT License.`,
    },
  },
  en: {
    nav: {
      howto: 'How to Use',
      screenshots: 'Key Features',
      features: 'More Features',
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
      steps: [
        { title: 'Dock / Auto-Hide / Window Mode', desc: 'One click on the titlebar icon tucks it away to a thin sliver at the screen edge; move your mouse there to bring it back. Drag the titlebar to switch into a floating window.' },
        { title: 'Quick File Search', desc: "The titlebar's search icon (or Ctrl + F / Ctrl + E) toggles between the search box and the explorer for a quick file lookup." },
        { title: 'Context Menu & File Access', desc: 'Right-click a folder or file in the explorer for the essentials at hand — copy files out to another window, or send them straight to the VS Code explorer.' },
        { title: 'Collapse/Restore All & Auto Accordion', desc: 'The ▲ icon on the titlebar collapses or restores the whole expanded tree at once; you can also keep only the folder you open expanded while the rest auto-collapse.' },
      ],
    },
    screenshots: {
      title: 'Key Features',
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
          desc: 'Customizable colors for folder names, file names, backgrounds, selection, and more — with separate dark and light palettes.',
        },
        {
          title: 'Items per folder',
          desc: 'Adjust how many items each folder shows at once, from 1 to 50.',
        },
      ],
    },
    features: {
      title: 'More Features',
      items: [
        { title: 'Folder Tree View', desc: "VS Code-style tree browsing - collapse/restore all, Auto Collapse, and a per-folder item cap keep even huge folders fast." },
        { title: 'Docking & Window Mode', desc: "Dock to either screen edge or float as a free window - double-click the edge to auto-fit the width." },
        { title: 'Auto-Hide', desc: 'One click tucks it away to a thin sliver at the screen edge; hover to reveal, pin to keep it open.' },
        { title: 'Core Options', desc: "Launch at Windows startup, stay always-on-top, minimize to tray - all toggleable from the options menu." },
        { title: 'File Management', desc: 'Drag files straight into other apps, plus copy/paste/rename/properties, with live updates when files change externally.' },
        { title: 'File Search', desc: 'Lightweight indexing to jump straight to the file location you are after.' },
        { title: 'Favorites', desc: 'Pin frequently used folders and jump to them in one click; switch the panel to the top or bottom.' },
        { title: 'Folder/File Icon Toggle', desc: 'Hide folder and file icons for a cleaner look.' },
        { title: 'Indent & Row Spacing', desc: "Adjust the tree's indent width and row spacing independently, to taste." },
        { title: 'Sort Options', desc: "Sort by name or date, ascending or descending. Each folder can keep its own sort independent of the global default, and you can clear it anytime." },
        { title: 'Color Customization', desc: 'Recolor nearly every visible element to your liking, with separate dark and light modes.' },
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
      title: 'Download',
      standardTitle: 'Standard',
      standardDesc: '~1 MB · requires .NET 8 Desktop Runtime',
      standaloneTitle: 'Standalone',
      standaloneDesc: '~160 MB · runs with nothing else installed',
      button: 'Download',
      note: "Not sure which one? Go with the standalone build.",
      smartscreenNote:
        "There's no paid code-signing certificate on the exe, so Windows may show an \"Unknown publisher\" SmartScreen warning —\nclick \"More info\" then \"Run anyway\" to proceed.",
      virustotalNote:
        'A few antivirus engines on VirusTotal and similar sites may flag it with a generic heuristic detection - common for small, unsigned indie apps. The full source is public on GitHub, so you’re welcome to check it yourself.',
    },
    footer: {
      contact: 'Contact',
      copyright: `© ${new Date().getFullYear()} Edgetree. MIT License.`,
    },
  },
} as const

export const t = computed(() => dict[lang.value])
