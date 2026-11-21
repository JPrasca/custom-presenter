import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import ProjectorView from '../src/views/projector/ProjectorView.vue';
import type { LiveState } from '../src/types/live';

describe('ProjectorView (HU-03)', () => {
  it('renderiza payloads HTML/JS como texto literal sin inyectar script', () => {
    // Arrange
    const xssPayload = '<script>alert(1)</script> & "salón"';
    const state: LiveState = {
      currentSlide: { id: 'p-1', title: 'Salón', text: xssPayload },
      flags: { clearText: false, blackout: false, logo: false },
    };

    // Act
    const wrapper = mount(ProjectorView, {
      props: { state },
    });

    // Assert
    expect(wrapper.find('script').exists()).toBe(false);
    expect(wrapper.find('.slide-text').text()).toBe(xssPayload);
  });

  it('oculta el texto cuando blackout está activo', () => {
    // Arrange
    const state: LiveState = {
      currentSlide: { id: 'p-2', title: 'Salón', text: 'Visible' },
      flags: { clearText: false, blackout: true, logo: false },
    };

    // Act
    const wrapper = mount(ProjectorView, {
      props: { state },
    });

    // Assert
    expect(wrapper.find('.slide-text').exists()).toBe(false);
    expect(wrapper.find('main.projector').classes()).toContain('blackout');
  });

  it('emite evento closeRequest al presionar Escape', async () => {
    // Arrange
    const wrapper = mount(ProjectorView);

    // Act
    await wrapper.trigger('keydown', { key: 'Escape' });

    // Assert
    expect(wrapper.emitted('closeRequest')).toBeTruthy();
  });
});
