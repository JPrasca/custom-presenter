import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import BibleNavigator from '../src/components/BibleNavigator.vue';
import type {
  BibleBookSummaryItem,
  BibleVerseItem,
  BibleVersionItem,
} from '../src/services/presenterApi';

describe('BibleNavigator — Autocompletado y Tabla Periódica (HU-16)', () => {
  const versions: BibleVersionItem[] = [
    { id: 'v1', name: 'Reina Valera 1960', abbreviation: 'RVR60' },
  ];

  const books: BibleBookSummaryItem[] = [{ bookNumber: 1, bookName: 'Salmos', chapterCount: 150 }];

  const verses: BibleVerseItem[] = [
    {
      versionId: 'v1',
      bookNumber: 1,
      bookName: 'Salmos',
      chapter: 23,
      verse: 1,
      text: 'Jehová es mi pastor; nada me faltará.',
    },
  ];

  it('filtra sugerencias de autocompletado al escribir parte del nombre del libro', async () => {
    // Arrange
    const wrapper = mount(BibleNavigator, {
      props: {
        versions,
        selectedVersionId: 'v1',
        books,
        verses,
        query: '',
      },
    });

    // Act
    const input = wrapper.get('[data-testid="bible-search-input"]');
    await input.setValue('sal');

    // Assert
    const suggestion = wrapper.get('[data-testid="bible-autocomplete-item"]');
    expect(suggestion.text()).toContain('Salmos');
  });

  it('navega por Tabla Periódica en 3 clics (Libro -> Capítulo -> Versículo)', async () => {
    // Arrange
    const onLoadChapter = vi.fn();
    const onProjectVerse = vi.fn();

    const wrapper = mount(BibleNavigator, {
      props: {
        versions,
        selectedVersionId: 'v1',
        books,
        verses,
        query: '',
        onLoadChapter,
        onProjectVerse,
      },
    });

    // Act - Abrir Tabla Periódica y elegir Salmos (Sal) -> Capítulo 23 -> Versículo 1
    await wrapper.get('[data-testid="btn-toggle-periodic-table"]').trigger('click');
    const tiles = wrapper.findAll('[data-testid^="bible-tile-"]');
    expect(tiles.length).toBe(66);

    await wrapper.get('[data-testid="bible-tile-19"]').trigger('click');
    await wrapper.get('[data-testid="bible-chapter-23"]').trigger('click');
    await wrapper.get('[data-testid="bible-verse-project-1"]').trigger('click');

    // Assert
    expect(onLoadChapter).toHaveBeenCalledWith(1, 23);
    expect(onProjectVerse).toHaveBeenCalledWith(verses[0]);
  });

  it('HU-19: todos los 66 libros canónicos están disponibles y permite proyectar desde botonera de versículos', async () => {
    // Arrange
    const onLoadChapter = vi.fn();
    const onProjectVerse = vi.fn();

    const wrapper = mount(BibleNavigator, {
      props: {
        versions,
        selectedVersionId: 'v1',
        books: [], // Sin libros descargados previamente (carga a demanda)
        verses,
        query: '',
        onLoadChapter,
        onProjectVerse,
      },
    });

    // Act - Abrir tabla y verificar que Juan (libro 43) está habilitado
    await wrapper.get('[data-testid="btn-toggle-periodic-table"]').trigger('click');
    const juanTile = wrapper.get('[data-testid="bible-tile-43"]');
    expect(juanTile.attributes('disabled')).toBeUndefined();

    // Seleccionar Juan -> Capítulos canónicos (21)
    await juanTile.trigger('click');
    expect(wrapper.find('[data-testid="bible-chapter-21"]').exists()).toBe(true);

    // Seleccionar Capítulo 23 (de los versículos cargados) y pulsar versículo 1 en botonera
    await wrapper.get('[data-testid="bible-chapter-1"]').trigger('click');
    expect(onLoadChapter).toHaveBeenCalledWith(43, 1);

    // Pulsar botón de versículo en la botonera
    const verseBtn = wrapper.get('[data-testid="bible-verse-tile-1"]');
    await verseBtn.trigger('click');

    // Assert
    expect(onProjectVerse).toHaveBeenCalledWith(verses[0]);

    // Verificar sección colapsable de importación de archivo
    expect(wrapper.find('[data-testid="bible-import-name"]').exists()).toBe(false);
    await wrapper.get('[data-testid="btn-toggle-import-xml"]').trigger('click');
    expect(wrapper.find('input[type="file"]').exists()).toBe(true);
  });
});
