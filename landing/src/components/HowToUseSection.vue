<script setup lang="ts">
import { t } from '../i18n'
</script>

<template>
  <section id="howto">
    <div class="container">
      <div class="section-heading">
        <h2>{{ t.howto.title }}</h2>
      </div>

      <div class="grid">
        <div v-for="step in t.howto.steps" :key="step.title" class="step">
          <h3>{{ step.title }}</h3>
          <p>{{ step.desc }}</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 24px;
}

.step {
  background: var(--bg-card);
  /* border: 1px solid var(--border); */
  border-radius: 12px;
  padding: 20px 24px;
}

.step h3 {
  font-size: 16px;
  font-weight: 400;
  margin-bottom: 8px;
}

.step p {
  font-size: 13.5px;
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
