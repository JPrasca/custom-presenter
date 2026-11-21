import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import ObsOverlayView from '../src/views/obs/ObsOverlayView.vue';
import type { LiveState } from '../src/types/live';

describe('Seguridad XSS en ObsOverlayView (HU-02)', () => {
  it('renderiza payloads HTML/JS como texto literal sin inyectar nodos script', () => {
    // Arrange
    const xssPayload = '<script>alert("xss")</script> & "comillas"';
    const state: LiveState = {
      currentSlide: {
        id: 'xss-1',
        title: 'Prueba XSS',
        text: xssPayload,
      },
      flags: { clearText: false, blackout: false, logo: false },
    };

    // Act
    const wrapper = mount(ObsOverlayView, {
      props: { state },
    });

    // Assert
    expect(wrapper.find('script').exists()).toBe(false);
    expect(wrapper.find('.slide-text').text()).toBe(xssPayload);
  });
});
