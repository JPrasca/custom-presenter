import { onMounted, onUnmounted } from 'vue';

export interface OperatorShortcutActions {
  nextSlide: () => void | Promise<void>;
  prevSlide: () => void | Promise<void>;
  toggleClearText: () => void | Promise<void>;
  toggleBlackout: () => void | Promise<void>;
  toggleLogo: () => void | Promise<void>;
}

export function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tag = target.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') {
    return true;
  }

  return target.isContentEditable || target.contentEditable === 'true';
}

export function useOperatorShortcuts(actions: OperatorShortcutActions): void {
  const onKeydown = (event: KeyboardEvent) => {
    if (event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }

    if (isEditableTarget(event.target)) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowRight':
        event.preventDefault();
        void actions.nextSlide();
        break;
      case 'ArrowUp':
      case 'ArrowLeft':
        event.preventDefault();
        void actions.prevSlide();
        break;
      case 'c':
      case 'C':
      case 'Escape':
        event.preventDefault();
        void actions.toggleClearText();
        break;
      case 'b':
      case 'B':
        event.preventDefault();
        void actions.toggleBlackout();
        break;
      case 'l':
      case 'L':
        event.preventDefault();
        void actions.toggleLogo();
        break;
    }
  };

  onMounted(() => {
    window.addEventListener('keydown', onKeydown);
  });

  onUnmounted(() => {
    window.removeEventListener('keydown', onKeydown);
  });
}
