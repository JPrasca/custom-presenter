<script setup lang="ts">
import { computed } from 'vue';
import {
  createDefaultThemeSettings,
  createInitialLiveState,
  isTextVisible,
} from '../../stores/live';
import type { LiveState } from '../../types/live';

interface Props {
  state?: LiveState;
}

const props = defineProps<Props>();
const activeState = computed(() => props.state ?? createInitialLiveState());
const textVisible = computed(() => isTextVisible(activeState.value));
const obsTheme = computed(
  () =>
    activeState.value.effectiveTheme?.obs ??
    activeState.value.theme?.obs ??
    createDefaultThemeSettings().obs
);

const alignMap = {
  Top: 'flex-start',
  Middle: 'center',
  Bottom: 'flex-end',
} as const;

const overlayStyle = computed(() => ({
  alignItems: alignMap[obsTheme.value.position],
  padding: `${obsTheme.value.marginVerticalPx}px ${obsTheme.value.marginHorizontalPx}px`,
}));

const lowerThirdStyle = computed(() => ({
  backgroundColor: obsTheme.value.boxBackgroundColor,
  color: obsTheme.value.textColor,
  borderRadius: `${obsTheme.value.borderRadiusPx}px`,
  fontFamily: obsTheme.value.fontFamily,
}));

const textStyle = computed(() => ({
  fontSize: `${obsTheme.value.fontSizePx}px`,
}));
</script>

<template>
  <main class="obs-overlay" :style="overlayStyle">
    <div
      v-if="textVisible && activeState.currentSlide"
      class="lower-third"
      :style="lowerThirdStyle"
    >
      <p class="slide-text" :style="textStyle">{{ activeState.currentSlide.text }}</p>
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
