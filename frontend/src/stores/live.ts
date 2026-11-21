import type { LiveState } from '../types/live';

export function createInitialLiveState(): LiveState {
  return {
    currentSlide: null,
    flags: {
      clearText: false,
      blackout: false,
      logo: false,
    },
  };
}

export function isTextVisible(state: LiveState): boolean {
  return (
    state.currentSlide !== null &&
    !state.flags.clearText &&
    !state.flags.blackout &&
    !state.flags.logo
  );
}
