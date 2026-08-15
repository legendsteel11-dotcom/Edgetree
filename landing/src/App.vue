<script setup lang="ts">
import AppHeader from './components/AppHeader.vue'
import HeroSection from './components/HeroSection.vue'
import HowToUseSection from './components/HowToUseSection.vue'
import ScreenshotCards from './components/ScreenshotCards.vue'
import ScreenshotGallery from './components/ScreenshotGallery.vue'
import FeatureGrid from './components/FeatureGrid.vue'
import InfoSection from './components/InfoSection.vue'
import DownloadSection from './components/DownloadSection.vue'
import AppFooter from './components/AppFooter.vue'
</script>

<template>
  <AppHeader />
  <main>
    <HeroSection />
    <ScreenshotCards />
    <FeatureGrid />
    <HowToUseSection />
    <!-- After 사용방법 and before the notes: by here the page has said what the
         app is and how it is driven, so a wall of whole screens reads as "this
         is what that looks like" rather than as an unlabelled gallery. -->
    <ScreenshotGallery />
    <InfoSection />
    <DownloadSection />
  </main>
  <AppFooter />
</template>
