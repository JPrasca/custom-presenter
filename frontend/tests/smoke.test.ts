import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import StatusBadge from '../src/components/StatusBadge.vue';
import { createInitialLiveState, isTextVisible } from '../src/stores/live';

describe('Smoke test del entorno frontend Vue 3', () => {
  it('inicializa LiveState en reposo y oculta texto cuando hay blackout o clear', () => {
    const initial = createInitialLiveState();
    expect(isTextVisible(initial)).toBe(false);

    const active = {
      currentSlide: { id: '1', title: 'Prueba', text: 'Hola mundo' },
      flags: { clearText: false, blackout: false, logo: false },
    };
    expect(isTextVisible(active)).toBe(true);
    expect(isTextVisible({ ...active, flags: { ...active.flags, clearText: true } })).toBe(false);
    expect(isTextVisible({ ...active, flags: { ...active.flags, blackout: true } })).toBe(false);
  });

  it('monta un componente Vue 3 correctamente', () => {
    const wrapper = mount(StatusBadge, {
      props: { label: 'En vivo', active: true },
    });
    expect(wrapper.text()).toContain('En vivo');
    expect(wrapper.classes()).toContain('active');
  });
});
