<script setup lang="ts">
import { computed } from 'vue'
import { t } from '../i18n'

// The flag lives on the entry itself in i18n.ts, next to the copy, rather than
// as a list of positions here - this list is edited by hand and reordered, and
// picking rows by index is the pattern that has silently broken things in this
// project before. Only some entries carry it, so it is read defensively and
// normalised to a boolean once.
const items = computed(() =>
  t.value.features.items.map((item) => ({
    title: item.title,
    desc: item.desc,
    highlight: 'highlight' in item && item.highlight === true,
  })))
</script>

<template>
  <section id="features" class="alt">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.features.title }}</h2>
      </div>

      <div class="grid">
        <div v-for="item in items" :key="item.title" class="item">
          <h3 :class="{ 'is-highlight': item.highlight }">{{ item.title }}</h3>
          <p>{{ item.desc }}</p>
        </div>
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
  grid-template-columns: repeat(3, 1fr);
  gap: 24px 32px;
}

.item {
  padding-left: 16px;
  border-left: 2px solid var(--border);
    border-left: 2px solid #224464;
}

.item h3 {
  font-size: 16px;
  font-weight: 400;
  margin-bottom: 6px;
  color: var(--text-strong);
}

/* A few titles lifted out of the twenty. Colour only - no bold, no size change:
   the list reads as one block and a heavier weight would break the grid's rhythm
   as well as mark the row. */
.item h3.is-highlight {
  color: var(--accent-strong);
}

.item p {
  font-size: 14px;
  line-height: 1.6;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
