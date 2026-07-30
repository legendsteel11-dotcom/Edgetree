<script setup lang="ts">
import { t } from '../i18n'
</script>

<template>
  <section id="info">
    <div class="container grid">
      <div class="card">
        <h2>{{ t.info.requirementsTitle }}</h2>
        <ul>
          <li v-for="line in t.info.requirements" :key="line">{{ line }}</li>
        </ul>
      </div>
      <div class="card">
        <h2>{{ t.info.licenseTitle }}</h2>
        <p>{{ t.info.licenseBody }}</p>
        <!-- The icons are third-party and under two different licences, so the
             attribution lives beside the app's own licence rather than only in
             the repository. -->
        <p class="notices">{{ t.info.iconNotice }}</p>
        <a
          class="link"
          href="https://github.com/legendsteel11/Edgetree/blob/main/LICENSE.md"
          target="_blank"
          rel="noopener"
        >{{ t.info.licenseLink }} →</a>
        <a
          class="link"
          href="https://github.com/legendsteel11/Edgetree/blob/main/THIRD-PARTY-NOTICES.md"
          target="_blank"
          rel="noopener"
        >{{ t.info.noticesLink }} →</a>
      </div>
    </div>
  </section>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 28px;
}

.card {
  background: var(--bg-card);
  /* border: 1px solid var(--border); */
  border-radius: 12px;
  padding: 32px;
}

.card h2 {
  font-size: 20px;
  margin-bottom: 16px;
}

.card ul {
  margin: 0;
  padding-left: 20px;
}

.card li {
  margin-bottom: 8px;
  line-height: 1.6;
    font-size: 14px;
}

.card p {
  line-height: 1.6;
  margin-bottom: 16px;
    font-size: 14px;
}

/* A step smaller than the licence line above it, which is what marks it as the
   fine print - no opacity, so it stays as readable as everything else. */
.card p.notices {
  font-size: 13px;
  white-space: pre-line;
}

.link {
  color: var(--accent-strong);
  text-decoration: none;
  font-size: 14px;
  font-weight: 600;
  display: inline-block;
  margin-right: 18px;
}

.link:hover {
  text-decoration: underline;
}

@media (max-width: 720px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
