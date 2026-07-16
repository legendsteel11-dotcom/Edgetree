import { ref, computed } from 'vue'

export type Lang = 'ko' | 'en'

export const lang = ref<Lang>('ko')

export function toggleLang() {
  lang.value = lang.value === 'ko' ? 'en' : 'ko'
}

const dict = {
  ko: {
    nav: {
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
      ctaDownload: '다운로드',
      ctaGithub: 'GitHub에서 보기',
    },
    screenshots: {
      title: '스크린샷으로 보는 Edgetree',
      subtitle: '화면 어디에 있든, 익숙한 스타일',
      items: [
        {
          title: '도킹',
          desc: '화면 좌측 또는 우측 가장자리에 붙어 항상 열려 있습니다. 옵션 메뉴에서 방향을 바로 전환할 수 있습니다.',
        },
        {
          title: '자유 이동 창모드',
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
          title: '폴더 표시 개수 조정',
          desc: '폴더 하나에서 한 번에 보여줄 파일 개수를 1~50개 사이로 직접 조절할 수 있습니다.',
        },
      ],
    },
    features: {
      title: '이 외에도',
      subtitle: '스크린샷에 다 담지 못한 기능들',
      items: [
        { title: '즐겨찾기 패널', desc: '자주 쓰는 폴더를 헤더 아래 고정, 한 클릭으로 이동' },
        { title: '인라인 이름 바꾸기', desc: 'F2 한 번으로 트리 안에서 바로 편집' },
        { title: '파일 드래그 아웃', desc: '탐색기나 다른 앱으로 그대로 드래그 앤 드롭' },
        { title: '이름/날짜순 정렬', desc: '오름차순·내림차순, 폴더별로도 즉시 재정렬' },
        { title: '트레이로 최소화', desc: '트레이 아이콘 클릭 한 번으로 열고 닫기' },
        { title: '폰트 크기 확대/축소', desc: 'Ctrl + / - 로 트리 글자 크기 조절, 행간도 함께' },
        { title: '설정 내보내기/가져오기', desc: 'JSON 파일로 저장해서 다른 PC로 그대로 이동' },
        { title: '폴더/파일 아이콘 토글', desc: '폴더/파일 아이콘을 제거하여 더 깔끔하게 볼 수 있습니다.' },
        { title: '한국어/영어 지원', desc: '옵션 메뉴에서 언제든 전환' },
        { title: 'Material Icon Theme', desc: '파일/폴더 확장자별 아이콘 자동 적용' },
        { title: '자동 숨김', desc: '마우스를 가장자리에 올리면 살짝 나타났다가, 벗어나면 다시 얇은 선으로 숨습니다.' },
        { title: '윈도우 시작과 함께', desc: '윈도우 시작 시 자동으로 실행할 수 있습니다.' },
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
      ctaDownload: 'Download',
      ctaGithub: 'View on GitHub',
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
        { title: 'Favorites panel', desc: 'Pin folders below the header, jump in one click' },
        { title: 'Inline rename', desc: 'F2 edits the name directly in the tree row' },
        { title: 'Drag files out', desc: 'Standard drag-and-drop into Explorer or any app' },
        { title: 'Sort by name or date', desc: 'Ascending or descending, per-folder re-sort' },
        { title: 'Minimize to tray', desc: 'One click on the tray icon to open or close' },
        { title: 'Font size zoom', desc: 'Ctrl + / - resizes the tree text, row spacing included' },
        { title: 'Export / import settings', desc: 'Save to a JSON file, carry it to another PC' },
        { title: 'Folder/file icon toggle', desc: 'Hide folder and file icons for a cleaner look.' },
        { title: 'Korean / English UI', desc: 'Switch languages anytime from the options menu' },
        { title: 'Material Icon Theme', desc: 'Per-extension icons for files and folders' },
        { title: 'Auto-hide', desc: 'Peeks open when you hover the edge, and quietly hides back to a thin sliver when you move away.' },
        { title: 'Launch at startup', desc: 'Start automatically when Windows starts.' },
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
