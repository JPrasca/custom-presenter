export type BibleCategory =
  | 'pentateuco'
  | 'historicos'
  | 'poeticos'
  | 'profetas-mayores'
  | 'profetas-menores'
  | 'evangelios'
  | 'hechos'
  | 'paulinas'
  | 'generales'
  | 'apocalipsis';

export interface CanonicalBibleBook {
  number: number;
  name: string;
  abbr: string;
  testament: 'AT' | 'NT';
  category: BibleCategory;
}

export function normalizeBookText(value: string): string {
  return value
    .normalize('NFD')
    .replaceAll(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

export const CANONICAL_BIBLE_BOOKS: CanonicalBibleBook[] = [
  { number: 1, name: 'Génesis', abbr: 'Gn', testament: 'AT', category: 'pentateuco' },
  { number: 2, name: 'Éxodo', abbr: 'Éx', testament: 'AT', category: 'pentateuco' },
  { number: 3, name: 'Levítico', abbr: 'Lv', testament: 'AT', category: 'pentateuco' },
  { number: 4, name: 'Números', abbr: 'Nm', testament: 'AT', category: 'pentateuco' },
  { number: 5, name: 'Deuteronomio', abbr: 'Dt', testament: 'AT', category: 'pentateuco' },
  { number: 6, name: 'Josué', abbr: 'Jos', testament: 'AT', category: 'historicos' },
  { number: 7, name: 'Jueces', abbr: 'Jue', testament: 'AT', category: 'historicos' },
  { number: 8, name: 'Rut', abbr: 'Rt', testament: 'AT', category: 'historicos' },
  { number: 9, name: '1 Samuel', abbr: '1S', testament: 'AT', category: 'historicos' },
  { number: 10, name: '2 Samuel', abbr: '2S', testament: 'AT', category: 'historicos' },
  { number: 11, name: '1 Reyes', abbr: '1R', testament: 'AT', category: 'historicos' },
  { number: 12, name: '2 Reyes', abbr: '2R', testament: 'AT', category: 'historicos' },
  { number: 13, name: '1 Crónicas', abbr: '1Cr', testament: 'AT', category: 'historicos' },
  { number: 14, name: '2 Crónicas', abbr: '2Cr', testament: 'AT', category: 'historicos' },
  { number: 15, name: 'Esdras', abbr: 'Esd', testament: 'AT', category: 'historicos' },
  { number: 16, name: 'Nehemías', abbr: 'Neh', testament: 'AT', category: 'historicos' },
  { number: 17, name: 'Ester', abbr: 'Est', testament: 'AT', category: 'historicos' },
  { number: 18, name: 'Job', abbr: 'Job', testament: 'AT', category: 'poeticos' },
  { number: 19, name: 'Salmos', abbr: 'Sal', testament: 'AT', category: 'poeticos' },
  { number: 20, name: 'Proverbios', abbr: 'Pr', testament: 'AT', category: 'poeticos' },
  { number: 21, name: 'Eclesiastés', abbr: 'Ec', testament: 'AT', category: 'poeticos' },
  { number: 22, name: 'Cantares', abbr: 'Cnt', testament: 'AT', category: 'poeticos' },
  { number: 23, name: 'Isaías', abbr: 'Is', testament: 'AT', category: 'profetas-mayores' },
  { number: 24, name: 'Jeremías', abbr: 'Jer', testament: 'AT', category: 'profetas-mayores' },
  { number: 25, name: 'Lamentaciones', abbr: 'Lm', testament: 'AT', category: 'profetas-mayores' },
  { number: 26, name: 'Ezequiel', abbr: 'Ez', testament: 'AT', category: 'profetas-mayores' },
  { number: 27, name: 'Daniel', abbr: 'Dn', testament: 'AT', category: 'profetas-mayores' },
  { number: 28, name: 'Oseas', abbr: 'Os', testament: 'AT', category: 'profetas-menores' },
  { number: 29, name: 'Joel', abbr: 'Jl', testament: 'AT', category: 'profetas-menores' },
  { number: 30, name: 'Amós', abbr: 'Am', testament: 'AT', category: 'profetas-menores' },
  { number: 31, name: 'Abdías', abbr: 'Abd', testament: 'AT', category: 'profetas-menores' },
  { number: 32, name: 'Jonás', abbr: 'Jon', testament: 'AT', category: 'profetas-menores' },
  { number: 33, name: 'Miqueas', abbr: 'Mi', testament: 'AT', category: 'profetas-menores' },
  { number: 34, name: 'Nahúm', abbr: 'Nah', testament: 'AT', category: 'profetas-menores' },
  { number: 35, name: 'Habacuc', abbr: 'Hab', testament: 'AT', category: 'profetas-menores' },
  { number: 36, name: 'Sofonías', abbr: 'Sof', testament: 'AT', category: 'profetas-menores' },
  { number: 37, name: 'Hageo', abbr: 'Hag', testament: 'AT', category: 'profetas-menores' },
  { number: 38, name: 'Zacarías', abbr: 'Zac', testament: 'AT', category: 'profetas-menores' },
  { number: 39, name: 'Malaquías', abbr: 'Mal', testament: 'AT', category: 'profetas-menores' },
  { number: 40, name: 'Mateo', abbr: 'Mt', testament: 'NT', category: 'evangelios' },
  { number: 41, name: 'Marcos', abbr: 'Mr', testament: 'NT', category: 'evangelios' },
  { number: 42, name: 'Lucas', abbr: 'Lc', testament: 'NT', category: 'evangelios' },
  { number: 43, name: 'Juan', abbr: 'Jn', testament: 'NT', category: 'evangelios' },
  { number: 44, name: 'Hechos', abbr: 'Hch', testament: 'NT', category: 'hechos' },
  { number: 45, name: 'Romanos', abbr: 'Ro', testament: 'NT', category: 'paulinas' },
  { number: 46, name: '1 Corintios', abbr: '1Co', testament: 'NT', category: 'paulinas' },
  { number: 47, name: '2 Corintios', abbr: '2Co', testament: 'NT', category: 'paulinas' },
  { number: 48, name: 'Gálatas', abbr: 'Gá', testament: 'NT', category: 'paulinas' },
  { number: 49, name: 'Efesios', abbr: 'Ef', testament: 'NT', category: 'paulinas' },
  { number: 50, name: 'Filipenses', abbr: 'Flp', testament: 'NT', category: 'paulinas' },
  { number: 51, name: 'Colosenses', abbr: 'Col', testament: 'NT', category: 'paulinas' },
  { number: 52, name: '1 Tesalonicenses', abbr: '1Ts', testament: 'NT', category: 'paulinas' },
  { number: 53, name: '2 Tesalonicenses', abbr: '2Ts', testament: 'NT', category: 'paulinas' },
  { number: 54, name: '1 Timoteo', abbr: '1Ti', testament: 'NT', category: 'paulinas' },
  { number: 55, name: '2 Timoteo', abbr: '2Ti', testament: 'NT', category: 'paulinas' },
  { number: 56, name: 'Tito', abbr: 'Tit', testament: 'NT', category: 'paulinas' },
  { number: 57, name: 'Filemón', abbr: 'Flm', testament: 'NT', category: 'paulinas' },
  { number: 58, name: 'Hebreos', abbr: 'Heb', testament: 'NT', category: 'generales' },
  { number: 59, name: 'Santiago', abbr: 'Stg', testament: 'NT', category: 'generales' },
  { number: 60, name: '1 Pedro', abbr: '1P', testament: 'NT', category: 'generales' },
  { number: 61, name: '2 Pedro', abbr: '2P', testament: 'NT', category: 'generales' },
  { number: 62, name: '1 Juan', abbr: '1Jn', testament: 'NT', category: 'generales' },
  { number: 63, name: '2 Juan', abbr: '2Jn', testament: 'NT', category: 'generales' },
  { number: 64, name: '3 Juan', abbr: '3Jn', testament: 'NT', category: 'generales' },
  { number: 65, name: 'Judas', abbr: 'Jud', testament: 'NT', category: 'generales' },
  { number: 66, name: 'Apocalipsis', abbr: 'Ap', testament: 'NT', category: 'apocalipsis' },
];
