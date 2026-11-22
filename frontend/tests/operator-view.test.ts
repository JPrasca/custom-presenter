import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import OperatorView from '../src/views/operator/OperatorView.vue';
import { createLiveStore, type LiveTransport } from '../src/stores/live';
import { LiveStateBuilder, SlideContentBuilder } from './builders/liveStateBuilder';

function createFakeTransport(): LiveTransport {
  return {
    getState: vi.fn(async () => new LiveStateBuilder().build()),
    projectSlide: vi.fn(async (slide) => new LiveStateBuilder().withSlide(slide).build()),
    toggleClearText: vi.fn(async (current) =>
      new LiveStateBuilder()
        .withSlide(current.currentSlide)
        .withFlags({ ...current.flags, clearText: !current.flags.clearText })
        .build()
    ),
    toggleBlackout: vi.fn(async (current) =>
      new LiveStateBuilder()
        .withSlide(current.currentSlide)
        .withFlags({ ...current.flags, blackout: !current.flags.blackout })
        .build()
    ),
    toggleLogo: vi.fn(async (current) =>
      new LiveStateBuilder()
        .withSlide(current.currentSlide)
        .withFlags({ ...current.flags, logo: !current.flags.logo })
        .build()
    ),
  };
}

describe('OperatorView y atajos de teclado (HU-04)', () => {
  it('actualiza el store reactivo y el indicador Live al proyectar y alternar banderas', async () => {
    // Arrange
    const transport = createFakeTransport();
    const slide1 = new SlideContentBuilder().withId('s1').withText('Estrofa 1').build();
    const slide2 = new SlideContentBuilder().withId('s2').withText('Estrofa 2').build();
    const store = createLiveStore(transport, [slide1, slide2]);
    const wrapper = mount(OperatorView, {
      props: { store },
    });

    // Act
    await wrapper.get('[data-testid="btn-next"]').trigger('click');
    await wrapper.get('[data-testid="btn-clear"]').trigger('click');

    // Assert
    expect(store.state.value.currentSlide?.id).toBe('s1');
    expect(store.state.value.flags.clearText).toBe(true);
    expect(wrapper.get('[data-testid="live-status"]').text()).toContain('Texto limpio');
  });

  it('ejecuta atajos globales C, Esc, B, L y flechas cuando no hay foco en inputs', async () => {
    // Arrange
    const transport = createFakeTransport();
    const slide1 = new SlideContentBuilder().withId('s1').withText('Uno').build();
    const slide2 = new SlideContentBuilder().withId('s2').withText('Dos').build();
    const store = createLiveStore(transport, [slide1, slide2]);
    const wrapper = mount(OperatorView, {
      props: { store },
      attachTo: document.body,
    });

    // Act
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'b', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'l', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));

    // Assert
    expect(store.state.value.currentSlide?.id).toBe('s2');
    expect(store.state.value.flags.clearText).toBe(true);
    expect(store.state.value.flags.blackout).toBe(true);
    expect(store.state.value.flags.logo).toBe(true);
    wrapper.unmount();
  });

  it('ignora atajos de una tecla cuando el foco está en input, textarea o contenteditable', async () => {
    // Arrange
    const transport = createFakeTransport();
    const store = createLiveStore(transport, [new SlideContentBuilder().build()]);
    const wrapper = mount(OperatorView, {
      props: { store },
      attachTo: document.body,
    });

    const inputEl = wrapper.get('[data-testid="search-input"]').element as HTMLInputElement;
    const textarea = document.createElement('textarea');
    const editable = document.createElement('div');
    editable.contentEditable = 'true';
    document.body.append(textarea, editable);

    // Act
    inputEl.focus();
    inputEl.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', bubbles: true }));
    inputEl.dispatchEvent(new KeyboardEvent('keydown', { key: 'b', bubbles: true }));
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'b', bubbles: true }));
    editable.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));

    // Assert
    expect(transport.toggleClearText).not.toHaveBeenCalled();
    expect(transport.toggleBlackout).not.toHaveBeenCalled();

    textarea.remove();
    editable.remove();
    wrapper.unmount();
  });

  it('ignora atajos cuando ctrlKey, metaKey o altKey están presionados', async () => {
    // Arrange
    const transport = createFakeTransport();
    const store = createLiveStore(transport, [new SlideContentBuilder().build()]);
    const wrapper = mount(OperatorView, {
      props: { store },
      attachTo: document.body,
    });

    // Act
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', metaKey: true, bubbles: true }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'b', ctrlKey: true, bubbles: true }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'l', altKey: true, bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));

    // Assert
    expect(transport.toggleClearText).not.toHaveBeenCalled();
    expect(transport.toggleBlackout).not.toHaveBeenCalled();
    expect(transport.toggleLogo).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it('HU-20: muestra botonera de switch en caliente cuando hay un versículo bíblico al aire', async () => {
    // Arrange
    const bibleSlide = new SlideContentBuilder()
      .withId('v1:19:23:1')
      .withTitle('Salmos 23:1 (RVR60)')
      .withText('Jehová es mi pastor; nada me faltará.')
      .build();
    const transport = {
      ...createFakeTransport(),
      getState: vi.fn(async () => new LiveStateBuilder().withSlide(bibleSlide).build()),
    };
    const store = createLiveStore(transport, [bibleSlide]);
    await store.projectSlide(bibleSlide);

    const wrapper = mount(OperatorView, {
      props: { store },
    });

    // Act & Assert
    const hotVersionsBar = wrapper.find('[data-testid="hot-versions-bar"]');
    expect(hotVersionsBar.exists()).toBe(true);
  });
});
