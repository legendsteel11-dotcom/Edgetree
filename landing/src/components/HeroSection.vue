<script setup lang="ts">
import { t } from '../i18n'

const RELEASE_URL = 'https://github.com/legendsteel11-dotcom/Edgetree/releases/latest'
</script>

<template>
  <section id="top" class="hero">
    <div class="container grid">
      <div class="copy">
        <img src="/icon.png" alt="" width="56" height="56" class="icon" />
        <p class="eyebrow">{{ t.hero.eyebrow }}</p>
        <h1>{{ t.hero.title }}</h1>
        <p class="tagline">{{ t.hero.tagline }}</p>
        <p class="description">{{ t.hero.description }}</p>
        <div class="cta">
          <a class="btn btn-primary" :href="RELEASE_URL" target="_blank" rel="noopener">{{ t.hero.ctaDownload }}</a>
          <a class="btn btn-secondary" href="https://github.com/legendsteel11-dotcom/Edgetree" target="_blank" rel="noopener">{{ t.hero.ctaGithub }}</a>
        </div>
      </div>
      <div class="shot">
        <img src="/screenshots/docked-left.png" alt="Edgetree docked to the left edge of the screen" />
      </div>
    </div>
  </section>
</template>

<style scoped>
.hero {
  padding-top: 64px;
  background:
    radial-gradient(ellipse 900px 500px at 15% 0%, rgba(47, 143, 234, 0.12), transparent 60%),
    var(--bg);
}

.grid {
  display: grid;
  grid-template-columns: 1.1fr 0.9fr;
  align-items: center;
  gap: 48px;
}

.icon {
  margin-bottom: 20px;
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
  font-size: 52px;
  letter-spacing: -0.03em;
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
}

.cta {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.shot {
  display: flex;
  justify-content: center;
}

.shot img {
  max-height: 640px;
  width: auto;
  border-radius: 10px;
  border: 1px solid var(--border);
  box-shadow: 0 30px 80px -20px rgba(0, 0, 0, 0.6);
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
    text-align: center;
  }

  .cta {
    justify-content: center;
  }

  .shot {
    order: -1;
  }

  .shot img {
    max-height: 420px;
  }
}
</style>
