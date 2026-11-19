// Shared client state.
export const store = {
  user: null,
  config: { registrationOpen: true, maxAudioMb: 60, maxCoverMb: 5 },
  tracks: [],          // whole library, newest first
  playlists: [],
  likes: new Set(),    // ids of liked tracks (server is the source of truth)
  coverVer: {},        // cache-busting counters for covers that changed
  lists: new Map(),    // named track lists that rendered rows/cards refer to by key
};

export function resetStore() {
  store.user = null;
  store.tracks = [];
  store.playlists = [];
  store.likes = new Set();
  store.coverVer = {};
  store.lists = new Map();
}

/** Syncs the like set with tracks received from the server. */
export function ingest(tracks) {
  for (const t of tracks) {
    if (t.isLiked) store.likes.add(t.id);
    else store.likes.delete(t.id);
  }
  return tracks;
}

export const isLiked = (id) => store.likes.has(id);
export const setList = (key, tracks) => { store.lists.set(key, tracks); return tracks; };
export const getList = (key) => store.lists.get(key) ?? [];
export const coverUrl = (id) => `/api/tracks/${id}/cover?v=${store.coverVer[id] || 0}`;

export function findTrack(id) {
  const fromLib = store.tracks.find((t) => t.id === id);
  if (fromLib) return fromLib;
  for (const list of store.lists.values()) {
    const t = list.find((x) => x.id === id);
    if (t) return t;
  }
  return null;
}

export function groupAlbums(tracks) {
  const map = new Map();
  for (const t of tracks) {
    if (!t.album) continue;
    const key = `${t.artist}\u0000${t.album}`;
    if (!map.has(key)) map.set(key, { name: t.album, artist: t.artist, tracks: [] });
    map.get(key).tracks.push(t);
  }
  return [...map.values()]
    .map((a) => ({ name: a.name, artist: a.artist, tracks: a.tracks, count: a.tracks.length, coverId: a.tracks.find((t) => t.hasCover)?.id ?? null }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function groupArtists(tracks) {
  const map = new Map();
  for (const t of tracks) {
    if (!map.has(t.artist)) map.set(t.artist, []);
    map.get(t.artist).push(t);
  }
  return [...map.entries()]
    .map(([name, list]) => ({ name, tracks: list, count: list.length, coverId: list.find((t) => t.hasCover)?.id ?? null }))
    .sort((a, b) => a.name.localeCompare(b.name));
}
