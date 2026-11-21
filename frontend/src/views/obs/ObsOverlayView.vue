<script setup lang="ts">
import { computed } from 'vue';
import { createInitialLiveState, isTextVisible } from '../../stores/live';

const state = createInitialLiveState();
const textVisible = computed(() => isTextVisible(state));
</script>

<template>
  <main class="obs-overlay">
    <div v-if="textVisible && state.currentSlide" class="lower-third">
      <p class="slide-text">
        {{ state.currentSlide.text }}
      </p>
    </div>
  </main>
</template>

<style scoped>
.obs-overlay {
  width: 100vw;
  height: 100vh;
  background: transparent;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding: 3rem;
}

.lower-third {
  background: rgba(15, 17, 21, 0.78);
  padding: 1rem 2rem;
  border-radius: 0.5rem;
  color: #ffffff;
  text-align: center;
}

.slide-text {
  white-space: pre-line;
  font-size: 2rem;
}
</style>
