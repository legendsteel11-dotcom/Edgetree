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
        <!-- TWO ROUTES, and the second one is why this changed (2026-08-19).
             The only way in used to be mail, and a report that arrives as mail
             arrives among the rest of the mail: an issue sat unanswered for a
             day because a small "1" in a repository header is not something
             anyone watches. An issue carries its own badge and its own
             notification, and the next person with the same symptom can read
             it instead of asking again.

             STILL IN THE FOOTER. It belongs where someone who has decided to
             use the app goes looking, not beside the download button - a line
             about bug reports there is a reason to hesitate at the moment of
             deciding. -->
        <span class="contact">
          {{ t.footer.contact }}:
          <a :href="`mailto:${EMAIL}`">{{ EMAIL }}</a>
          ·
          <a href="https://github.com/legendsteel11/Edgetree/issues" target="_blank" rel="noopener">GitHub Issues</a>
        </span>
      </div>

      <!-- 상호 링크는 푸터에(TabStick 랜딩과 같은 자리·같은 모양).
           SweepCap rides as a SECOND capsule (2026-08-23, 사용자 요청) rather
           than a second name inside the first: the first pill's label reads
           across the row, so the neighbour carries just the name - repeating
           "같은 개발자의" per pill would say the same thing twice side by side. -->
      <p class="other">
        {{ t.footer.otherTool }} ·
        <a href="https://tabstick.com/" target="_blank" rel="noopener">{{ t.footer.otherToolName }}</a>
      </p>
      <p class="other">
        <a href="https://github.com/legendsteel11/SweepCap" target="_blank" rel="noopener">{{ t.footer.otherTool2Name }}</a>
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

/* 이 자리가 링크 하나였다가 라벨 + 링크 둘로 바뀌었으므로(2026-08-19), 색과
   밑줄은 안쪽 a가 받는다. 바깥 span에만 두면 라벨까지 링크처럼 보이고, 정작
   눌리는 두 곳은 브라우저 기본 파랑으로 돌아간다. */
.contact {
  color: var(--text);
  font-size: 14px;
}

.contact a {
  color: inherit;
  text-decoration: none;
}

.contact a:hover {
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

/* 두 알약 사이 간격. inline-block이라 붙여 두면 한 덩어리로 읽힌다. */
.other + .other {
  margin-left: 8px;
}

.copyright {
  font-size: 12.5px;
  opacity: 0.6;
}
</style>
