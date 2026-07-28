<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { t } from '../i18n'
import { track } from '@vercel/analytics'
import { standardDownloadUrl, standaloneDownloadUrl, releaseVersion, ensureReleaseAssetsLoaded, trackDownload } from '../releaseAssets'

onMounted(ensureReleaseAssetsLoaded)

// Hard-coded rather than read from location.host: the point is to hand over
// the address of the published site, which is not where the visitor
// necessarily is (a LAN dev server, a preview deployment).
const siteUrl = 'edgetree.vercel.app'

const copied = ref(false)
let resetTimer: number | undefined

async function copyAddress() {
  // A phone visitor showing they intend to carry on at a PC. Close to half of
  // all visitors arrive on one and none of them can install from there, so
  // whether this button gets pressed is the measure of whether that half is
  // being caught at all - until now there was no way to know it was ever used.
  track('copy_link')

  const text = `https://${siteUrl}`
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
    } else {
      // The Clipboard API is https-only, and this box is aimed squarely at
      // phones - including one browsing a plain-http dev server on the LAN.
      // execCommand is deprecated but is what still works there.
      const field = document.createElement('textarea')
      field.value = text
      field.setAttribute('readonly', '')
      field.style.position = 'fixed'
      field.style.opacity = '0'
      document.body.appendChild(field)
      field.select()
      document.execCommand('copy')
      document.body.removeChild(field)
    }
    copied.value = true
    window.clearTimeout(resetTimer)
    resetTimer = window.setTimeout(() => (copied.value = false), 1800)
  } catch {
    // Nothing to rescue: the address is on screen and selectable by hand.
  }
}
</script>

<template>
  <section id="download" class="alt">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.download.title }}</h2>
      </div>

      <div class="mobile-note">
        <p>{{ t.download.mobileTitle }}<br>{{ t.download.mobileDesc }}</p>
        <div class="mobile-copy">
          <code>{{ siteUrl }}</code>
          <button type="button" class="btn btn-primary" @click="copyAddress">
            {{ copied ? t.download.mobileCopied : t.download.mobileCopy }}
          </button>
        </div>
      </div>

      <div class="grid">
        <div class="card">
          <h3>{{ t.download.standardTitle }}</h3>
          <p>{{ t.download.standardDesc }}</p>
          <p v-if="releaseVersion" class="version">{{ releaseVersion }}</p>
          <a class="btn btn-secondary" :href="standardDownloadUrl" target="_blank" rel="noopener"
                  @click="trackDownload('standard', 'download')">{{ t.download.button }}</a>
        </div>
        <div class="card highlight">
          <h3>{{ t.download.standaloneTitle }}</h3>
          <p>{{ t.download.standaloneDesc }}</p>
          <p v-if="releaseVersion" class="version">{{ releaseVersion }}</p>
          <a class="btn btn-primary" :href="standaloneDownloadUrl" target="_blank" rel="noopener"
                  @click="trackDownload('standalone', 'download')">{{ t.download.button }}</a>
        </div>
      </div>

      <p class="note">{{ t.download.note }}</p>

      <div class="disclaimers">
        <p>{{ t.download.smartscreenNote }}</p>
        <p>{{ t.download.virustotalNote }}</p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.alt {
  background: var(--bg-alt);
  /* border-top: 1px solid var(--border);
  border-bottom: 1px solid var(--border); */
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
  /* border: 1px solid var(--border); */
  border-radius: 12px;
  padding: 32px;
  text-align: center;
}

.card.highlight {
  border-color: var(--accent);
}

.card h3 {
  font-size: 16px;
  font-weight: 400;
  margin-bottom: 8px;
}

.card p {
  font-size: 14px;
  margin-bottom: 20px;
}

.card p.version {
  font-size: 12.5px;
  opacity: 0.55;
  margin-top: -12px;
}

.note {
  text-align: center;
  margin-top: 28px;
  font-size: 13.5px;
  opacity: 0.75;
}

.disclaimers {
  max-width: 560px;
  margin: 16px auto 0;
  text-align: center;
}

.disclaimers p {
  font-size: 12.5px;
  line-height: 1.6;
  opacity: 0.55;
  white-space: pre-line;
}

.disclaimers p + p {
  margin-top: 6px;
}

/* Nearly half of all visitors arrive on a phone (measured), where every
   download button below is a dead end - the file is a Windows exe. Rather
   than let them tap one and get a useless file, hand them the address to
   reopen on a PC. Hidden on desktop, where it would only be noise. */
.mobile-note {
  display: none;
  max-width: 560px;
  margin: 0 auto 28px;
  padding: 20px;
  border-radius: 12px;
  background: var(--accent-bg);
  text-align: center;
}

.mobile-note p {
  font-size: 14px;
  line-height: 1.65;
  color: var(--text-strong);
  margin-bottom: 16px;
}

.mobile-copy {
  display: flex;
  align-items: stretch;
  justify-content: center;
  gap: 8px;
}

.mobile-copy code {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 12px;
  border-radius: 8px;
  background: var(--bg-card);
  font-size: 13.5px;
  color: var(--text-strong);
  /* The address is the payload - let it shrink rather than wrap or clip. */
  overflow-wrap: anywhere;
}

.mobile-copy .btn {
  flex: none;
  padding: 10px 16px;
  font-size: 13.5px;
  white-space: nowrap;
}

/* Touch pointers get it at any width - a tablet can't run the exe either. */
@media (pointer: coarse), (max-width: 600px) {
  .mobile-note {
    display: block;
  }
}

@media (max-width: 600px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
