import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import ObsOverlayView from '../src/views/obs/ObsOverlayView.vue';
import ProjectorView from '../src/views/projector/ProjectorView.vue';
import { createDefaultThemeSettings } from '../src/stores/live';
import type { LiveState } from '../src/types/live';

describe('Temas visuales independientes en Proyector y OBS (HU-11)', () => {
  it('aplica estilos del tema de Proyector mediante bindings CSSOM sin v-html', () => {
    // Arrange
    const theme = createDefaultThemeSettings();
    theme.projector = {
      fontFamily: 'Montserrat',
      fontSizePx: 84,
      textColor: '#FFE082',
      backgroundColor: '#10141F',
      alignment: 'Left',
    };

    const state: LiveState = {
      currentSlide: { id: '1', title: 'T', text: 'Texto Proyector' },
      flags: { clearText: false, blackout: false, logo: false },
      theme,
    };

    // Act
    const wrapper = mount(ProjectorView, { props: { state } });
    const mainEl = wrapper.find('main.projector').element as HTMLElement;
    const slideEl = wrapper.find('.slide-text').element as HTMLElement;

    // Assert
    expect(mainEl.style.backgroundColor).not.toBe('');
    expect(slideEl.style.fontSize).toBe('84px');
    expect(slideEl.style.textAlign).toBe('left');
  });

  it('aplica posicionamiento y estilos del tema de OBS en tiempo real', () => {
    // Arrange
    const theme = createDefaultThemeSettings();
    theme.obs = {
      fontFamily: 'Roboto',
      fontSizePx: 40,
      textColor: '#FFFFFF',
      boxBackgroundColor: '#0F172ACC',
      position: 'Top',
      marginHorizontalPx: 72,
      marginVerticalPx: 36,
      borderRadiusPx: 14,
    };

    const state: LiveState = {
      currentSlide: { id: '2', title: 'T', text: 'Texto OBS' },
      flags: { clearText: false, blackout: false, logo: false },
      theme,
    };

    // Act
    const wrapper = mount(ObsOverlayView, { props: { state } });
    const overlayEl = wrapper.find('main.obs-overlay').element as HTMLElement;
    const textEl = wrapper.find('.slide-text').element as HTMLElement;

    // Assert
    expect(overlayEl.style.alignItems).toBe('flex-start');
    expect(textEl.style.fontSize).toBe('40px');
  });
});
