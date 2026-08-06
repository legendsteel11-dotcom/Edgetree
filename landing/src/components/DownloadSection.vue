<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { t } from '../i18n'
import { track } from '@vercel/analytics'
import UpdateNotes from './UpdateNotes.vue'
import { standardDownloadUrl, standaloneDownloadUrl, setupDownloadUrl, releaseVersion, ensureReleaseAssetsLoaded, trackDownload } from '../releaseAssets'

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

      <!-- Above the buttons on purpose: it is the one place someone is already
           deciding whether to take the file, and "what changed" belongs there
           rather than in a section further down that nobody scrolls to. -->
      <UpdateNotes />

      <div class="mobile-note">
        <p>{{ t.download.mobileTitle }}<br>{{ t.download.mobileDesc }}</p>
        <div class="mobile-copy">
          <code>{{ siteUrl }}</code>
          <button type="button" class="btn btn-primary" @click="copyAddress">
            {{ copied ? t.download.mobileCopied : t.download.mobileCopy }}
          </button>
        </div>
      </div>

      <!-- Three builds, but not three equal cards: about 80% of the other app's
           downloads take its installer, so this one takes the room and the two
           alternatives sit under it a size down. Three side by side would ask
           the reader to weigh 155 MB against 49 MB before they know what either
           means. -->
      <a class="lead" :href="setupDownloadUrl" target="_blank" rel="noopener"
              @click="trackDownload('setup', 'download')">
        <span class="lead-body">
          <span class="lead-title">
            {{ t.download.setupTitle }}
            <span class="badge">{{ t.download.recommend }}</span>
          </span>
          <span class="lead-desc">{{ t.download.setupDesc }}</span>
        </span>
        <span class="lead-cta">
          <span class="cta-label">{{ t.download.button }}</span>
          <span class="size">{{ t.download.setupSize }}</span>
        </span>
      </a>

      <p v-if="releaseVersion" class="version">{{ releaseVersion }}</p>

      <div class="grid">
        <a class="card" :href="standaloneDownloadUrl" target="_blank" rel="noopener"
                @click="trackDownload('standalone', 'download')">
          <span class="card-title">{{ t.download.portableTitle }}</span>
          <span class="card-desc">{{ t.download.portableDesc }}</span>
          <span class="card-cta">{{ t.download.button }} · {{ t.download.portableSize }}</span>
        </a>
        <a class="card" :href="standardDownloadUrl" target="_blank" rel="noopener"
                @click="trackDownload('standard', 'download')">
          <span class="card-title">{{ t.download.lightTitle }}</span>
          <span class="card-desc">{{ t.download.lightDesc }}</span>
          <span class="card-cta">{{ t.download.button }} · {{ t.download.lightSize }}</span>
        </a>
      </div>

      <!-- Reads as a tip for someone who uses both apps, not as an apology for
           the runtime: install it once and the small build opens up on both
           sides. Mirrors the same line on TabStick's landing. -->
      <p class="note">{{ t.download.bothApps }}</p>

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

/* The lead card. One accent-filled row: title, what it does, and the button
   built into the card itself so the whole thing is the target. */
.lead {
  display: flex;
  align-items: center;
  gap: 20px;
  max-width: 720px;
  margin: 0 auto;
  padding: 22px 26px;
  border-radius: 12px;
  background: var(--accent-bg);
  border: 1px solid var(--accent);
  text-decoration: none;
  color: inherit;
  transition: border-color 0.15s ease, transform 0.15s ease;
}

.lead:hover {
  border-color: var(--accent-strong);
  transform: translateY(-1px);
}

.lead-body {
  flex: 1;
  min-width: 0;
}

.lead-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 17px;
  color: var(--text-strong);
  margin-bottom: 6px;
}

.badge {
  font-size: 11.5px;
  font-weight: 700;
  letter-spacing: 0.02em;
  padding: 2px 7px;
  border-radius: 999px;
  background: var(--accent);
  color: #fff;
}

.lead-desc {
  display: block;
  font-size: 14px;
  line-height: 1.6;
}

.lead-cta {
  flex: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.cta-label {
  padding: 9px 20px;
  border-radius: 8px;
  background: var(--accent);
  color: #fff;
  font-size: 14px;
  white-space: nowrap;
}

.size {
  font-size: 12px;
  opacity: 0.7;
}

/* The version belongs to the lead card above it - the two alternatives carry
   the same one, and repeating it three times says nothing three times. */
.version {
  max-width: 720px;
  margin: 8px auto 0;
  text-align: right;
  font-size: 12.5px;
  opacity: 0.55;
}

.grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  max-width: 720px;
  margin: 16px auto 0;
}

/* A size down from the lead in every direction: no fill, smaller type, the
   button reduced to a line of text. They are alternatives, not rivals. */
.card {
  display: block;
  background: var(--bg-card);
  border: 1px solid transparent;
  border-radius: 12px;
  padding: 20px 22px;
  text-decoration: none;
  color: inherit;
  transition: border-color 0.15s ease;
}

.card:hover {
  border-color: var(--border);
}

.card-title {
  display: block;
  font-size: 15px;
  color: var(--text-strong);
  margin-bottom: 4px;
}

.card-desc {
  display: block;
  font-size: 13.5px;
  line-height: 1.6;
  margin-bottom: 12px;
}

.card-cta {
  display: block;
  font-size: 13px;
  color: var(--accent-strong);
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

  /* The lead card stacks rather than squeezing its button off the edge. */
  .lead {
    flex-direction: column;
    align-items: stretch;
    text-align: center;
    gap: 14px;
  }

  .lead-title {
    justify-content: center;
  }

  .version {
    text-align: center;
  }
}
</style>
