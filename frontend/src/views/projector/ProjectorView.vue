<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import {
  createDefaultThemeSettings,
  createInitialLiveState,
  isTextVisible,
} from '../../stores/live';
import type { LiveState } from '../../types/live';

interface Props {
  state?: LiveState;
}

interface PhotinoExternal {
  receiveMessage?: (callback: (message: string) => void) => void;
  sendMessage?: (message: string) => void;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  closeRequest: [];
}>();

const liveState = ref<LiveState>(createInitialLiveState());
const activeState = computed(() => props.state ?? liveState.value);
const textVisible = computed(() => isTextVisible(activeState.value));
const projectorTheme = computed(
  () =>
    activeState.value.effectiveTheme?.projector ??
    activeState.value.theme?.projector ??
    createDefaultThemeSettings().projector
);
const containerStyle = computed(() => ({
  backgroundColor: activeState.value.flags.blackout
    ? '#000000'
    : projectorTheme.value.backgroundColor,
  color: projectorTheme.value.textColor,
  fontFamily: projectorTheme.value.fontFamily,
}));
const slideTextStyle = computed(() => ({
  fontSize: `${projectorTheme.value.fontSizePx}px`,
  textAlign: projectorTheme.value.alignment.toLowerCase() as 'left' | 'center' | 'right',
}));

function handleKeydown(event: KeyboardEvent) {
  if (event.key !== 'Escape') {
    return;
  }

  emit('closeRequest');
  const external = (window as unknown as { external?: PhotinoExternal }).external;
  external?.sendMessage?.('projector:close');
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown);
  const external = (window as unknown as { external?: PhotinoExternal }).external;
  external?.receiveMessage?.((raw) => {
    try {
      const parsed = JSON.parse(raw) as LiveState;
      if (parsed && typeof parsed === 'object' && 'flags' in parsed) {
        liveState.value = parsed;
      }
    } catch {
      // Ignora payloads malformados sin afectar el renderizado.
    }
  });
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown);
});
</script>

<template>
  <main
    class="projector"
    :class="{ blackout: activeState.flags.blackout }"
    :style="containerStyle"
    tabindex="0"
    @keydown="handleKeydown"
  >
    <p v-if="textVisible && activeState.currentSlide" class="slide-text" :style="slideTextStyle">
      {{ activeState.currentSlide.text }}
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
  outline: none;
}

.slide-text {
  white-space: pre-line;
  font-size: 3rem;
  line-height: 1.3;
}
</style>
