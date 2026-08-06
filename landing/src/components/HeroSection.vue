<script setup lang="ts">
import { onMounted } from 'vue'
import { t } from '../i18n'
import { setupDownloadUrl, standaloneDownloadUrl, ensureReleaseAssetsLoaded, trackDownload } from '../releaseAssets'

onMounted(ensureReleaseAssetsLoaded)
</script>

<template>
  <section id="top" class="hero">
    <div class="container">
      <div class="copy">
        <img src="/icon.png" alt="" width="56" height="56" class="icon" />
        <p class="eyebrow">{{ t.hero.eyebrow }}</p>
        <h1>{{ t.hero.title }}</h1>
        <p class="tagline">{{ t.hero.tagline }}</p>
        <p class="description">{{ t.hero.description }}</p>
        <!-- Korean page only (the English string is empty). Placed here rather
             than beside the screenshots it corrects: by the time someone has
             scrolled to those, the impression is already made. -->
        <p v-if="t.hero.langBadge" class="lang-badge">{{ t.hero.langBadge }}</p>
        <div class="cta">
          <a class="btn btn-primary" :href="setupDownloadUrl" target="_blank" rel="noopener"
                  @click="trackDownload('setup', 'hero')">{{ t.hero.ctaDownloadSetup }}</a>
          <a class="btn btn-secondary" :href="standaloneDownloadUrl" target="_blank" rel="noopener"
                  @click="trackDownload('standalone', 'hero')">{{ t.hero.ctaDownloadPortable }}</a>
          <a class="btn btn-secondary" href="https://github.com/legendsteel11/Edgetree" target="_blank" rel="noopener">{{ t.hero.ctaGithub }}</a>
        </div>
      </div>
      <div class="shot">
        <img src="/screenshots/EdgetreeDemo.gif" alt="Edgetree docked to the left edge of a full screen" />
      </div>
    </div>
  </section>
</template>

<style scoped>
.hero {
  padding-top: 64px;
  background:
    radial-gradient(ellipse 900px 500px at 50% 0%, rgba(47, 143, 234, 0.12), transparent 60%),
    var(--bg);
}

.copy {
  text-align: center;
  max-width: 720px;
  margin: 0 auto;
}

.icon {
  margin: 0 auto 20px;
}

.eyebrow {
  color: var(--accent-strong);
  font-size: 14px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  margin-bottom: 12px;
}

h1 {
  font-size: 48px;
  /* letter-spacing: -0.03em; */
  margin-bottom: 16px;
}

.tagline {
  font-size: 20px;
  color: var(--text-strong);
  line-height: 1.5;
  margin-bottom: 16px;
}

.description {
  font-size: 16px;
  line-height: 1.7;
  margin-bottom: 32px;
  /* The two sentences are broken by hand in i18n.ts - left to wrap, the second
     one spilled onto a third line and split a word across it. A narrow screen
     still wraps each line further, which is fine; this only stops the pair
     from running together. */
  white-space: pre-line;
}

/* Outlined rather than filled: it sits just above the download buttons and a
   solid accent pill there would compete with the one control that matters. */
.lang-badge {
  display: inline-block;
  margin: -18px 0 24px;
  padding: 4px 12px;
  border-radius: 999px;
  border: 1px solid var(--accent);
  background: var(--accent-bg);
  color: var(--accent-strong);
  font-size: 13px;
  line-height: 1.4;
}

.cta {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
  margin-bottom: 56px;
}

.shot img {
  /* Grows up to the demo GIF's native 960px width - past that it would
     upscale and blur, so max-width caps it there. */
  width: 100%;
  max-width: 960px;
  display: block;
  margin: 0 auto;
  border-radius: 10px;
  /* border: 1px solid var(--border); */
  box-shadow: 0 30px 80px -20px rgba(0, 0, 0, 0.6);
}

@media (max-width: 720px) {
  h1 {
    font-size: 38px;
  }

  .tagline {
    font-size: 18px;
  }

  .shot img {
    width: 90%;
    margin: 0 auto;
  }
}
</style>
