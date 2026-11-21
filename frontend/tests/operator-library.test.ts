import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import OperatorView from '../src/views/operator/OperatorView.vue';
import { createLiveStore } from '../src/stores/live';

describe('Paneles de biblioteca en OperatorView (HU-14)', () => {
  it('permite navegar entre pestañas de biblioteca manteniendo controles en vivo', async () => {
    // Arrange
    const store = createLiveStore();
    const wrapper = mount(OperatorView, {
      props: { store },
    });

    // Act
    const tabs = wrapper.findAll('[data-testid^="tab-"]');
    await wrapper.get('[data-testid="tab-bibles"]').trigger('click');

    // Assert
    expect(tabs.length).toBeGreaterThanOrEqual(7);
    expect(wrapper.find('[data-testid="btn-blackout"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="panel-bibles"]').exists()).toBe(true);
  });
});
