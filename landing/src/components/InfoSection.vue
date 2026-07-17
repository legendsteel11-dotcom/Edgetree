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
        <a
          class="link"
          href="https://github.com/legendsteel11-dotcom/Edgetree/blob/main/LICENSE.md"
          target="_blank"
          rel="noopener"
        >{{ t.info.licenseLink }} →</a>
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

.link {
  color: var(--accent-strong);
  text-decoration: none;
  font-size: 14px;
  font-weight: 600;
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
