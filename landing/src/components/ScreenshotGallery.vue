<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { t } from '../i18n'

// UNCROPPED 1920x1080, every one of them, and that is what the section is for
// rather than an accident of how they were taken: how much of a real screen the
// sidebar takes is the thing a card-sized crop cannot show. So the thumbnail is
// the whole screen scaled down, and the full size is that screen at 1:1.
//
// A SECTION, not a page of its own. It was built as a second HTML entry first
// (2026-08-15) and the author found moving off the page and back a nuisance for
// what is, in the end, more of what the page above is already showing.
const COUNT = 16
const shots = Array.from({ length: COUNT }, (_, i) => String(i + 1).padStart(2, '0'))

const open = ref<number | null>(null)

function show(i: number) {
  open.value = i
}

function close() {
  open.value = null
}

function step(by: number) {
  if (open.value === null) {
    return
  }
  open.value = (open.value + by + COUNT) % COUNT
}

function onKey(e: KeyboardEvent) {
  if (open.value === null) {
    return
  }
  if (e.key === 'Escape') {
    close()
  } else if (e.key === 'ArrowRight') {
    step(1)
  } else if (e.key === 'ArrowLeft') {
    step(-1)
  }
}

onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <section id="shots">
    <div class="container">
      <!-- No line under the title. One was written ("눌러서 원본 크기로") on the
           argument that a grid of thumbnails does not announce it opens at full
           size, and the author cut it: the cursor already turns to zoom-in over
           every tile, which says the same thing at the moment it is needed and
           without a sentence on a section whose rule is 말보다 화면. -->
      <div class="section-heading">
        <h2>{{ t.gallery.title }}</h2>
      </div>

      <div class="grid">
        <button
          v-for="(n, i) in shots"
          :key="n"
          type="button"
          class="shot"
          @click="show(i)"
        >
          <img
            :src="`/shots/t-${n}.webp`"
            width="640"
            height="360"
            alt=""
            loading="lazy"
            decoding="async"
          />
        </button>
      </div>
    </div>

    <!-- The overlay draws the picture at its NATURAL size and scrolls if the
         window cannot hold it, rather than fitting it to the viewport. Fitting
         would undo the one thing this section is for - on a 1920-wide screen
         the shot lands pixel for pixel on the screen it was taken from. -->
    <div v-if="open !== null" class="viewer" @click.self="close">
      <img :src="`/shots/f-${shots[open]}.webp`" width="1920" height="1080" alt="" />

      <button type="button" class="nav prev" @click.stop="step(-1)" aria-label="Previous">‹</button>
      <button type="button" class="nav next" @click.stop="step(1)" aria-label="Next">›</button>
      <button type="button" class="nav close" @click.stop="close" aria-label="Close">×</button>
      <span class="count">{{ open + 1 }} / {{ COUNT }}</span>
    </div>
  </section>
</template>

<style scoped>
/* FOUR ACROSS, three rows. Twelve 16:9 shots at four columns is the widest the
   grid goes before a thumbnail stops showing which app is on screen. */
.grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.shot {
  padding: 0;
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
  background: var(--bg-card);
  cursor: zoom-in;
  display: block;
  line-height: 0;
  transition: border-color 0.15s ease, transform 0.15s ease;
}

.shot:hover {
  border-color: var(--accent);
  transform: translateY(-2px);
}

.shot img {
  width: 100%;
  height: auto;
  display: block;
}

@media (max-width: 1100px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 620px) {
  .grid {
    grid-template-columns: 1fr;
  }
}

.viewer {
  position: fixed;
  inset: 0;
  z-index: 50;
  background: rgba(0, 0, 0, 0.92);
  overflow: auto;
  display: grid;
  place-items: center;
  cursor: zoom-out;
}

/* Natural size - see the template's comment. max-width: none is what stops the
   page's own image rule from shrinking it back to the viewport. */
.viewer img {
  width: 1920px;
  max-width: none;
  height: auto;
  display: block;
}

/* Below 1920 the shot no longer fits, and scrolling a screenshot sideways to
   read it is worse than seeing it whole - so the narrow end fits to width. The
   1:1 promise is kept exactly where it can be honoured. */
@media (max-width: 1919px) {
  .viewer img {
    width: 100%;
  }
}

.nav {
  position: fixed;
  background: rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.15);
  color: #fff;
  border-radius: 8px;
  cursor: pointer;
  font-size: 1.6rem;
  line-height: 1;
  padding: 10px 16px;
}

.nav:hover {
  background: rgba(0, 0, 0, 0.8);
}

.prev {
  left: 16px;
  top: 50%;
  transform: translateY(-50%);
}

.next {
  right: 16px;
  top: 50%;
  transform: translateY(-50%);
}

.close {
  right: 16px;
  top: 16px;
  font-size: 1.4rem;
}

.count {
  position: fixed;
  left: 50%;
  bottom: 16px;
  transform: translateX(-50%);
  color: rgba(255, 255, 255, 0.7);
  font-size: 0.9rem;
  font-variant-numeric: tabular-nums;
}
</style>
