<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { lang, t } from '../i18n'
import { changelog } from '../changelog'
import { releaseVersion, ensureReleaseAssetsLoaded } from '../releaseAssets'

onMounted(ensureReleaseAssetsLoaded)

const index = ref(0)
const entry = computed(() => changelog[index.value])
const lines = computed(() => (lang.value === 'en' ? entry.value.en : entry.value.ko))

// Shown unless we KNOW the list has fallen behind: if GitHub reports a latest
// version this card doesn't lead with, the lines below are describing something
// other than what the buttons underneath hand over, and saying nothing beats
// saying the wrong thing. An API call that failed or hasn't landed yet leaves
// releaseVersion empty, which is not evidence of anything - the card stays.
//
// The dev server is exempt. The guard is about what the PUBLISHED page claims,
// and the notes for a release are written before that release exists - which is
// exactly when they need looking at. Without this, drafting them locally shows
// an empty space where the card is meant to be.
const isCurrent = computed(() =>
  import.meta.env.DEV ||
  releaseVersion.value === '' || releaseVersion.value === changelog[0].version)

function step(by: number) {
  const next = index.value + by
  if (next >= 0 && next < changelog.length) {
    index.value = next
  }
}
</script>

<template>
  <div v-if="isCurrent" class="notes">
    <div class="notes-head">
      <span class="version">{{ entry.version }} {{ t.updates.title }}</span>
      <!-- Older entries are reachable but never in the way: the arrows sit next
           to the version, and the one at either end simply stops working rather
           than disappearing (a control that vanishes moves the other one). -->
      <span class="nav">
        <button type="button"
                :disabled="index >= changelog.length - 1"
                :aria-label="t.updates.older"
                @click="step(1)">‹</button>
        <button type="button"
                :disabled="index <= 0"
                :aria-label="t.updates.newer"
                @click="step(-1)">›</button>
      </span>
    </div>
    <ul>
      <li v-for="line in lines" :key="line">{{ line }}</li>
    </ul>
  </div>
</template>

<style scoped>
.notes {
  background: var(--bg-card);
  border-radius: 12px;
  padding: 22px 26px;
  /* Same 720px lane as the two download cards below (see DownloadSection's
     .grid) - this card belongs to that block, and spanning wider than the thing
     it describes made it read as a separate section. */
  max-width: 720px;
  margin: 0 auto 24px;
}

.notes-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.version {
  color: var(--accent-strong);
  font-size: 14px;
  font-weight: 700;
}

.nav {
  display: inline-flex;
  gap: 2px;
}

.nav button {
  background: none;
  border: none;
  color: var(--accent-strong);
  cursor: pointer;
  font-size: 15px;
  line-height: 1;
  padding: 2px 5px;
}

.nav button:disabled {
  /* The only place a control is faded here - it is a disabled button, not text
     being marked as secondary. */
  opacity: 0.35;
  cursor: default;
}

.notes ul {
  margin: 0;
  padding-left: 20px;
}

.notes li {
  font-size: 14px;
  line-height: 1.7;
  /* So a line in changelog.ts can break itself where the sense breaks - the
     v2.0.0 music entry carries an examples clause that reads as a second
     thought rather than more of the first. Note that pre-line COLLAPSES
     ordinary spaces, so the indent on such a line is written with non-breaking
     spaces ( ) in the string itself. */
  white-space: pre-line;
}
</style>
