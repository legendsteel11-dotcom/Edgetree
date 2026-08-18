// 나가는 글에 같은 실수가 다시 생겼는지 기계가 본다.  node tools/check-text.mjs
//
// 왜 있는가: 2026-08-18에 네 가지가 한 판에서 새어 나갔고 네 번 다 사용자가
// 발견했다 - 정해졌다고 말해 놓고 안 넣은 라벨, 영문 릴리즈 노트에 섞인 한글,
// 옛 이름이 남은 문서, 버전이 뒤처진 디버그 빌드. 그때 검사 스크립트를 네 개
// 짰다가 임시 폴더째로 버렸다. 버리지 않고 여기 둔다.
//
// 이 파일의 값어치는 검사 항목이 아니라 RETIRED 목록에 있다. 이름을 바꿀
// 때마다 옛 이름을 거기 한 줄 넣으면, 그 이름이 어디서 되살아나든 걸린다.
// 사람이 기억할 필요가 없어지는 것이 요점이다.
//
// 오탐이 나면 그 자리에서 고칠 것. 양치기 검사기는 아무도 안 본다.

import fs from 'node:fs'
import path from 'node:path'

const ROOT = path.resolve(import.meta.dirname, '..')
const R = (p) => path.join(ROOT, p)
const read = (p) => fs.readFileSync(R(p), 'utf8')
const HANGUL = /[가-힣]/

let failed = 0
const report = (name, hits, note) => {
  if (!hits.length) { console.log('  OK    ' + name); return }
  failed++
  console.log('  걸림  ' + name + '  (' + hits.length + '건)' + (note ? '  - ' + note : ''))
  hits.slice(0, 12).forEach((h) => console.log('          ' + h))
  if (hits.length > 12) console.log('          ... 그 밖 ' + (hits.length - 12) + '건')
}

// ---------------------------------------------------------------- 은퇴한 이름
//
// 바뀐 이름이 문서나 코드에 되살아나는지 본다. 변경 이력은 제외한다 - 그때의
// 기록이므로 옛 이름이 있는 것이 맞다.
//
// 이름을 바꿀 때 여기 한 줄 추가하는 것이 이 스크립트를 쓰는 방법이다.
const RETIRED = [
  ['자름맞춤', '채우기', '2026-08-17'],
  ['이미지 뷰어', '멀티미디어 패널', '2026-08-12'],
  ['필름스트립', '썸네일 바', '2026-08-14'],
  ['전체 덮기', '앱 전체화면', '2026-08-16'],
  ['조작 막대', '컨트롤 패널', '2026-08-16'],
  ['표시할 파일 종류', '표시할 파일 형식', '2026-08-18'],
  ['바탕화면 전체', '바탕화면 채우기', '2026-08-18'],
  ['제목 표시줄 타이틀', '제목 표시줄 텍스트', '2026-08-18'],
  ['Accordion Mode', 'Auto-Collapse Folders', '2026-08-18'],
  ['꺽쇠', '펼침기호', '2026-08-18'],
  ['아코디언', '폴더 자동 접기', '2026-08-18'],
  ['탭 간격', '들여쓰기 간격', '2026-08-18'],
]

// 변경 이력 아래는 과거의 기록이므로 잘라 낸다.
function livingPart(file, marker) {
  const lines = read(file).split(/\r?\n/)
  const cut = marker ? lines.findIndex((l) => l.startsWith(marker)) : -1
  return (cut < 0 ? lines : lines.slice(0, cut)).map((l, i) => [i + 1, l])
}

{
  const targets = [
    ['src/Edgetree/Services/Strings.cs', null],
    ['src/Edgetree/Services/HelpContent.cs', null],
    ['README.md', '## Changelog'],
    ['README-ko.md', '## 변경 이력'],
    ['landing/src/i18n.ts', null],
  ]
  const hits = []
  for (const [file, marker] of targets) {
    for (const [ln, line] of livingPart(file, marker)) {
      if (line.trim().startsWith('//')) continue // 주석은 옛 이름을 적어 두는 자리다
      for (const [old, now, when] of RETIRED) {
        if (line.includes(old)) hits.push(file + ':' + ln + '  "' + old + '" -> "' + now + '" (' + when + ')')
      }
    }
  }
  report('은퇴한 이름이 되살아났는가', hits, '변경 이력과 주석은 제외')
}

// ------------------------------------------------------------ 영문에 한글
//
// 영문 화면에 한글이 찍히던 것이 v2.4.1에서 고친 것 중 하나였다. 다시 생기면
// 여기서 걸린다. 일부러 두 언어에 같게 두는 둘은 통과시킨다.
const BILINGUAL_ON_PURPOSE = ['MenuLanguage', 'LanguageRestartNote']
{
  const src = read('src/Edgetree/Services/Strings.cs').split(/\r?\n/)
  const enStart = src.findIndex((l) => /^\s*IsEnglish = true;/.test(l))
  const hits = []
  if (enStart < 0) {
    hits.push('영문 구역을 못 찾음 - Strings.cs 구조가 바뀌었는지 확인할 것')
  } else {
    src.slice(enStart).forEach((line, i) => {
      if (line.trim().startsWith('//')) return
      if (!HANGUL.test(line)) return
      if (BILINGUAL_ON_PURPOSE.some((k) => line.includes(k))) return
      hits.push('Strings.cs:' + (enStart + i + 1) + '  ' + line.trim().slice(0, 70))
    })
  }
  report('영문 문자열에 한글', hits)
}
{
  // R(ko, ko, en, en) / T(ko, en) 의 영문 인자만 본다. 소스를 정규식으로 훑을 때
  // 가장 놓치기 쉬운 자리라 일부러 여기 둔다 - 실제로 두 줄이 이렇게 새어 나갔다.
  //
  // 인자 목록을 정규식으로 자르면 여러 줄짜리 호출에서 다음 호출까지 딸려온다
  // (처음에 그렇게 짰다가 오탐 9건이 났다). 괄호를 세어 호출 하나만 떼어낸다.
  const s = read('src/Edgetree/Services/HelpContent.cs')
  const hits = []
  const bodies = (name) => {
    const out = []
    const re = new RegExp('\\b' + name + '\\(', 'g')
    let m
    while ((m = re.exec(s))) {
      let i = m.index + m[0].length, depth = 1, inStr = false
      const start = i
      while (i < s.length && depth > 0) {
        const c = s[i]
        if (inStr) {
          if (c === '\\') i++
          else if (c === '"') inStr = false
        } else if (c === '"') inStr = true
        else if (c === '(') depth++
        else if (c === ')') depth--
        i++
      }
      out.push(s.slice(start, i - 1))
    }
    return out
  }
  const STR = /"((?:[^"\\]|\\.)*)"/g
  for (const [name, from, to] of [['R', 2, 4], ['T', 1, 2]]) {
    for (const body of bodies(name)) {
      const args = [...body.matchAll(STR)].map((x) => x[1])
      if (args.length < to) continue
      for (const a of args.slice(from, to)) {
        if (HANGUL.test(a)) hits.push('HelpContent.cs  ' + name + '(...)  영문 인자: ' + a.slice(0, 60))
      }
    }
  }
  report('F1 영문 인자에 한글', hits)
}
{
  const ts = read('landing/src/i18n.ts')
  const i = ts.indexOf('\n  en: {')
  const hits = []
  if (i < 0) hits.push('랜딩 en 블록을 못 찾음')
  else ts.slice(i).split(/\r?\n/).forEach((line) => {
    if (line.trim().startsWith('//')) return
    if (HANGUL.test(line)) hits.push('i18n.ts(en)  ' + line.trim().slice(0, 70))
  })
  report('랜딩 영문에 한글', hits)
}

// ------------------------------------------------------------ 영국식 철자
//
// 앱은 미국식으로 통일돼 있다 - 창 제목이 Color Settings 이고 파일이
// edgetree-colors.json 이라 그쪽이 기존 결정이다. 주석은 대상이 아니다.
const BRIT = /\b(colours?|coloured|behaviour\w*|favourite\w*|minimis\w+|maximis\w+|customis\w+|organis\w+|recognis\w+|licence|defence|centre\w*|greyscale|grey|cancelled|labelled|travelling|programme|catalogue)\b/gi
{
  const hits = []
  const scan = (file, marker) => {
    for (const [ln, line] of livingPart(file, marker)) {
      if (line.trim().startsWith('//')) continue
      if (/Apache Licen[cs]e|MIT Licen[cs]e/i.test(line)) continue
      // .cs 는 문자열 리터럴 안만, .md 는 줄 전체
      const text = file.endsWith('.cs')
        ? [...line.matchAll(/"((?:[^"\\]|\\.)*)"/g)].map((m) => m[1]).join(' ')
        : line
      for (const m of text.matchAll(BRIT)) hits.push(file + ':' + ln + '  ' + m[0])
    }
  }
  scan('src/Edgetree/Services/Strings.cs', null)
  scan('src/Edgetree/Services/HelpContent.cs', null)
  scan('README.md', '## Changelog')
  report('영국식 철자 (나가는 글)', hits, '변경 이력·주석 제외')
}

// -------------------------------------------------- 앱 밖에 박힌 사용자 텍스트
//
// 사용자에게 보이는 글은 Strings.cs 와 HelpContent.cs 에만 있어야 한다.
// 2026-08-18에 전수 확인한 결과이고, 새로 생기면 검수 범위가 조용히 넓어진다.
//
// 속성 이름 앞의 공백을 요구하는 것은 SizeToContent="Height" 와
// InputGestureText="Space" 가 Content=/Text= 로 걸리기 때문이다.
{
  const KEY_NAME = /^(Space|Home|End|Insert|Tab|PgUp|PgDn|Backspace)$/
  const ALLOWED = /한국어|English|Ctrl|Alt|Shift|Del|Enter|Esc|Edgetree|TabStick|https?:|vercel|github/
  const hits = []
  for (const f of ['src/Edgetree/MainWindow.xaml', 'src/Edgetree/AboutWindow.xaml',
                   'src/Edgetree/ColorSettingsWindow.xaml', 'src/Edgetree/HelpWindow.xaml',
                   'src/Edgetree/FilterExtensionsWindow.xaml', 'src/Edgetree/PresetNameWindow.xaml']) {
    if (!fs.existsSync(R(f))) continue
    read(f).split(/\r?\n/).forEach((line, i) => {
      if (line.includes('<!--')) return
      for (const m of line.matchAll(/\s(?:Content|Text|Header|ToolTip|Title)="([^"{][^"]*)"/g)) {
        const v = m[1].trim()
        if (!HANGUL.test(v) && !/[A-Za-z]{3}/.test(v)) continue
        if (ALLOWED.test(v) || KEY_NAME.test(v) || /^F\d\d?$/.test(v)) continue
        hits.push(path.basename(f) + ':' + (i + 1) + '  "' + v.slice(0, 50) + '"')
      }
    })
  }
  report('Strings.cs 밖에 박힌 화면 텍스트', hits)
}

// ------------------------------------------------------------------ 버전
//
// 2026-08-18: 버전을 올린 뒤 Debug 를 다시 안 만들고 띄워서, 사용자가 2.4.0
// 으로 뜬다고 알려 주었다. 빌드가 csproj 보다 오래됐는지 본다.
{
  const csproj = read('src/Edgetree/Edgetree.csproj').match(/<Version>([^<]+)<\/Version>/)?.[1]
  const hits = []
  console.log('\n  csproj 버전  ' + csproj)
  for (const [label, p] of [['Debug  ', 'src/Edgetree/bin/Debug/net8.0-windows/Edgetree.dll'],
                            ['Release', 'src/Edgetree/bin/Release/net8.0-windows/Edgetree.dll']]) {
    if (!fs.existsSync(R(p))) { console.log('  ' + label + ' 빌드 없음'); continue }
    const buf = fs.readFileSync(R(p)).toString('latin1')
    const v = buf.match(/(\d+\.\d+\.\d+)\+[0-9a-f]{40}/)?.[1] ?? '(못 읽음)'
    console.log('  ' + label + ' 빌드  ' + v)
    if (v !== csproj) hits.push(label.trim() + ' 빌드가 ' + v + ' - csproj 는 ' + csproj + '. 다시 빌드할 것')
  }
  report('빌드가 csproj 버전과 같은가', hits)
}

console.log(failed === 0 ? '\n전부 통과.' : '\n' + failed + '가지 걸림.')
process.exit(failed === 0 ? 0 : 1)
