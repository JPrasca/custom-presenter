import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import OperatorView from '../src/views/operator/OperatorView.vue';
import { createLiveStore } from '../src/stores/live';
import { presenterApi, type SongItem } from '../src/services/presenterApi';

describe('Edición y eliminación de canciones en OperatorView (HU-17)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('carga canción en edición, guarda cambios y resetea el formulario si se elimina la canción editada', async () => {
    // Arrange
    const initialSong: SongItem = {
      id: 's1',
      title: 'Sublime Gracia',
      author: 'John Newton',
      musicalKey: 'G',
      rawLyrics: 'Estrofa 1',
      slides: [{ index: 0, label: 'Estrofa 1', text: 'Estrofa 1' }],
    };

    const updatedSong: SongItem = {
      ...initialSong,
      title: 'Sublime Gracia (Nueva)',
      musicalKey: 'A',
    };

    vi.spyOn(presenterApi, 'listSongs').mockResolvedValue([initialSong]);
    vi.spyOn(presenterApi, 'listBibleVersions').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'listAnnouncements').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'listPlaylists').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'getThemes').mockResolvedValue({
      projector: {
        fontFamily: 'Inter',
        fontSizePx: 64,
        textColor: '#FFFFFF',
        backgroundColor: '#0B0D12',
        alignment: 'Center',
      },
      obs: {
        fontFamily: 'Inter',
        fontSizePx: 34,
        textColor: '#FFFFFF',
        boxBackgroundColor: '#0F1115D1',
        position: 'Bottom',
        marginHorizontalPx: 64,
        marginVerticalPx: 48,
        borderRadiusPx: 10,
      },
    });
    vi.spyOn(presenterApi, 'listMedia').mockResolvedValue([]);
    const updateSpy = vi.spyOn(presenterApi, 'updateSong').mockResolvedValue(updatedSong);
    const deleteSpy = vi.spyOn(presenterApi, 'deleteSong').mockResolvedValue({ deleted: true });

    const wrapper = mount(OperatorView, {
      props: { store: createLiveStore() },
    });
    await flushPromises();

    // Act - Entrar a edición y guardar
    await wrapper.get('[data-testid="song-edit-s1"]').trigger('click');
    const titleInput = wrapper.get('[data-testid="song-title-input"]');
    expect((titleInput.element as HTMLInputElement).value).toBe('Sublime Gracia');

    await titleInput.setValue('Sublime Gracia (Nueva)');
    await wrapper.get('[data-testid="song-save-btn"]').trigger('click');
    await flushPromises();

    // Assert - Guardado
    expect(updateSpy).toHaveBeenCalledWith(
      's1',
      'Sublime Gracia (Nueva)',
      'Estrofa 1',
      'John Newton',
      'G'
    );

    // Act - Volver a editar y eliminar la misma canción en edición
    await wrapper.get('[data-testid="song-edit-s1"]').trigger('click');
    expect(
      (wrapper.get('[data-testid="song-title-input"]').element as HTMLInputElement).value
    ).toBe('Sublime Gracia');
    await wrapper.get('[data-testid="song-delete-s1"]').trigger('click');
    await flushPromises();

    // Assert - Resetea formulario al eliminar la canción actualmente en edición
    expect(deleteSpy).toHaveBeenCalledWith('s1');
    expect(
      (wrapper.get('[data-testid="song-title-input"]').element as HTMLInputElement).value
    ).toBe('');
    expect(wrapper.find('[data-testid="song-cancel-edit-btn"]').exists()).toBe(false);
  });

  it('busca letras en internet y carga la letra formateada en el formulario', async () => {
    // Arrange
    vi.spyOn(presenterApi, 'listSongs').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'listBibleVersions').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'listAnnouncements').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'listPlaylists').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'getThemes').mockResolvedValue({
      projector: {
        fontFamily: 'Inter',
        fontSizePx: 64,
        textColor: '#FFFFFF',
        backgroundColor: '#0B0D12',
        alignment: 'Center',
      },
      obs: {
        fontFamily: 'Inter',
        fontSizePx: 34,
        textColor: '#FFFFFF',
        boxBackgroundColor: '#0F1115D1',
        position: 'Bottom',
        marginHorizontalPx: 64,
        marginVerticalPx: 48,
        borderRadiusPx: 10,
      },
    });
    vi.spyOn(presenterApi, 'listMedia').mockResolvedValue([]);
    vi.spyOn(presenterApi, 'searchOnlineLyrics').mockResolvedValue([
      {
        trackName: 'Cuán Grande Es Él',
        artistName: 'Marco Barrientos',
        formattedLyrics: 'Señor mi Dios\n\nCuán grande es Él',
      },
    ]);

    const wrapper = mount(OperatorView, {
      props: { store: createLiveStore() },
    });
    await flushPromises();

    // Act
    await wrapper.get('[data-testid="online-lyrics-input"]').setValue('Cuan grande es el');
    await wrapper.get('[data-testid="online-lyrics-search-btn"]').trigger('click');
    await flushPromises();
    await wrapper.get('[data-testid="online-lyrics-use-0"]').trigger('click');

    // Assert
    expect(
      (wrapper.get('[data-testid="song-title-input"]').element as HTMLInputElement).value
    ).toBe('Cuán Grande Es Él');
    expect(
      (wrapper.get('[data-testid="song-lyrics-input"]').element as HTMLTextAreaElement).value
    ).toBe('Señor mi Dios\n\nCuán grande es Él');
  });
});
