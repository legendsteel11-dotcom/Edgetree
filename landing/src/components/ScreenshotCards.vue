<script setup lang="ts">
import { t } from '../i18n'

const images = [
  'docked-left-2.png',
  'browsing.png',
  'context-menu.png',
  'options-menu.png',
  'color-settings.png',
  'favorite.png',
]
</script>

<template>
  <section id="screenshots">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.screenshots.title }}</h2>
      </div>

      <div class="grid">
        <figure v-for="(item, i) in t.screenshots.items" :key="item.title" class="card">
          <div class="shot">
            <img :src="`/screenshots/${images[i]}`" :alt="item.title" loading="lazy" />
          </div>
          <figcaption>
            <h3>{{ item.title }}</h3>
            <p>{{ item.desc }}</p>
          </figcaption>
        </figure>
      </div>
    </div>
  </section>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 28px;
}

.card {
  margin: 0;
  background: var(--bg-card);
  /* border: 1px solid var(--border); */
  border-radius: 12px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.shot {
  background: #0e0e0e;
  height: 260px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  border-bottom: 1px solid var(--border);
}

.shot img {
  height: 100%;
  width: 100%;
  object-fit: cover;
  object-position: top;
}

figcaption {
  padding: 20px;
}

h3 {
  font-size: 16px;
  font-weight: 400;
  margin-bottom: 8px;
}

figcaption p {
  font-size: 14px;
  line-height: 1.6;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 600px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
