<script setup lang="ts">
import { onMounted } from 'vue'
import { t } from '../i18n'
import { standardDownloadUrl, standaloneDownloadUrl, ensureReleaseAssetsLoaded } from '../releaseAssets'

onMounted(ensureReleaseAssetsLoaded)
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
          <a class="btn btn-secondary" :href="standardDownloadUrl" target="_blank" rel="noopener">{{ t.download.button }}</a>
        </div>
        <div class="card highlight">
          <h3>{{ t.download.standaloneTitle }}</h3>
          <p>{{ t.download.standaloneDesc }}</p>
          <a class="btn btn-primary" :href="standaloneDownloadUrl" target="_blank" rel="noopener">{{ t.download.button }}</a>
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
}

.disclaimers p + p {
  margin-top: 6px;
}

@media (max-width: 600px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
