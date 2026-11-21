import { computed, readonly, ref } from 'vue';
import type { EffectiveDisplayMode, LiveState, SlideContent, ThemeSettings } from '../types/live';

export function createDefaultThemeSettings(): ThemeSettings {
  return {
    projector: {
      fontFamily: 'system-ui, -apple-system, sans-serif',
      fontSizePx: 64,
      textColor: '#FFFFFF',
      backgroundColor: '#0B0D12',
      alignment: 'Center',
    },
    obs: {
      fontFamily: 'system-ui, -apple-system, sans-serif',
      fontSizePx: 34,
      textColor: '#FFFFFF',
      boxBackgroundColor: '#0F1115D1',
      position: 'Bottom',
      marginHorizontalPx: 64,
      marginVerticalPx: 48,
      borderRadiusPx: 10,
    },
  };
}

export function createInitialLiveState(): LiveState {
  return {
    currentSlide: null,
    flags: {
      clearText: false,
      blackout: false,
      logo: false,
    },
    theme: createDefaultThemeSettings(),
  };
}

export function getEffectiveMode(state: LiveState): EffectiveDisplayMode {
  if (state.flags.blackout) {
    return 'Blackout';
  }
  if (state.flags.logo) {
    return 'Logo';
  }
  if (state.flags.clearText) {
    return 'ClearText';
  }
  return state.currentSlide !== null ? 'Slide' : 'Idle';
}

export function isTextVisible(state: LiveState): boolean {
  return getEffectiveMode(state) === 'Slide';
}

export interface LiveTransport {
  getState: () => Promise<LiveState>;
  projectSlide: (slide: SlideContent) => Promise<LiveState>;
  toggleClearText: (current: LiveState) => Promise<LiveState>;
  toggleBlackout: (current: LiveState) => Promise<LiveState>;
  toggleLogo: (current: LiveState) => Promise<LiveState>;
}

export function createHttpLiveTransport(baseUrl = 'http://127.0.0.1:47321'): LiveTransport {
  const postCommand = async (path: string, body?: unknown): Promise<LiveState> => {
    const response = await fetch(`${baseUrl}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-CustomPresenter-Client': 'operator',
      },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
    if (!response.ok) {
      throw new Error(`Error al ejecutar comando ${path}: ${response.status}`);
    }
    return (await response.json()) as LiveState;
  };

  return {
    getState: async () => {
      const response = await fetch(`${baseUrl}/api/live`);
      if (!response.ok) {
        throw new Error(`Error al obtener estado en vivo: ${response.status}`);
      }
      return (await response.json()) as LiveState;
    },
    projectSlide: (slide) => postCommand('/api/live/project', slide),
    toggleClearText: () => postCommand('/api/live/clear'),
    toggleBlackout: () => postCommand('/api/live/blackout'),
    toggleLogo: () => postCommand('/api/live/logo'),
  };
}

export function createLiveStore(
  transport: LiveTransport = createHttpLiveTransport(),
  initialSlides: SlideContent[] = []
) {
  const state = ref<LiveState>(createInitialLiveState());
  const slides = ref<SlideContent[]>(initialSlides);
  const selectedIndex = ref<number>(-1);
  const effectiveMode = computed(() => getEffectiveMode(state.value));

  async function projectSlide(slide: SlideContent): Promise<void> {
    state.value = await transport.projectSlide(slide);
    const idx = slides.value.findIndex((item) => item.id === slide.id);
    if (idx >= 0) {
      selectedIndex.value = idx;
    }
  }

  async function nextSlide(): Promise<void> {
    if (slides.value.length === 0) {
      return;
    }
    const nextIdx =
      selectedIndex.value < 0 ? 0 : Math.min(selectedIndex.value + 1, slides.value.length - 1);
    const target = slides.value.at(nextIdx);
    if (target) {
      await projectSlide(target);
    }
  }

  async function prevSlide(): Promise<void> {
    if (slides.value.length === 0) {
      return;
    }
    const prevIdx = selectedIndex.value <= 0 ? 0 : selectedIndex.value - 1;
    const target = slides.value.at(prevIdx);
    if (target) {
      await projectSlide(target);
    }
  }

  async function toggleClearText(): Promise<void> {
    state.value = await transport.toggleClearText(state.value);
  }

  async function toggleBlackout(): Promise<void> {
    state.value = await transport.toggleBlackout(state.value);
  }

  async function toggleLogo(): Promise<void> {
    state.value = await transport.toggleLogo(state.value);
  }

  function setSlides(nextSlides: SlideContent[]): void {
    slides.value = nextSlides;
    selectedIndex.value = nextSlides.length > 0 ? 0 : -1;
  }

  function syncState(nextState: LiveState): void {
    state.value = nextState;
  }

  return {
    state: readonly(state),
    slides: readonly(slides),
    selectedIndex: readonly(selectedIndex),
    effectiveMode,
    projectSlide,
    nextSlide,
    prevSlide,
    toggleClearText,
    toggleBlackout,
    toggleLogo,
    setSlides,
    syncState,
  };
}

export type LiveStore = ReturnType<typeof createLiveStore>;
