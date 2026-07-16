<script setup lang="ts">
import { t } from '../i18n'

const EMAIL = 'pjh85336@gmail.com'
</script>

<template>
  <footer class="footer">
    <div class="container">
      <div class="row">
        <div class="brand">
          <img src="/icon.png" alt="" width="20" height="20" />
          <span>Edgetree</span>
        </div>
        <a class="contact" :href="`mailto:${EMAIL}`">{{ t.footer.contact }}: {{ EMAIL }}</a>
      </div>
      <p class="copyright">{{ t.footer.copyright }}</p>
    </div>
  </footer>
</template>

<style scoped>
.footer {
  padding: 40px 0;
  border-top: 1px solid var(--border);
}

.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 16px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: var(--text-strong);
  font-size: 14px;
}

.contact {
  color: var(--text);
  text-decoration: none;
  font-size: 14px;
}

.contact:hover {
  color: var(--accent-strong);
}

.copyright {
  font-size: 12.5px;
  opacity: 0.6;
}
</style>
