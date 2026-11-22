<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BibleNavigator from '../../components/BibleNavigator.vue';
import StatusBadge from '../../components/StatusBadge.vue';
import { useOperatorShortcuts } from '../../composables/useOperatorShortcuts';
import { CANONICAL_BIBLE_BOOKS, normalizeBookText } from '../../data/bibleBooks';
import {
  presenterApi,
  type AnnouncementItem,
  type BibleBookSummaryItem,
  type BibleVerseItem,
  type BibleVersionItem,
  type MediaAssetDto,
  type OnlineLyricsResult,
  type PlaylistDto,
  type SongItem,
} from '../../services/presenterApi';
import {
  createDefaultThemeSettings,
  createLiveStore,
  isTextVisible,
  type LiveStore,
} from '../../stores/live';
import type { ObsTheme, ProjectorTheme, SlideContent } from '../../types/live';

interface Props {
  store?: LiveStore;
}

type LibraryTab =
  'songs' | 'bibles' | 'announcements' | 'playlists' | 'themes' | 'media' | 'backup';

const DEFAULT_PREVIEW_SLIDES: SlideContent[] = [
  {
    id: 'demo-1',
    title: 'Bienvenida',
    text: 'Bienvenidos a nuestra reunión',
  },
  {
    id: 'demo-2',
    title: 'Salmos 23:1',
    text: 'El Señor es mi pastor; nada me faltará.',
    reference: 'Salmos 23:1',
  },
];

const LIBRARY_TABS: { id: LibraryTab; label: string }[] = [
  { id: 'songs', label: 'Canciones' },
  { id: 'bibles', label: 'Biblia' },
  { id: 'announcements', label: 'Anuncios' },
  { id: 'playlists', label: 'Playlist' },
  { id: 'themes', label: 'Temas' },
  { id: 'media', label: 'Fondos' },
  { id: 'backup', label: 'Respaldo' },
];

const props = defineProps<Props>();
const activeStore = props.store ?? createLiveStore(undefined, DEFAULT_PREVIEW_SLIDES);
const searchQuery = ref('');
const activeTab = ref<LibraryTab>('songs');
const errorMessage = ref('');
const statusNotice = ref('');

const songs = ref<SongItem[]>([]);
const editingSongId = ref<string | null>(null);
const songTitle = ref('');
const songAuthor = ref('');
const songKey = ref('');
const songLyrics = ref('');
const onlineLyricsQuery = ref('');
const onlineLyricsResults = ref<OnlineLyricsResult[]>([]);

const bibleVersions = ref<BibleVersionItem[]>([]);
const selectedVersionId = ref('');
const bibleBooks = ref<BibleBookSummaryItem[]>([]);
const bibleQuery = ref('');
const bibleVerses = ref<BibleVerseItem[]>([]);

const announcements = ref<AnnouncementItem[]>([]);
const announcementTitle = ref('');
const announcementBody = ref('');

const playlists = ref<PlaylistDto[]>([]);
const playlistName = ref('');

const defaults = createDefaultThemeSettings();
const projectorTheme = ref<ProjectorTheme>({ ...defaults.projector });
const obsTheme = ref<ObsTheme>({ ...defaults.obs });

const mediaAssets = ref<MediaAssetDto[]>([]);
const mediaFile = ref<File | null>(null);

const backupJsonInput = ref('');

const statusLabel = computed(() => {
  switch (activeStore.effectiveMode.value) {
    case 'Slide':
      return 'En vivo';
    case 'ClearText':
      return 'Texto limpio';
    case 'Blackout':
      return 'Blackout';
    case 'Logo':
      return 'Logo';
    default:
      return 'En reposo';
  }
});

const liveTextVisible = computed(() => isTextVisible(activeStore.state.value));

useOperatorShortcuts({
  nextSlide: activeStore.nextSlide,
  prevSlide: activeStore.prevSlide,
  toggleClearText: activeStore.toggleClearText,
  toggleBlackout: activeStore.toggleBlackout,
  toggleLogo: activeStore.toggleLogo,
});

async function runAction(action: () => Promise<void>) {
  errorMessage.value = '';
  statusNotice.value = '';
  try {
    await action();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Error inesperado.';
  }
}

async function refreshLibraries() {
  if (typeof fetch === 'undefined') {
    return;
  }

  try {
    const [songList, versionList, annList, plList, themeData, mediaList] = await Promise.all([
      presenterApi.listSongs(searchQuery.value),
      presenterApi.listBibleVersions(),
      presenterApi.listAnnouncements(),
      presenterApi.listPlaylists(),
      presenterApi.getThemes(),
      presenterApi.listMedia(),
    ]);
    songs.value = songList;
    bibleVersions.value = versionList;
    if (!selectedVersionId.value && versionList.length > 0) {
      selectedVersionId.value = versionList[0].id;
    }
    announcements.value = annList;
    playlists.value = plList;
    projectorTheme.value = { ...themeData.projector };
    obsTheme.value = { ...themeData.obs };
    mediaAssets.value = mediaList;
  } catch {
    // En modo aislado o sin servidor HTTP activo mantiene el preview local.
  }
}

function resetSongForm() {
  editingSongId.value = null;
  songTitle.value = '';
  songAuthor.value = '';
  songKey.value = '';
  songLyrics.value = '';
}

function startEditSong(song: SongItem) {
  editingSongId.value = song.id;
  songTitle.value = song.title;
  songAuthor.value = song.author ?? '';
  songKey.value = song.musicalKey ?? '';
  songLyrics.value = song.rawLyrics ?? song.slides.map((s) => s.text).join('\n\n');
}

async function handleSaveSong() {
  await runAction(async () => {
    if (editingSongId.value) {
      await presenterApi.updateSong(
        editingSongId.value,
        songTitle.value,
        songLyrics.value,
        songAuthor.value || undefined,
        songKey.value || undefined
      );
      statusNotice.value = 'Canción actualizada.';
    } else {
      await presenterApi.createSong(
        songTitle.value,
        songLyrics.value,
        songAuthor.value || undefined,
        songKey.value || undefined
      );
      statusNotice.value = 'Canción guardada.';
    }
    resetSongForm();
    songs.value = await presenterApi.listSongs(searchQuery.value);
  });
}

async function handleDeleteSong(id: string) {
  await runAction(async () => {
    await presenterApi.deleteSong(id);
    if (editingSongId.value === id) {
      resetSongForm();
    }
    songs.value = await presenterApi.listSongs(searchQuery.value);
  });
}

async function handleSearchOnlineLyrics() {
  await runAction(async () => {
    onlineLyricsResults.value = await presenterApi.searchOnlineLyrics(onlineLyricsQuery.value);
    if (onlineLyricsResults.value.length === 0) {
      statusNotice.value = 'No se encontraron letras en internet para esa búsqueda.';
    }
  });
}

function useOnlineLyricsResult(result: OnlineLyricsResult) {
  editingSongId.value = null;
  songTitle.value = result.trackName;
  songAuthor.value = result.artistName;
  songLyrics.value = result.formattedLyrics;
  onlineLyricsResults.value = [];
  statusNotice.value = 'Letra cargada y formateada por estrofas. Revisa y pulsa Guardar.';
}

async function handleProjectSong(song: SongItem) {
  const mapped: SlideContent[] = song.slides.map((s) => ({
    id: `${song.id}:${s.index}`,
    title: song.title,
    text: s.text,
  }));
  activeStore.setSlides(mapped);
  await runAction(async () => {
    const state = await presenterApi.projectSong(song.id, 0);
    await activeStore.syncState(state);
  });
}

async function handleSearchBible() {
  if (!selectedVersionId.value) {
    return;
  }
  await runAction(async () => {
    bibleVerses.value = await presenterApi.searchBible(selectedVersionId.value, bibleQuery.value);
  });
}

async function refreshBibleBooks(versionId: string) {
  if (!versionId || typeof fetch === 'undefined') {
    bibleBooks.value = [];
    return;
  }
  try {
    bibleBooks.value = await presenterApi.listBibleBooks(versionId);
  } catch {
    bibleBooks.value = [];
  }
}

watch(selectedVersionId, (versionId) => {
  void refreshBibleBooks(versionId);
});

async function handleLoadChapter(bookNumber: number, chapter: number) {
  if (!selectedVersionId.value) {
    return;
  }
  await runAction(async () => {
    bibleVerses.value = await presenterApi.getBibleChapterVerses(
      selectedVersionId.value,
      bookNumber,
      chapter
    );
  });
}

async function handleImportBible(name: string, abbr: string, file: File) {
  await runAction(async () => {
    await presenterApi.importBibleXml(name, abbr, file);
    bibleVersions.value = await presenterApi.listBibleVersions();
    if (bibleVersions.value.length > 0) {
      selectedVersionId.value = bibleVersions.value[0].id;
      await refreshBibleBooks(selectedVersionId.value);
    }
  });
}

async function handleProjectBibleVerse(verse: BibleVerseItem) {
  const version = bibleVersions.value.find((v) => v.id === verse.versionId);
  const abbr = version?.abbreviation ? ` (${version.abbreviation})` : '';
  const chapterSlides: SlideContent[] = bibleVerses.value.map((item) => ({
    id: `${item.versionId}:${item.bookNumber}:${item.chapter}:${item.verse}`,
    title: `${item.bookName} ${item.chapter}:${item.verse}${abbr}`,
    text: item.text,
    reference: `${item.bookName} ${item.chapter}:${item.verse}${abbr}`,
  }));
  if (chapterSlides.length > 0) {
    activeStore.setSlides(chapterSlides);
  }
  await runAction(async () => {
    const state = await presenterApi.projectBibleVerse(verse, version?.abbreviation ?? '');
    await activeStore.syncState(state);
  });
}

interface ActiveBibleSlideInfo {
  versionId?: string;
  versionAbbr?: string;
  bookNumber: number;
  chapter: number;
  verse: number;
  bookName?: string;
}

const activeBibleSlide = computed<ActiveBibleSlideInfo | null>(() => {
  const slide = activeStore.state.value.currentSlide;
  if (!slide) {
    return null;
  }

  const parts = slide.id.split(':');
  if (parts.length >= 4) {
    const bookNumber = parseInt(parts[1], 10);
    const chapter = parseInt(parts[2], 10);
    const verse = parseInt(parts[3], 10);
    if (!Number.isNaN(bookNumber) && !Number.isNaN(chapter) && !Number.isNaN(verse)) {
      const version = bibleVersions.value.find((v) => v.id === parts[0]);
      return {
        versionId: parts[0],
        versionAbbr: version?.abbreviation,
        bookNumber,
        chapter,
        verse,
      };
    }
  }

  const refText = (slide.reference || slide.title || '').trim();
  if (refText) {
    const colonIdx = refText.indexOf(':');
    if (colonIdx > 0) {
      const beforeColon = refText.slice(0, colonIdx).trim();
      const afterColon = refText.slice(colonIdx + 1).trim();
      const spaceIdx = beforeColon.lastIndexOf(' ');
      if (spaceIdx > 0) {
        const bookName = beforeColon.slice(0, spaceIdx).trim();
        const chapter = parseInt(beforeColon.slice(spaceIdx + 1), 10);
        const parenIdx = afterColon.indexOf('(');
        const verseStr = parenIdx >= 0 ? afterColon.slice(0, parenIdx).trim() : afterColon.trim();
        const verse = parseInt(verseStr.split('-')[0], 10);
        const abbr =
          parenIdx >= 0
            ? afterColon
                .slice(parenIdx + 1)
                .replace(')', '')
                .trim()
            : undefined;

        if (!Number.isNaN(chapter) && !Number.isNaN(verse)) {
          const version = bibleVersions.value.find(
            (v) =>
              (abbr && v.abbreviation.toUpperCase() === abbr.toUpperCase()) ||
              (selectedVersionId.value && v.id === selectedVersionId.value)
          );
          const canonical = CANONICAL_BIBLE_BOOKS.find(
            (b) => normalizeBookText(b.name) === normalizeBookText(bookName)
          );
          return {
            versionId: version?.id,
            versionAbbr: version?.abbreviation ?? abbr,
            bookNumber: canonical?.number ?? 1,
            chapter,
            verse,
            bookName,
          };
        }
      }
    }
  }

  return null;
});

async function handleHotSwitchVersion(targetVersion: BibleVersionItem) {
  const info = activeBibleSlide.value;
  if (!info) {
    return;
  }

  await runAction(async () => {
    let bookNumber = info.bookNumber;
    if (!bookNumber && info.bookName) {
      const canonical = CANONICAL_BIBLE_BOOKS.find(
        (b) => normalizeBookText(b.name) === normalizeBookText(info.bookName!)
      );
      if (canonical) {
        bookNumber = canonical.number;
      }
    }

    const verses = await presenterApi.getBibleChapterVerses(
      targetVersion.id,
      bookNumber,
      info.chapter
    );

    const targetVerse = verses.find((v) => v.verse === info.verse) ?? verses[0];
    if (!targetVerse) {
      return;
    }

    selectedVersionId.value = targetVersion.id;
    bibleVerses.value = verses;
    await handleProjectBibleVerse(targetVerse);
    statusNotice.value = `Versión cambiada a ${targetVersion.abbreviation} en vivo.`;
  });
}

function isHotVersionActive(version: BibleVersionItem): boolean {
  if (!activeBibleSlide.value) {
    return false;
  }
  const currentAbbr = (activeBibleSlide.value.versionAbbr ?? '').toUpperCase();
  return (
    currentAbbr === version.abbreviation.toUpperCase() ||
    activeBibleSlide.value.versionId === version.id
  );
}

async function handleCreateAnnouncement() {
  await runAction(async () => {
    await presenterApi.createAnnouncement(announcementTitle.value, announcementBody.value);
    announcementTitle.value = '';
    announcementBody.value = '';
    announcements.value = await presenterApi.listAnnouncements();
  });
}

async function handleProjectAnnouncement(ann: AnnouncementItem) {
  await runAction(async () => {
    const state = await presenterApi.projectAnnouncement(ann.id, 0);
    await activeStore.syncState(state);
  });
}

async function handleCreatePlaylist() {
  await runAction(async () => {
    await presenterApi.createPlaylist(playlistName.value);
    playlistName.value = '';
    playlists.value = await presenterApi.listPlaylists();
  });
}

async function handleProjectPlaylistItem(playlistId: string, itemIndex: number) {
  await runAction(async () => {
    const state = await presenterApi.projectPlaylistItem(playlistId, itemIndex);
    await activeStore.syncState(state);
  });
}

async function handleSaveThemes() {
  await runAction(async () => {
    await presenterApi.updateProjectorTheme(projectorTheme.value);
    const state = await presenterApi.updateObsTheme(obsTheme.value);
    await activeStore.syncState(state);
    statusNotice.value = 'Temas actualizados en tiempo real.';
  });
}

async function handleUploadMedia() {
  if (!mediaFile.value) {
    return;
  }
  await runAction(async () => {
    await presenterApi.uploadMedia(mediaFile.value!);
    mediaAssets.value = await presenterApi.listMedia();
  });
}

async function handleAssignMedia(id: string) {
  await runAction(async () => {
    const state = await presenterApi.assignMedia(id);
    await activeStore.syncState(state);
  });
}

async function handleClearMedia() {
  await runAction(async () => {
    const state = await presenterApi.clearMedia();
    await activeStore.syncState(state);
  });
}

async function handleImportBackup() {
  await runAction(async () => {
    const summary = await presenterApi.importBackupJson(backupJsonInput.value);
    statusNotice.value = `Restauradas ${summary.importedSongs} canciones y ${summary.importedAnnouncements} anuncios.`;
    await refreshLibraries();
  });
}

onMounted(() => {
  void refreshLibraries();
});
</script>

<template>
  <main class="operator">
    <header class="topbar">
      <div class="brand">
        <h1>Custom Presenter</h1>
        <StatusBadge
          data-testid="live-status"
          :label="statusLabel"
          :active="activeStore.effectiveMode.value !== 'Idle'"
        />
      </div>
      <input
        v-model="searchQuery"
        data-testid="search-input"
        type="search"
        class="search-input"
        placeholder="Buscar canción, versículo o anuncio..."
        @input="refreshLibraries"
      />
    </header>

    <p v-if="errorMessage" role="alert" class="alert-error">{{ errorMessage }}</p>
    <p v-if="statusNotice" class="alert-ok">{{ statusNotice }}</p>

    <div class="operator-dashboard">
      <!-- Columna Izquierda: Biblioteca y Contenido (~60%) -->
      <section class="panel library-section col-library" aria-label="Biblioteca y gestión">
        <nav class="tabs">
          <button
            v-for="tab in LIBRARY_TABS"
            :key="tab.id"
            type="button"
            :data-testid="`tab-${tab.id}`"
            :class="{ active: activeTab === tab.id }"
            @click="activeTab = tab.id"
          >
            {{ tab.label }}
          </button>
        </nav>

        <div class="tab-content-scroll">
          <div v-if="activeTab === 'songs'" data-testid="panel-songs" class="tab-panel">
            <div class="form-row">
              <input
                v-model="onlineLyricsQuery"
                data-testid="online-lyrics-input"
                placeholder="Buscar letra en internet (ej. Sublime Gracia)..."
                @keydown.enter="handleSearchOnlineLyrics"
              />
              <button
                type="button"
                data-testid="online-lyrics-search-btn"
                @click="handleSearchOnlineLyrics"
              >
                Buscar letra online
              </button>
            </div>
            <ul v-if="onlineLyricsResults.length > 0" class="item-list">
              <li v-for="(item, idx) in onlineLyricsResults" :key="`${item.trackName}-${idx}`">
                <span>{{ item.trackName }} — {{ item.artistName }}</span>
                <button
                  type="button"
                  :data-testid="`online-lyrics-use-${idx}`"
                  @click="useOnlineLyricsResult(item)"
                >
                  Usar letra
                </button>
              </li>
            </ul>
            <div class="form-row">
              <input v-model="songTitle" data-testid="song-title-input" placeholder="Título" />
              <input v-model="songAuthor" placeholder="Autor" />
              <input v-model="songKey" placeholder="Tono" />
            </div>
            <textarea
              v-model="songLyrics"
              data-testid="song-lyrics-input"
              rows="4"
              placeholder="Letra por estrofas..."
            />
            <div class="form-row">
              <button type="button" data-testid="song-save-btn" @click="handleSaveSong">
                {{ editingSongId ? 'Actualizar canción' : 'Guardar canción' }}
              </button>
              <button
                v-if="editingSongId"
                type="button"
                data-testid="song-cancel-edit-btn"
                @click="resetSongForm"
              >
                Cancelar edición
              </button>
            </div>
            <ul class="item-list">
              <li v-for="song in songs" :key="song.id">
                <span>{{ song.title }}</span>
                <div class="form-row">
                  <button type="button" @click="handleProjectSong(song)">Proyectar</button>
                  <button
                    type="button"
                    :data-testid="`song-edit-${song.id}`"
                    @click="startEditSong(song)"
                  >
                    Editar
                  </button>
                  <button
                    type="button"
                    :data-testid="`song-delete-${song.id}`"
                    @click="handleDeleteSong(song.id)"
                  >
                    Eliminar
                  </button>
                </div>
              </li>
            </ul>
          </div>

          <BibleNavigator
            v-else-if="activeTab === 'bibles'"
            :versions="bibleVersions"
            :selected-version-id="selectedVersionId"
            :books="bibleBooks"
            :verses="bibleVerses"
            :query="bibleQuery"
            @update:selected-version-id="(v) => (selectedVersionId = v)"
            @update:query="(q) => (bibleQuery = q)"
            @search="handleSearchBible"
            @load-chapter="handleLoadChapter"
            @project-verse="handleProjectBibleVerse"
            @import-bible="handleImportBible"
          />

          <div
            v-else-if="activeTab === 'announcements'"
            data-testid="panel-announcements"
            class="tab-panel"
          >
            <input v-model="announcementTitle" placeholder="Título del anuncio" />
            <textarea v-model="announcementBody" rows="3" placeholder="Texto del anuncio..." />
            <button type="button" @click="handleCreateAnnouncement">Guardar anuncio</button>
            <ul class="item-list">
              <li v-for="ann in announcements" :key="ann.id">
                <span>{{ ann.title }}</span>
                <button type="button" @click="handleProjectAnnouncement(ann)">Proyectar</button>
              </li>
            </ul>
          </div>

          <div
            v-else-if="activeTab === 'playlists'"
            data-testid="panel-playlists"
            class="tab-panel"
          >
            <div class="form-row">
              <input v-model="playlistName" placeholder="Nombre de la playlist" />
              <button type="button" @click="handleCreatePlaylist">Crear playlist</button>
            </div>
            <ul class="item-list">
              <li v-for="pl in playlists" :key="pl.id">
                <span>{{ pl.name }} ({{ pl.items.length }} ítems)</span>
                <button
                  v-if="pl.items.length > 0"
                  type="button"
                  @click="handleProjectPlaylistItem(pl.id, 0)"
                >
                  Iniciar
                </button>
              </li>
            </ul>
          </div>

          <div v-else-if="activeTab === 'themes'" data-testid="panel-themes" class="tab-panel">
            <div class="form-row">
              <label>
                Proyector px
                <input
                  v-model.number="projectorTheme.fontSizePx"
                  type="number"
                  min="16"
                  max="160"
                />
              </label>
              <label>
                Color texto
                <input v-model="projectorTheme.textColor" />
              </label>
              <label>
                OBS px
                <input v-model.number="obsTheme.fontSizePx" type="number" min="14" max="120" />
              </label>
              <button type="button" @click="handleSaveThemes">Guardar temas</button>
            </div>
          </div>

          <div v-else-if="activeTab === 'media'" data-testid="panel-media" class="tab-panel">
            <div class="form-row">
              <input
                type="file"
                accept=".png,.jpg,.jpeg,.webp,.mp4,.webm"
                @change="(e) => (mediaFile = (e.target as HTMLInputElement).files?.[0] ?? null)"
              />
              <button type="button" @click="handleUploadMedia">Subir fondo</button>
              <button type="button" @click="handleClearMedia">Quitar fondo</button>
            </div>
            <ul class="item-list">
              <li v-for="m in mediaAssets" :key="m.id">
                <span>{{ m.originalName }} ({{ m.kind }})</span>
                <button type="button" @click="handleAssignMedia(m.id)">Usar fondo</button>
              </li>
            </ul>
          </div>

          <div v-else-if="activeTab === 'backup'" data-testid="panel-backup" class="tab-panel">
            <div class="form-row">
              <a :href="presenterApi.exportBackupUrl()" download="custom-presenter-backup.json">
                Descargar respaldo JSON
              </a>
            </div>
            <textarea v-model="backupJsonInput" rows="4" placeholder="Pegar JSON de respaldo..." />
            <button type="button" @click="handleImportBackup">Importar respaldo</button>
          </div>
        </div>
      </section>

      <!-- Columna Derecha: Monitoreo Live, Controles y Preview (~40%) -->
      <aside class="col-control" aria-label="Monitoreo y Controles">
        <!-- Monitor Live con Switch de Versiones en cabecera -->
        <div class="panel live-panel">
          <div class="panel-header">
            <h2>Live</h2>
            <div v-if="activeBibleSlide" class="hot-versions-bar" data-testid="hot-versions-bar">
              <span class="hot-version-label">Versión:</span>
              <button
                v-for="v in bibleVersions"
                :key="v.id"
                type="button"
                :data-testid="`hot-version-${v.abbreviation}`"
                class="btn-hot-version"
                :class="{ active: isHotVersionActive(v) }"
                :title="v.name"
                @click="handleHotSwitchVersion(v)"
              >
                {{ v.abbreviation }}
              </button>
            </div>
          </div>
          <div class="live-monitor" :class="{ blackout: activeStore.state.value.flags.blackout }">
            <p v-if="liveTextVisible && activeStore.state.value.currentSlide" class="live-text">
              {{ activeStore.state.value.currentSlide.text }}
            </p>
            <p v-else class="live-placeholder">
              {{ statusLabel }}
            </p>
          </div>
        </div>

        <!-- Barra de Controles en vivo -->
        <div class="panel controls-panel">
          <section class="controls" aria-label="Controles en vivo">
            <button type="button" data-testid="btn-prev" @click="activeStore.prevSlide">
              Anterior [↑]
            </button>
            <button type="button" data-testid="btn-next" @click="activeStore.nextSlide">
              Siguiente [↓]
            </button>
            <button
              type="button"
              data-testid="btn-clear"
              :class="{ active: activeStore.state.value.flags.clearText }"
              @click="activeStore.toggleClearText"
            >
              Limpiar texto [C / Esc]
            </button>
            <button
              type="button"
              data-testid="btn-blackout"
              :class="{ active: activeStore.state.value.flags.blackout }"
              @click="activeStore.toggleBlackout"
            >
              Blackout [B]
            </button>
            <button
              type="button"
              data-testid="btn-logo"
              :class="{ active: activeStore.state.value.flags.logo }"
              @click="activeStore.toggleLogo"
            >
              Logo [L]
            </button>
          </section>
        </div>

        <!-- Panel Preview -->
        <div class="panel preview-panel">
          <h2>Preview</h2>
          <div class="preview-scroll">
            <ul class="slide-list">
              <li v-for="slide in activeStore.slides.value" :key="slide.id">
                <button
                  type="button"
                  class="slide-item"
                  :class="{ selected: activeStore.state.value.currentSlide?.id === slide.id }"
                  @click="activeStore.projectSlide(slide)"
                >
                  <strong>{{ slide.title }}</strong>
                  <span>{{ slide.text }}</span>
                </button>
              </li>
            </ul>
          </div>
        </div>
      </aside>
    </div>
  </main>
</template>

<style scoped>
.operator {
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  height: 100vh;
  max-height: 100vh;
  box-sizing: border-box;
  overflow: hidden;
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.search-input {
  min-width: 280px;
  padding: 0.5rem 0.75rem;
  border-radius: 0.375rem;
  border: 1px solid #2a2f3a;
  background-color: var(--bg-secondary);
  color: var(--text-primary);
}

.alert-error {
  padding: 0.4rem 0.75rem;
  border-radius: 0.375rem;
  background-color: #451a1a;
  color: #fecaca;
  margin: 0;
  flex-shrink: 0;
}

.alert-ok {
  padding: 0.4rem 0.75rem;
  border-radius: 0.375rem;
  background-color: #143625;
  color: #bbf7d0;
  margin: 0;
  flex-shrink: 0;
}

/* Dashboard de 2 Columnas Unificado */
.operator-dashboard {
  display: grid;
  grid-template-columns: minmax(400px, 1.25fr) minmax(360px, 1fr);
  gap: 1rem;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.col-library {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  overflow: hidden;
}

.tab-content-scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding-right: 0.35rem;
}

.col-control {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 0;
  overflow: hidden;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.hot-versions-bar {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.hot-version-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--text-muted);
}

.btn-hot-version {
  padding: 0.2rem 0.5rem !important;
  font-size: 0.75rem !important;
  font-weight: 700;
  border-radius: 0.25rem !important;
  background-color: #1c212b !important;
  border: 1px solid #333a46 !important;
  color: #cbd5e1 !important;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
}

.btn-hot-version:hover {
  border-color: var(--accent) !important;
  color: #ffffff !important;
}

.btn-hot-version.active {
  background-color: var(--accent) !important;
  border-color: var(--accent) !important;
  color: #ffffff !important;
  box-shadow: 0 0 6px rgba(59, 130, 246, 0.4);
}

.controls-panel {
  padding: 0.6rem 0.75rem;
  flex-shrink: 0;
}

.preview-panel {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.preview-scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding-right: 0.35rem;
}

.controls,
.tabs,
.form-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}

.controls button,
.tabs button,
.tab-panel button,
.slide-item {
  padding: 0.45rem 0.8rem;
  border-radius: 0.375rem;
  border: 1px solid #2a2f3a;
  background-color: var(--bg-secondary);
  color: var(--text-primary);
  cursor: pointer;
}

.controls button.active,
.tabs button.active,
.slide-item.selected {
  background-color: var(--accent);
  border-color: var(--accent);
}

.panel {
  background-color: var(--bg-secondary);
  border: 1px solid #2a2f3a;
  border-radius: 0.5rem;
  padding: 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.tab-panel {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.tab-panel input,
.tab-panel textarea,
.tab-panel select {
  padding: 0.45rem 0.65rem;
  border-radius: 0.375rem;
  border: 1px solid #2a2f3a;
  background-color: #0b0d11;
  color: var(--text-primary);
}

.slide-list,
.item-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.item-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}

.slide-item {
  width: 100%;
  text-align: left;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.live-monitor {
  aspect-ratio: 16 / 9;
  background-color: #0b0d11;
  border: 1px solid #2a2f3a;
  border-radius: 0.375rem;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.25rem;
  text-align: center;
  flex-shrink: 0;
}

.live-monitor.blackout {
  background-color: #000000;
}

.live-text {
  white-space: pre-line;
  font-size: 1.15rem;
}

.live-placeholder {
  color: var(--text-muted);
}

@media (max-width: 900px) {
  .operator {
    height: auto;
    max-height: none;
    overflow-y: auto;
  }

  .operator-dashboard {
    grid-template-columns: 1fr;
    height: auto;
    overflow: visible;
  }

  .tab-content-scroll,
  .preview-scroll {
    overflow-y: visible;
  }
}
</style>
