<script setup lang="ts">
import { t, lang, toggleLang } from '../i18n'
</script>

<template>
  <header class="header">
    <div class="container bar">
      <a href="#top" class="brand">
        <img src="/icon.png" alt="Edgetree" width="28" height="28" />
        <span>Edgetree</span>
      </a>

      <nav class="nav">
        <a href="#screenshots">{{ t.nav.screenshots }}</a>
        <a href="#features">{{ t.nav.features }}</a>
        <a href="#download">{{ t.nav.download }}</a>
        <a href="https://github.com/legendsteel11-dotcom/Edgetree" target="_blank" rel="noopener">{{ t.nav.github }}</a>
      </nav>

      <button class="lang-toggle" type="button" @click="toggleLang" :aria-label="lang === 'ko' ? 'Switch to English' : '한국어로 전환'">
        <span :class="{ active: lang === 'ko' }">한</span>
        <span class="sep">/</span>
        <span :class="{ active: lang === 'en' }">EN</span>
      </button>
    </div>
  </header>
</template>

<style scoped>
.header {
  position: sticky;
  top: 0;
  z-index: 20;
  background: rgba(26, 26, 26, 0.05);
  backdrop-filter: blur(8px);
  border-bottom: 1px solid var(--border);
}

.bar {
  display: flex;
  align-items: center;
  gap: 24px;
  height: 64px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 700;
  font-size: 18px;
  color: var(--text-strong);
  text-decoration: none;
  flex-shrink: 0;
}

.nav {
  display: flex;
  gap: 24px;
  margin-left: 8px;
  flex-grow: 1;
}

.nav a {
  color: var(--text);
  text-decoration: none;
  font-size: 15px;
}

.nav a:hover {
  color: var(--text-strong);
}

.lang-toggle {
  display: flex;
  align-items: center;
  gap: 4px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 6px 12px;
  color: var(--text);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  flex-shrink: 0;
}

.lang-toggle .sep {
  opacity: 0.4;
}

.lang-toggle .active {
  color: var(--accent-strong);
}

@media (max-width: 720px) {
  .nav {
    display: none;
  }
}
</style>
