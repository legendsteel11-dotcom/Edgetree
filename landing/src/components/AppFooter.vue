<script setup lang="ts">
import { t } from '../i18n'

const EMAIL = 'pjh85336@gmail.com'
</script>

<template>
  <footer class="footer">
    <div class="container">
      <div class="row">
        <!-- GitHub sits WITH THE NAME, not on a line of its own further down.
             Below, it landed next to the TabStick pill and read as TabStick's
             repository - two outbound links side by side, and the nearer label
             wins (2026-08-15). Beside "Edgetree" there is nothing else it could
             belong to. -->
        <div class="brand">
          <img src="/icon.png" alt="" width="20" height="20" />
          <span>Edgetree</span>
          <a class="repo" href="https://github.com/legendsteel11/Edgetree" target="_blank" rel="noopener">{{ t.nav.github }}</a>
        </div>
        <a class="contact" :href="`mailto:${EMAIL}`">{{ t.footer.contact }}: {{ EMAIL }}</a>
      </div>

      <!-- 상호 링크는 푸터에(TabStick 랜딩과 같은 자리·같은 모양). -->
      <p class="other">
        {{ t.footer.otherTool }} ·
        <a href="https://tabstick.com/" target="_blank" rel="noopener">{{ t.footer.otherToolName }}</a>
      </p>

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

/* A plain link, not the pill the TabStick line wears: the pill is what made it
   read as one of a pair of equal outbound links. Here it is a note attached to
   the name beside it. */
.repo {
  margin-left: 6px;
  color: var(--text);
  text-decoration: none;
  font-size: 14px;
  font-weight: 400;
}

.repo:hover {
  color: var(--accent-strong);
}

.contact {
  color: var(--text);
  text-decoration: none;
  font-size: 14px;
}

.contact:hover {
  color: var(--accent-strong);
}

/* 다른 도구(TabStick) 안내 - 옅은 파랑 틴트 알약으로 감싸 다른 푸터 줄과 구분.
   내용 폭만큼만 감싸게 inline-block. 치수는 이 랜딩의 작은 폰트 스케일에 맞춤. */
.other {
  display: inline-block;
  font-size: 13.5px;
  margin-bottom: 12px;
  padding: 7px 18px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--accent-bg);
}

.other a {
  color: var(--accent-strong);
  font-weight: 700;
  text-decoration: none;
}

.other a:hover {
  text-decoration: underline;
}

.copyright {
  font-size: 12.5px;
  opacity: 0.6;
}
</style>
