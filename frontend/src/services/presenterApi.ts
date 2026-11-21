import type { LiveState, ObsTheme, ProjectorTheme, ThemeSettings } from '../types/live';

export interface SongItem {
  id: string;
  title: string;
  author?: string | null;
  musicalKey?: string | null;
  slides: { index: number; label: string; text: string }[];
}

export interface BibleVersionItem {
  id: string;
  name: string;
  abbreviation: string;
}

export interface BibleVerseItem {
  versionId: string;
  bookNumber: number;
  bookName: string;
  chapter: number;
  verse: number;
  text: string;
}

export interface AnnouncementItem {
  id: string;
  title: string;
  rawBody: string;
  slides: { index: number; text: string }[];
}

export interface PlaylistItemDto {
  id: string;
  position: number;
  kind: number;
  displayTitle: string;
  subtitle?: string | null;
}

export interface PlaylistDto {
  id: string;
  name: string;
  items: PlaylistItemDto[];
}

export interface MediaAssetDto {
  id: string;
  originalName: string;
  kind: 'Image' | 'Video';
  contentType: string;
}

export interface BackupRestoreSummary {
  importedSongs: number;
  skippedSongs: number;
  importedAnnouncements: number;
  skippedAnnouncements: number;
}

const OPERATOR_HEADERS = {
  'X-CustomPresenter-Client': 'operator',
};

async function requestJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    let message = `Error HTTP ${response.status}`;
    try {
      const data = (await response.json()) as { error?: string };
      if (data.error) {
        message = data.error;
      }
    } catch {
      // Mantiene mensaje HTTP genérico si el cuerpo no es JSON.
    }
    throw new Error(message);
  }

  return (await response.json()) as T;
}

function jsonMutate<T>(url: string, method: 'POST' | 'PUT' | 'DELETE', body?: unknown): Promise<T> {
  return requestJson<T>(url, {
    method,
    headers: {
      ...OPERATOR_HEADERS,
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
}

export const presenterApi = {
  listSongs: (q = '') =>
    requestJson<SongItem[]>(`/api/songs${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  createSong: (title: string, rawLyrics: string, author?: string, musicalKey?: string) =>
    jsonMutate<SongItem>('/api/songs', 'POST', { title, rawLyrics, author, musicalKey }),
  projectSong: (id: string, slideIndex: number) =>
    jsonMutate<LiveState>(`/api/songs/${id}/project/${slideIndex}`, 'POST'),

  listBibleVersions: () => requestJson<BibleVersionItem[]>('/api/bibles/versions'),
  searchBible: (versionId: string, q: string) =>
    requestJson<BibleVerseItem[]>(
      `/api/bibles/search?versionId=${encodeURIComponent(versionId)}&q=${encodeURIComponent(q)}`
    ),
  importBibleXml: (name: string, abbreviation: string, file: File) => {
    const form = new FormData();
    form.append('name', name);
    form.append('abbreviation', abbreviation);
    form.append('file', file);
    return requestJson<BibleVersionItem>('/api/bibles/import', {
      method: 'POST',
      headers: OPERATOR_HEADERS,
      body: form,
    });
  },
  projectBibleVerse: (verse: BibleVerseItem, versionAbbreviation: string) =>
    jsonMutate<LiveState>('/api/bibles/project', 'POST', {
      ...verse,
      versionAbbreviation,
    }),

  listAnnouncements: () => requestJson<AnnouncementItem[]>('/api/announcements'),
  createAnnouncement: (title: string, rawBody: string) =>
    jsonMutate<AnnouncementItem>('/api/announcements', 'POST', { title, rawBody }),
  projectAnnouncement: (id: string, slideIndex: number) =>
    jsonMutate<LiveState>(`/api/announcements/${id}/project/${slideIndex}`, 'POST'),

  listPlaylists: () => requestJson<PlaylistDto[]>('/api/playlists'),
  createPlaylist: (name: string) => jsonMutate<PlaylistDto>('/api/playlists', 'POST', { name }),
  addSongToPlaylist: (playlistId: string, songId: string) =>
    jsonMutate<PlaylistDto>(`/api/playlists/${playlistId}/items`, 'POST', { kind: 'Song', songId }),
  projectPlaylistItem: (playlistId: string, itemIndex: number) =>
    jsonMutate<LiveState>(`/api/playlists/${playlistId}/project/${itemIndex}`, 'POST'),

  getThemes: () => requestJson<ThemeSettings>('/api/themes'),
  updateProjectorTheme: (theme: ProjectorTheme) =>
    jsonMutate<LiveState>('/api/themes/projector', 'PUT', theme),
  updateObsTheme: (theme: ObsTheme) => jsonMutate<LiveState>('/api/themes/obs', 'PUT', theme),

  listMedia: () => requestJson<MediaAssetDto[]>('/api/media'),
  uploadMedia: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return requestJson<MediaAssetDto>('/api/media', {
      method: 'POST',
      headers: OPERATOR_HEADERS,
      body: form,
    });
  },
  assignMedia: (id: string) => jsonMutate<LiveState>(`/api/media/${id}/assign`, 'POST'),
  clearMedia: () => jsonMutate<LiveState>('/api/media/clear', 'POST'),

  exportBackupUrl: () => '/api/backup/export',
  importBackupJson: (jsonText: string) =>
    requestJson<BackupRestoreSummary>('/api/backup/import', {
      method: 'POST',
      headers: {
        ...OPERATOR_HEADERS,
        'Content-Type': 'application/json',
      },
      body: jsonText,
    }),
};
