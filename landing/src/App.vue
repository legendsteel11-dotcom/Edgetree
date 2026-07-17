<script setup lang="ts">
import AppHeader from './components/AppHeader.vue'
import HeroSection from './components/HeroSection.vue'
import HowToUseSection from './components/HowToUseSection.vue'
import ScreenshotCards from './components/ScreenshotCards.vue'
import FeatureGrid from './components/FeatureGrid.vue'
import InfoSection from './components/InfoSection.vue'
import DownloadSection from './components/DownloadSection.vue'
import AppFooter from './components/AppFooter.vue'
</script>

<template>
  <AppHeader />
  <main>
    <HeroSection />
    <HowToUseSection />
    <ScreenshotCards />
    <FeatureGrid />
    <InfoSection />
    <DownloadSection />
  </main>
  <AppFooter />
</template>
