<script setup lang="ts">
import { computed, ref } from 'vue';
import StatusBadge from '../../components/StatusBadge.vue';
import { useOperatorShortcuts } from '../../composables/useOperatorShortcuts';
import { createLiveStore, isTextVisible, type LiveStore } from '../../stores/live';
import type { SlideContent } from '../../types/live';

interface Props {
  store?: LiveStore;
}

const DEFAULT_PREVIEW_SLIDES: SlideContent[] = [
  {
    id: 'demo-1',
    title: 'Bienvenida',
    text: 'Bienvenidos a nuestra reunión',
  },
  {
    id: 'demo-2',
    title: 'Salmos 23:1',
    text: 'El Señor es mi pastor; nada me faltará.',
    reference: 'Salmos 23:1',
  },
];

const props = defineProps<Props>();
const activeStore = props.store ?? createLiveStore(undefined, DEFAULT_PREVIEW_SLIDES);
const searchQuery = ref('');

const statusLabel = computed(() => {
  switch (activeStore.effectiveMode.value) {
    case 'Slide':
      return 'En vivo';
    case 'ClearText':
      return 'Texto limpio';
    case 'Blackout':
      return 'Blackout';
    case 'Logo':
      return 'Logo';
    default:
      return 'En reposo';
  }
});

const liveTextVisible = computed(() => isTextVisible(activeStore.state.value));

useOperatorShortcuts({
  nextSlide: activeStore.nextSlide,
  prevSlide: activeStore.prevSlide,
  toggleClearText: activeStore.toggleClearText,
  toggleBlackout: activeStore.toggleBlackout,
  toggleLogo: activeStore.toggleLogo,
});
</script>

<template>
  <main class="operator">
    <header class="topbar">
      <div class="brand">
        <h1>Custom Presenter</h1>
        <StatusBadge
          data-testid="live-status"
          :label="statusLabel"
          :active="activeStore.effectiveMode.value !== 'Idle'"
        />
      </div>
      <input
        v-model="searchQuery"
        data-testid="search-input"
        type="search"
        class="search-input"
        placeholder="Buscar canción, versículo o anuncio..."
      />
    </header>

    <section class="controls" aria-label="Controles en vivo">
      <button type="button" data-testid="btn-prev" @click="activeStore.prevSlide">
        Anterior [↑]
      </button>
      <button type="button" data-testid="btn-next" @click="activeStore.nextSlide">
        Siguiente [↓]
      </button>
      <button
        type="button"
        data-testid="btn-clear"
        :class="{ active: activeStore.state.value.flags.clearText }"
        @click="activeStore.toggleClearText"
      >
        Limpiar texto [C / Esc]
      </button>
      <button
        type="button"
        data-testid="btn-blackout"
        :class="{ active: activeStore.state.value.flags.blackout }"
        @click="activeStore.toggleBlackout"
      >
        Blackout [B]
      </button>
      <button
        type="button"
        data-testid="btn-logo"
        :class="{ active: activeStore.state.value.flags.logo }"
        @click="activeStore.toggleLogo"
      >
        Logo [L]
      </button>
    </section>

    <section class="workspace">
      <div class="panel">
        <h2>Preview</h2>
        <ul class="slide-list">
          <li v-for="slide in activeStore.slides.value" :key="slide.id">
            <button
              type="button"
              class="slide-item"
              :class="{ selected: activeStore.state.value.currentSlide?.id === slide.id }"
              @click="activeStore.projectSlide(slide)"
            >
              <strong>{{ slide.title }}</strong>
              <span>{{ slide.text }}</span>
            </button>
          </li>
        </ul>
      </div>

      <div class="panel">
        <h2>Live</h2>
        <div class="live-monitor" :class="{ blackout: activeStore.state.value.flags.blackout }">
          <p v-if="liveTextVisible && activeStore.state.value.currentSlide" class="live-text">
            {{ activeStore.state.value.currentSlide.text }}
          </p>
          <p v-else class="live-placeholder">
            {{ statusLabel }}
          </p>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.operator {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-height: 100vh;
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.search-input {
  min-width: 280px;
  padding: 0.5rem 0.75rem;
  border-radius: 0.375rem;
  border: 1px solid #2a2f3a;
  background-color: var(--bg-secondary);
  color: var(--text-primary);
}

.controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.controls button,
.slide-item {
  padding: 0.5rem 0.9rem;
  border-radius: 0.375rem;
  border: 1px solid #2a2f3a;
  background-color: var(--bg-secondary);
  color: var(--text-primary);
  cursor: pointer;
}

.controls button.active,
.slide-item.selected {
  background-color: var(--accent);
  border-color: var(--accent);
}

.workspace {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  flex: 1;
}

.panel {
  background-color: var(--bg-secondary);
  border: 1px solid #2a2f3a;
  border-radius: 0.5rem;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.slide-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.slide-item {
  width: 100%;
  text-align: left;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.live-monitor {
  aspect-ratio: 16 / 9;
  background-color: #0b0d11;
  border: 1px solid #2a2f3a;
  border-radius: 0.375rem;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  text-align: center;
}

.live-monitor.blackout {
  background-color: #000000;
}

.live-text {
  white-space: pre-line;
  font-size: 1.25rem;
}

.live-placeholder {
  color: var(--text-muted);
}
</style>
