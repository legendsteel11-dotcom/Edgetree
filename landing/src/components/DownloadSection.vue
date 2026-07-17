<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { t } from '../i18n'

const RELEASE_URL = 'https://github.com/legendsteel11-dotcom/Edgetree/releases/latest'
const RELEASE_API_URL = 'https://api.github.com/repos/legendsteel11-dotcom/Edgetree/releases/latest'

// Both buttons start pointed at the releases page (same as before) and only
// swap to a direct per-file link once the actual latest-release asset list
// loads - a safe fallback if the API call fails (rate-limited, offline,
// GitHub down, ...) rather than a broken link. Asset filenames carry the
// version number (Edgetree-v1.0.4-win-x64.exe), so they can't be
// hardcoded here without going stale on every release - matching by the
// fixed suffix instead means this never needs touching again as new
// versions ship.
const standardUrl = ref(RELEASE_URL)
const standaloneUrl = ref(RELEASE_URL)

onMounted(async () => {
  try {
    const res = await fetch(RELEASE_API_URL)
    if (!res.ok) return
    const release = await res.json()
    const assets: { name: string; browser_download_url: string }[] = release.assets ?? []

    const standalone = assets.find((a) => a.name.endsWith('-standalone.exe'))
    const standard = assets.find((a) => a.name.endsWith('-win-x64.exe') && !a.name.endsWith('-standalone.exe'))

    if (standard) standardUrl.value = standard.browser_download_url
    if (standalone) standaloneUrl.value = standalone.browser_download_url
  } catch {
    // Leave both pointed at the releases page - already a working fallback.
  }
})
</script>

<template>
  <section id="download" class="alt">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.download.title }}</h2>
        <p>{{ t.download.subtitle }}</p>
      </div>

      <div class="grid">
        <div class="card">
          <h3>{{ t.download.standardTitle }}</h3>
          <p>{{ t.download.standardDesc }}</p>
          <a class="btn btn-secondary" :href="standardUrl" target="_blank" rel="noopener">{{ t.download.button }}</a>
        </div>
        <div class="card highlight">
          <h3>{{ t.download.standaloneTitle }}</h3>
          <p>{{ t.download.standaloneDesc }}</p>
          <a class="btn btn-primary" :href="standaloneUrl" target="_blank" rel="noopener">{{ t.download.button }}</a>
        </div>
      </div>

      <p class="note">{{ t.download.note }}</p>
    </div>
  </section>
</template>

<style scoped>
.alt {
  background: var(--bg-alt);
  border-top: 1px solid var(--border);
  border-bottom: 1px solid var(--border);
}

.grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 24px;
  max-width: 720px;
  margin: 0 auto;
}

.card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 32px;
  text-align: center;
}

.card.highlight {
  border-color: var(--accent);
}

.card h3 {
  font-size: 19px;
  margin-bottom: 8px;
}

.card p {
  font-size: 14px;
  margin-bottom: 20px;
}

.note {
  text-align: center;
  margin-top: 28px;
  font-size: 13.5px;
  opacity: 0.75;
}

@media (max-width: 600px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
