<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
  CANONICAL_BIBLE_BOOKS,
  normalizeBookText,
  type CanonicalBibleBook,
} from '../data/bibleBooks';
import type {
  BibleBookSummaryItem,
  BibleVerseItem,
  BibleVersionItem,
} from '../services/presenterApi';

interface Props {
  versions: BibleVersionItem[];
  selectedVersionId: string;
  books: BibleBookSummaryItem[];
  verses: BibleVerseItem[];
  query: string;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  'update:selectedVersionId': [value: string];
  'update:query': [value: string];
  search: [];
  loadChapter: [bookNumber: number, chapter: number];
  projectVerse: [verse: BibleVerseItem];
  importBible: [name: string, abbr: string, file: File];
}>();

const showPeriodicTable = ref(false);
const showImportForm = ref(false);
const selectedCanonicalBook = ref<CanonicalBibleBook | null>(null);
const selectedBookSummary = ref<BibleBookSummaryItem | null>(null);
const selectedChapter = ref<number | null>(null);
const selectedVerseNumber = ref<number | null>(null);

const bibleImportName = ref('');
const bibleImportAbbr = ref('');
const bibleFile = ref<File | null>(null);

const localQuery = ref(props.query);
watch(
  () => props.query,
  (val) => {
    localQuery.value = val;
  }
);

const booksByNormalizedName = computed(() => {
  const map = new Map<string, BibleBookSummaryItem>();
  for (const book of props.books) {
    map.set(normalizeBookText(book.bookName), book);
  }
  return map;
});

function resolveAvailableBook(canonical: CanonicalBibleBook): BibleBookSummaryItem | undefined {
  return (
    booksByNormalizedName.value.get(normalizeBookText(canonical.name)) ??
    props.books.find((b) => b.bookNumber === canonical.number)
  );
}

const autocompleteSuggestions = computed(() => {
  const term = normalizeBookText(localQuery.value);
  if (term.length < 2 || term.includes(':')) {
    return [];
  }

  return props.books.filter((b) => normalizeBookText(b.bookName).includes(term)).slice(0, 8);
});

const oldTestamentTiles = computed(() => CANONICAL_BIBLE_BOOKS.filter((b) => b.testament === 'AT'));
const newTestamentTiles = computed(() => CANONICAL_BIBLE_BOOKS.filter((b) => b.testament === 'NT'));

const chapterNumbers = computed(() => {
  if (!selectedCanonicalBook.value) {
    return [];
  }
  const count = selectedBookSummary.value?.chapterCount ?? selectedCanonicalBook.value.chapters;
  return Array.from({ length: Math.max(count, 1) }, (_, i) => i + 1);
});

function handleQueryInput(value: string) {
  localQuery.value = value;
  emit('update:query', value);
}

function selectAutocomplete(book: BibleBookSummaryItem) {
  const next = `${book.bookName} `;
  localQuery.value = next;
  emit('update:query', next);
}

function selectBookTile(canonical: CanonicalBibleBook) {
  selectedCanonicalBook.value = canonical;
  selectedBookSummary.value = resolveAvailableBook(canonical) ?? null;
  selectedChapter.value = null;
  selectedVerseNumber.value = null;
}

function selectChapter(chapter: number) {
  if (!selectedCanonicalBook.value) {
    return;
  }
  selectedChapter.value = chapter;
  selectedVerseNumber.value = null;
  const bookNumber = selectedBookSummary.value?.bookNumber ?? selectedCanonicalBook.value.number;
  emit('loadChapter', bookNumber, chapter);
}

function selectVerse(verse: BibleVerseItem) {
  selectedVerseNumber.value = verse.verse;
  emit('projectVerse', verse);
}

function submitImport() {
  if (!bibleFile.value) {
    return;
  }
  emit('importBible', bibleImportName.value, bibleImportAbbr.value, bibleFile.value);
  bibleImportName.value = '';
  bibleImportAbbr.value = '';
}
</script>

<template>
  <div data-testid="panel-bibles" class="bible-navigator">
    <div class="form-row header-controls">
      <div class="version-select-wrap">
        <label for="bible-version-select" class="control-label">Versión:</label>
        <select
          id="bible-version-select"
          data-testid="bible-version-select"
          :value="selectedVersionId"
          @change="emit('update:selectedVersionId', ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="v in versions" :key="v.id" :value="v.id">
            {{ v.abbreviation }} - {{ v.name }}
          </option>
        </select>
      </div>

      <div class="autocomplete-wrap">
        <input
          :value="localQuery"
          data-testid="bible-search-input"
          placeholder="Ej. Salmos 23:1 o palabra..."
          @input="handleQueryInput(($event.target as HTMLInputElement).value)"
          @keydown.enter="emit('search')"
        />
        <ul v-if="autocompleteSuggestions.length > 0" class="autocomplete-list">
          <li v-for="book in autocompleteSuggestions" :key="book.bookNumber">
            <button
              type="button"
              data-testid="bible-autocomplete-item"
              @click="selectAutocomplete(book)"
            >
              {{ book.bookName }} ({{ book.chapterCount }} cap.)
            </button>
          </li>
        </ul>
      </div>

      <button type="button" @click="emit('search')">Buscar</button>
      <button
        type="button"
        data-testid="btn-toggle-periodic-table"
        :class="{ active: showPeriodicTable }"
        @click="showPeriodicTable = !showPeriodicTable"
      >
        {{ showPeriodicTable ? 'Ocultar Tabla' : 'Tabla Periódica' }}
      </button>
    </div>

    <!-- Navegación Tabla Periódica -->
    <div v-if="showPeriodicTable" class="periodic-table">
      <div class="testament-group">
        <h3>Antiguo Testamento (39 libros)</h3>
        <div class="tiles-grid">
          <button
            v-for="tile in oldTestamentTiles"
            :key="tile.number"
            type="button"
            :data-testid="`bible-tile-${tile.number}`"
            class="book-tile"
            :class="[
              `cat-${tile.category}`,
              {
                available: true,
                selected: selectedCanonicalBook?.number === tile.number,
                local: Boolean(resolveAvailableBook(tile)),
              },
            ]"
            :title="tile.name"
            @click="selectBookTile(tile)"
          >
            <span class="tile-num">{{ tile.number }}</span>
            <strong class="tile-abbr">{{ tile.abbr }}</strong>
            <span class="tile-name">{{ tile.name }}</span>
          </button>
        </div>
      </div>

      <div class="testament-group">
        <h3>Nuevo Testamento (27 libros)</h3>
        <div class="tiles-grid">
          <button
            v-for="tile in newTestamentTiles"
            :key="tile.number"
            type="button"
            :data-testid="`bible-tile-${tile.number}`"
            class="book-tile"
            :class="[
              `cat-${tile.category}`,
              {
                available: true,
                selected: selectedCanonicalBook?.number === tile.number,
                local: Boolean(resolveAvailableBook(tile)),
              },
            ]"
            :title="tile.name"
            @click="selectBookTile(tile)"
          >
            <span class="tile-num">{{ tile.number }}</span>
            <strong class="tile-abbr">{{ tile.abbr }}</strong>
            <span class="tile-name">{{ tile.name }}</span>
          </button>
        </div>
      </div>

      <!-- Paso 2: Selector de Capítulos -->
      <div v-if="selectedCanonicalBook" class="chapter-grid-wrap">
        <h4>Paso 2: Capítulos — {{ selectedCanonicalBook.name }}</h4>
        <div class="chapters-grid">
          <button
            v-for="ch in chapterNumbers"
            :key="ch"
            type="button"
            :data-testid="`bible-chapter-${ch}`"
            class="chapter-btn"
            :class="{ selected: selectedChapter === ch }"
            @click="selectChapter(ch)"
          >
            {{ ch }}
          </button>
        </div>
      </div>

      <!-- Paso 3: Selector de Versículos (Estilo Holyrics) -->
      <div v-if="selectedChapter && verses.length > 0" class="verse-grid-wrap">
        <h4>Paso 3: Versículos — {{ selectedCanonicalBook?.name }} {{ selectedChapter }}</h4>
        <div class="verses-grid">
          <button
            v-for="v in verses"
            :key="v.verse"
            type="button"
            :data-testid="`bible-verse-tile-${v.verse}`"
            class="verse-tile-btn"
            :class="{ selected: selectedVerseNumber === v.verse }"
            @click="selectVerse(v)"
          >
            {{ v.verse }}
          </button>
        </div>
      </div>
    </div>

    <!-- Lista de versículos con texto para proyectar -->
    <div v-if="verses.length > 0" class="verses-list-wrap">
      <h4>Contenido del Capítulo / Búsqueda</h4>
      <ul class="item-list">
        <li
          v-for="v in verses"
          :key="`${v.bookNumber}-${v.chapter}-${v.verse}`"
          :class="{ active: selectedVerseNumber === v.verse }"
        >
          <span class="verse-content">
            <strong class="verse-citation">{{ v.bookName }} {{ v.chapter }}:{{ v.verse }}</strong>
            <span class="verse-text">{{ v.text }}</span>
          </span>
          <button
            type="button"
            class="btn-project"
            :data-testid="`bible-verse-project-${v.verse}`"
            @click="selectVerse(v)"
          >
            Proyectar
          </button>
        </li>
      </ul>
    </div>

    <!-- Sección colapsable para importación local técnica -->
    <div class="import-section">
      <button
        type="button"
        class="btn-toggle-import"
        data-testid="btn-toggle-import-xml"
        @click="showImportForm = !showImportForm"
      >
        {{
          showImportForm
            ? '▲ Ocultar importador de archivo'
            : '📁 Importar archivo local (XML/JSON)...'
        }}
      </button>

      <div v-if="showImportForm" class="import-box form-row">
        <input v-model="bibleImportName" placeholder="Nombre versión (ej. RVR1960)" />
        <input v-model="bibleImportAbbr" placeholder="Abreviatura" />
        <input
          type="file"
          accept=".xml,.json"
          @change="(e) => (bibleFile = (e.target as HTMLInputElement).files?.[0] ?? null)"
        />
        <button type="button" @click="submitImport">Cargar archivo</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.bible-navigator {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.form-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}

.header-controls {
  background-color: #161b24;
  padding: 0.6rem 0.75rem;
  border-radius: 0.5rem;
  border: 1px solid #2a2f3a;
}

.version-select-wrap {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.control-label {
  font-size: 0.8rem;
  color: #8c97ab;
}

.autocomplete-wrap {
  position: relative;
  min-width: 240px;
}

.autocomplete-list {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  z-index: 20;
  list-style: none;
  background-color: #161b24;
  border: 1px solid #2a2f3a;
  border-radius: 0.375rem;
  margin-top: 0.25rem;
  max-height: 220px;
  overflow-y: auto;
}

.autocomplete-list button {
  width: 100%;
  text-align: left;
  border: none;
  border-bottom: 1px solid #232936;
  border-radius: 0;
}

.periodic-table {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
  padding: 0.75rem;
  border: 1px solid #2a2f3a;
  border-radius: 0.5rem;
  background-color: #0b0d11;
}

.tiles-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(62px, 1fr));
  gap: 0.35rem;
  margin-top: 0.35rem;
}

.book-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.3rem 0.2rem;
  border-radius: 0.35rem;
  border: 1px solid #2a2f3a;
  opacity: 0.85;
  font-size: 0.7rem;
  cursor: pointer;
  transition:
    transform 0.1s ease,
    border-color 0.1s ease;
}

.book-tile:hover {
  opacity: 1;
  transform: translateY(-1px);
  border-color: #4a5568;
}

.book-tile.available {
  opacity: 1;
}

.book-tile.selected {
  outline: 2px solid #38bdf8;
  border-color: #38bdf8;
}

.tile-num {
  font-size: 0.6rem;
  opacity: 0.75;
}

.tile-abbr {
  font-size: 0.85rem;
}

.tile-name {
  font-size: 0.55rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.cat-pentateuco {
  background-color: #1e3a5f;
}
.cat-historicos {
  background-color: #1f4d3a;
}
.cat-poeticos {
  background-color: #5b3a1a;
}
.cat-profetas-mayores {
  background-color: #5a2342;
}
.cat-profetas-menores {
  background-color: #3e285c;
}
.cat-evangelios {
  background-color: #184e68;
}
.cat-hechos {
  background-color: #2f5233;
}
.cat-paulinas {
  background-color: #4f4019;
}
.cat-generales {
  background-color: #592e2e;
}
.cat-apocalipsis {
  background-color: #6b1d2f;
}

.chapter-grid-wrap,
.verse-grid-wrap {
  margin-top: 0.5rem;
  padding: 0.5rem;
  border-radius: 0.375rem;
  background-color: #131720;
  border: 1px solid #232936;
}

.chapter-grid-wrap h4,
.verse-grid-wrap h4 {
  margin: 0 0 0.35rem 0;
  font-size: 0.85rem;
  color: #94a3b8;
}

.chapters-grid,
.verses-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(42px, 1fr));
  gap: 0.35rem;
  margin-top: 0.35rem;
}

.chapter-btn,
.verse-tile-btn {
  padding: 0.35rem;
  font-size: 0.85rem;
  font-weight: 600;
  border-radius: 0.25rem;
  background-color: #1e2638;
  border: 1px solid #2d3748;
  cursor: pointer;
}

.chapter-btn:hover,
.verse-tile-btn:hover {
  background-color: #2b354f;
}

.chapter-btn.selected,
.verse-tile-btn.selected {
  background-color: #0284c7;
  border-color: #38bdf8;
  color: #ffffff;
}

.verses-list-wrap {
  margin-top: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.verses-list-wrap h4 {
  margin: 0;
  font-size: 0.85rem;
  color: #94a3b8;
}

.item-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0;
  margin: 0;
}

.item-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  background-color: #161b24;
  border: 1px solid #232936;
  border-radius: 0.375rem;
}

.item-list li.active {
  border-color: #0284c7;
  background-color: #0c2338;
}

.verse-content {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.verse-citation {
  color: #38bdf8;
  font-size: 0.85rem;
}

.verse-text {
  font-size: 0.95rem;
  color: #e2e8f0;
}

.btn-project {
  white-space: nowrap;
  background-color: #0284c7;
  color: #ffffff;
  padding: 0.35rem 0.75rem;
  border-radius: 0.25rem;
}

.import-section {
  margin-top: 0.75rem;
  padding-top: 0.5rem;
  border-top: 1px dashed #232936;
}

.btn-toggle-import {
  background: transparent;
  border: none;
  color: #64748b;
  font-size: 0.8rem;
  cursor: pointer;
  padding: 0.25rem 0.5rem;
}

.btn-toggle-import:hover {
  color: #94a3b8;
}

.import-box {
  margin-top: 0.5rem;
  padding: 0.5rem;
  background-color: #131720;
  border-radius: 0.375rem;
  border: 1px solid #232936;
}
</style>
