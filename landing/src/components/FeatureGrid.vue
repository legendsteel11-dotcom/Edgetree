<script setup lang="ts">
import { t } from '../i18n'
</script>

<template>
  <section id="features" class="alt">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.features.title }}</h2>
        <p>{{ t.features.subtitle }}</p>
      </div>

      <div class="grid">
        <div v-for="item in t.features.items" :key="item.title" class="item">
          <h3>{{ item.title }}</h3>
          <p>{{ item.desc }}</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.alt {
  background: var(--bg-alt);
  border-top: 1px solid var(--border);
  border-bottom: 1px solid var(--border);
}

.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 24px 32px;
}

.item {
  padding-left: 16px;
  border-left: 2px solid var(--border);
}

.item h3 {
  font-size: 16px;
  font-weight: 400;
  margin-bottom: 6px;
  color: var(--text-strong);
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
