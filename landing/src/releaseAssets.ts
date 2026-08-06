import { ref } from 'vue'
import { track } from '@vercel/analytics'

// Shared by HeroSection and DownloadSection so both sets of download
// buttons resolve to the same direct file links from a single API call
// (not one fetch per component instance).
export const RELEASE_URL = 'https://github.com/legendsteel11/Edgetree/releases/latest'
const RELEASE_API_URL = 'https://api.github.com/repos/legendsteel11/Edgetree/releases/latest'

// Both start pointed at the releases page and only swap to a direct
// per-file link once the actual latest-release asset list loads - a safe
// fallback if the API call fails (rate-limited, offline, GitHub down, ...)
// rather than a broken link. Asset filenames carry the version number
// (Edgetree-v1.0.4-win-x64.exe), so they can't be hardcoded here without
// going stale every release - matching by the fixed suffix instead means
// this never needs touching again as new versions ship.
export const standardDownloadUrl = ref(RELEASE_URL)
export const standaloneDownloadUrl = ref(RELEASE_URL)
// The installer, from v1.6.0 on. Falls back to the releases page like the other
// two, which also covers every release BEFORE it existed - an older latest
// release simply has no -setup.exe among its assets and the button still lands
// somewhere useful.
export const setupDownloadUrl = ref(RELEASE_URL)
// Empty until the API call resolves - DownloadSection only renders the
// version line once this has a value, rather than showing a placeholder.
export const releaseVersion = ref('')

let loadStarted = false

export function ensureReleaseAssetsLoaded() {
  if (loadStarted) {
    return
  }
  loadStarted = true

  fetch(RELEASE_API_URL)
    .then((res) => (res.ok ? res.json() : null))
    .then((release) => {
      const assets: { name: string; browser_download_url: string }[] = release?.assets ?? []
      const standalone = assets.find((a) => a.name.endsWith('-standalone.exe'))
      const standard = assets.find((a) => a.name.endsWith('-win-x64.exe') && !a.name.endsWith('-standalone.exe'))
      // Edgetree-v1.6.0-win-x64-setup.exe. It does not end in '-win-x64.exe',
      // so the standard match above cannot pick it up by accident.
      const setup = assets.find((a) => a.name.endsWith('-setup.exe'))

      if (standard) standardDownloadUrl.value = standard.browser_download_url
      if (standalone) standaloneDownloadUrl.value = standalone.browser_download_url
      if (setup) setupDownloadUrl.value = setup.browser_download_url
      if (release?.tag_name) releaseVersion.value = release.tag_name
    })
    .catch(() => {
      // Leave both pointed at the releases page - already a working fallback.
    })
}

// Which build was taken, and from where on the page.
//
// Until now the only figure was GitHub's own download count, compared by eye
// against visitors. Those two never lined up into a rate: the counts restart
// with every release and include whatever crawls the API, while the visitors
// are a different window entirely. Recorded here, a visit and a download sit
// in the same picture. Event and property names match TabStick's landing so
// the two read the same way side by side.
//
// `where` is this side's own addition: both the hero and the download section
// carry the same pair of buttons, and which of them people actually use is
// worth knowing before moving anything around again.
//
// The link is left alone - no preventDefault. A download link doesn't navigate
// away, so there is no race between sending this and the browser starting the
// file.
// 'setup' joins the two from v1.6.0. The existing names are left alone so the
// dashboard's history stays comparable across the change.
export function trackDownload(build: 'standalone' | 'standard' | 'setup', where: 'hero' | 'download') {
  track('download', { build, where, version: releaseVersion.value || 'unknown' })
}
