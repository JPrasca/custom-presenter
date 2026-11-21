import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import ObsOverlayView from '../src/views/obs/ObsOverlayView.vue';
import ProjectorView from '../src/views/projector/ProjectorView.vue';
import type { LiveState } from '../src/types/live';

describe('Fondos multimedia en Proyector Nativo y transparencia en OBS (HU-12)', () => {
  it('renderiza imagen de fondo exclusivamente en ProjectorView', () => {
    // Arrange
    const mediaId = '11111111-1111-1111-1111-111111111111';
    const state: LiveState = {
      currentSlide: { id: '1', title: 'T', text: 'Texto' },
      flags: { clearText: false, blackout: false, logo: false },
      backgroundMedia: { id: mediaId, kind: 'Image' },
    };

    // Act
    const projector = mount(ProjectorView, { props: { state } });
    const obs = mount(ObsOverlayView, { props: { state } });

    // Assert
    const img = projector.find('img.projector-bg-media');
    expect(img.exists()).toBe(true);
    expect(img.attributes('src')).toBe(`/media/${mediaId}`);
    expect(obs.find('img').exists()).toBe(false);
    expect(obs.find('video').exists()).toBe(false);
  });

  it('renderiza video en bucle silenciado en ProjectorView cuando el fondo es Video', () => {
    // Arrange
    const mediaId = '22222222-2222-2222-2222-222222222222';
    const state: LiveState = {
      currentSlide: null,
      flags: { clearText: false, blackout: false, logo: false },
      backgroundMedia: { id: mediaId, kind: 'Video' },
    };

    // Act
    const projector = mount(ProjectorView, { props: { state } });
    const video = projector.find('video.projector-bg-media');

    // Assert
    expect(video.exists()).toBe(true);
    expect(video.attributes('src')).toBe(`/media/${mediaId}`);
    expect(video.attributes('autoplay')).toBeDefined();
    expect(video.attributes('loop')).toBeDefined();
    expect((video.element as HTMLVideoElement).muted).toBe(true);
    expect(video.attributes('playsinline')).toBeDefined();
  });
});
