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
const selectedCanonicalBook = ref<CanonicalBibleBook | null>(null);
const selectedBookSummary = ref<BibleBookSummaryItem | null>(null);
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
  if (!selectedBookSummary.value) {
    return [];
  }
  const count = Math.max(selectedBookSummary.value.chapterCount, 1);
  return Array.from({ length: count }, (_, i) => i + 1);
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
  const available = resolveAvailableBook(canonical);
  if (!available) {
    return;
  }
  selectedCanonicalBook.value = canonical;
  selectedBookSummary.value = available;
}

function selectChapter(chapter: number) {
  if (!selectedBookSummary.value) {
    return;
  }
  emit('loadChapter', selectedBookSummary.value.bookNumber, chapter);
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
    <div class="form-row">
      <select
        :value="selectedVersionId"
        @change="emit('update:selectedVersionId', ($event.target as HTMLSelectElement).value)"
      >
        <option v-for="v in versions" :key="v.id" :value="v.id">
          {{ v.abbreviation }} - {{ v.name }}
        </option>
      </select>

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
        Tabla Periódica
      </button>
    </div>

    <div v-if="showPeriodicTable" class="periodic-table">
      <div class="testament-group">
        <h3>Antiguo Testamento</h3>
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
                available: Boolean(resolveAvailableBook(tile)),
                selected: selectedCanonicalBook?.number === tile.number,
              },
            ]"
            :disabled="!resolveAvailableBook(tile)"
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
        <h3>Nuevo Testamento</h3>
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
                available: Boolean(resolveAvailableBook(tile)),
                selected: selectedCanonicalBook?.number === tile.number,
              },
            ]"
            :disabled="!resolveAvailableBook(tile)"
            :title="tile.name"
            @click="selectBookTile(tile)"
          >
            <span class="tile-num">{{ tile.number }}</span>
            <strong class="tile-abbr">{{ tile.abbr }}</strong>
            <span class="tile-name">{{ tile.name }}</span>
          </button>
        </div>
      </div>

      <div v-if="selectedBookSummary" class="chapter-grid-wrap">
        <h4>Capítulos — {{ selectedBookSummary.bookName }}</h4>
        <div class="chapters-grid">
          <button
            v-for="ch in chapterNumbers"
            :key="ch"
            type="button"
            :data-testid="`bible-chapter-${ch}`"
            class="chapter-btn"
            @click="selectChapter(ch)"
          >
            {{ ch }}
          </button>
        </div>
      </div>
    </div>

    <div class="form-row">
      <input v-model="bibleImportName" placeholder="Nombre versión" />
      <input v-model="bibleImportAbbr" placeholder="Abrev." />
      <input
        type="file"
        accept=".xml"
        @change="(e) => (bibleFile = (e.target as HTMLInputElement).files?.[0] ?? null)"
      />
      <button type="button" @click="submitImport">Importar XML</button>
    </div>

    <ul class="item-list">
      <li v-for="v in verses" :key="`${v.bookNumber}-${v.chapter}-${v.verse}`">
        <span>{{ v.bookName }} {{ v.chapter }}:{{ v.verse }} — {{ v.text }}</span>
        <button
          type="button"
          :data-testid="`bible-verse-project-${v.verse}`"
          @click="emit('projectVerse', v)"
        >
          Proyectar
        </button>
      </li>
    </ul>
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
  opacity: 0.35;
  font-size: 0.7rem;
}

.book-tile.available {
  opacity: 1;
  cursor: pointer;
}

.book-tile.selected {
  outline: 2px solid #ffffff;
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

.chapters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(40px, 1fr));
  gap: 0.3rem;
  margin-top: 0.35rem;
}

.chapter-btn {
  padding: 0.3rem;
  font-size: 0.8rem;
}

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
</style>
