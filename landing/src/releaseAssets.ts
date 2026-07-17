import { ref } from 'vue'

// Shared by HeroSection and DownloadSection so both sets of download
// buttons resolve to the same direct file links from a single API call
// (not one fetch per component instance).
export const RELEASE_URL = 'https://github.com/legendsteel11-dotcom/Edgetree/releases/latest'
const RELEASE_API_URL = 'https://api.github.com/repos/legendsteel11-dotcom/Edgetree/releases/latest'

// Both start pointed at the releases page and only swap to a direct
// per-file link once the actual latest-release asset list loads - a safe
// fallback if the API call fails (rate-limited, offline, GitHub down, ...)
// rather than a broken link. Asset filenames carry the version number
// (Edgetree-v1.0.4-win-x64.exe), so they can't be hardcoded here without
// going stale every release - matching by the fixed suffix instead means
// this never needs touching again as new versions ship.
export const standardDownloadUrl = ref(RELEASE_URL)
export const standaloneDownloadUrl = ref(RELEASE_URL)

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

      if (standard) standardDownloadUrl.value = standard.browser_download_url
      if (standalone) standaloneDownloadUrl.value = standalone.browser_download_url
    })
    .catch(() => {
      // Leave both pointed at the releases page - already a working fallback.
    })
}
