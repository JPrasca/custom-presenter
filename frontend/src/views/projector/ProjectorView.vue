<script setup lang="ts">
import { computed } from 'vue';
import { createInitialLiveState, isTextVisible } from '../../stores/live';

const state = createInitialLiveState();
const textVisible = computed(() => isTextVisible(state));
</script>

<template>
  <main class="projector" :class="{ blackout: state.flags.blackout }">
    <p v-if="textVisible && state.currentSlide" class="slide-text">
      {{ state.currentSlide.text }}
    </p>
  </main>
</template>

<style scoped>
.projector {
  width: 100vw;
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #000000;
  color: #ffffff;
  text-align: center;
  padding: 4rem;
}

.slide-text {
  white-space: pre-line;
  font-size: 3rem;
  line-height: 1.3;
}
</style>
