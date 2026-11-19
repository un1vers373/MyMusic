import { api } from './api.js';
import { store, ingest, setList, groupAlbums, groupArtists } from './store.js';
import { $, $$, esc, icon, fmtTotal, plural, debounce } from './util.js';
import {
  trackCard, playlistCard, albumCard, artistCard, trackTable, section, empty, hero, mosaic, cover,
  trackCover, markPlaying,
} from './ui.js';

const view = () => $('#view');
let token = 0;

const grid = (html) => `<div class="grid">${html}</div>`;
const sumSeconds = (tracks) => tracks.reduce((s, t) => s + t.duration, 0);
const playActions = (key, extra = '') => `<div class="actions">
  <button class="play-btn" data-action="play-all" data-list="${key}" aria-label="Play">${icon('play')}</button>
  <button class="icon-btn" data-action="play-all" data-list="${key}" data-shuffle="1" aria-label="Shuffle play" title="Shuffle play">${icon('shuffle')}</button>${extra}</div>`;

// ---------- Router ----------
export async function renderRoute() {
  const my = ++token;
  const [path, query = ''] = (location.hash.slice(1) || '/').split('?');
  const parts = path.split('/').filter(Boolean).map(decodeURIComponent);
  const name = parts[0] ?? 'home';

  $$('[data-nav]').forEach((a) => a.classList.toggle('active', a.dataset.nav === (name === 'home' ? 'home' : name)));
  $$('#side-playlists a').forEach((a) => a.classList.toggle('active', name === 'playlist' && a.dataset.id === parts[1]));

  view().innerHTML = '<div class="spinner" role="status" aria-label="Loading"></div>';
  let result;
  try {
    result = await build(name, parts, new URLSearchParams(query));
  } catch (err) {
    result = { html: empty({ title: 'Could not load this page', text: err.message, iconName: 'x' }) };
  }
  if (my !== token) return; // user navigated away meanwhile
  view().innerHTML = result.html;
  result.mount?.();
  markPlaying();
  $('#main').scrollTop = 0;
}

function build(name, parts, query) {
  switch (name) {
    case 'home': return homeView();
    case 'search': return searchView(query.get('q') ?? '');
    case 'library': return libraryView(parts[1] ?? 'songs');
    case 'liked': return likedView();
    case 'playlist': return playlistView(parts[1]);
    case 'album': return albumView(parts[1], parts[2]);
    case 'artist': return artistView(parts[1]);
    default: return { html: empty({ title: 'Page not found', text: 'That page does not exist.', iconName: 'x' }) };
  }
}

// ---------- Home ----------
async function homeView() {
  const h = await api.get('/api/home');
  [h.recentlyPlayed, h.recentlyAdded, h.recommended].forEach(ingest);
  store.playlists = h.playlists;
  setList('recent', h.recentlyPlayed); setList('added', h.recentlyAdded); setList('rec', h.recommended);

  const hr = new Date().getHours();
  const greet = hr < 5 ? 'Still up' : hr < 12 ? 'Good morning' : hr < 18 ? 'Good afternoon' : 'Good evening';
  const title = `<h1 class="page-title">${greet}, ${esc(store.user.username)}</h1>`;

  if (h.trackCount === 0) {
    return { html: title + empty({ title: 'Your library is empty', text: 'Upload MP3 files and they will appear here, ready to play.', action: 'upload', label: 'Upload music', iconName: 'upload' }) };
  }

  const cards = (key, list) => grid(list.map((t, i) => trackCard(t, i, key)).join(''));
  return {
    html: title
      + section('Recently Played', h.recentlyPlayed.length ? cards('recent', h.recentlyPlayed) : '<p class="pad-msg">Songs you play will show up here.</p>')
      + section('Your Playlists', h.playlists.length
        ? grid(h.playlists.slice(0, 12).map(playlistCard).join(''))
        : `<p class="pad-msg">Group your songs into playlists.</p><button class="btn" data-action="new-playlist">${icon('plus')}<span>Create playlist</span></button>`,
      h.playlists.length > 12 ? { href: '#/library/playlists' } : {})
      + section('Recently Added', cards('added', h.recentlyAdded), { href: '#/library' })
      + section('Recommended Music', cards('rec', h.recommended)),
  };
}

// ---------- Search ----------
function searchView(initial) {
  const html = `<div class="searchbar">${icon('search')}<input id="q" type="search" placeholder="Search songs, artists, albums, playlists" autocomplete="off" value="${esc(initial)}" aria-label="Search"></div><div id="search-results"></div>`;
  return {
    html,
    mount() {
      const input = $('#q');
      const out = $('#search-results');
      let seq = 0;

      const idle = () => {
        const artists = groupArtists(store.tracks);
        out.innerHTML = artists.length
          ? section('Browse your artists', grid(artists.slice(0, 18).map(artistCard).join('')))
          : empty({ title: 'Nothing to search yet', text: 'Upload some music first.', action: 'upload', label: 'Upload music', iconName: 'upload' });
      };

      const run = async () => {
        const q = input.value.trim();
        history.replaceState(null, '', q ? `#/search?q=${encodeURIComponent(q)}` : '#/search');
        if (!q) { seq++; return idle(); }
        const mine = ++seq;
        try {
          const r = await api.get(`/api/search?q=${encodeURIComponent(q)}`);
          if (mine !== seq) return;
          ingest(r.tracks);
          setList('search', r.tracks);
          out.innerHTML = renderResults(r, q);
          markPlaying();
        } catch (err) {
          if (mine === seq) out.innerHTML = empty({ title: 'Search failed', text: err.message, iconName: 'x' });
        }
      };

      input.addEventListener('input', debounce(run, 180));
      run();
      if (!initial) input.focus();
    },
  };
}

function renderResults(r, q) {
  if (!r.tracks.length && !r.artists.length && !r.albums.length && !r.playlists.length)
    return empty({ title: `No results for “${q}”`, text: 'Check the spelling or try a different word.', iconName: 'search' });
  return (r.tracks.length ? section('Songs', trackTable(r.tracks, 'search')) : '')
    + (r.artists.length ? section('Artists', grid(r.artists.map((a) => artistCard({ name: a.name, count: a.trackCount, coverId: a.coverTrackId })).join(''))) : '')
    + (r.albums.length ? section('Albums', grid(r.albums.map((a) => albumCard({ name: a.name, artist: a.artist, coverId: a.coverTrackId })).join(''))) : '')
    + (r.playlists.length ? section('Playlists', grid(r.playlists.map(playlistCard).join(''))) : '');
}

// ---------- Library ----------
async function libraryView(tab) {
  const [tracks, playlists] = await Promise.all([api.get('/api/tracks'), api.get('/api/playlists')]);
  store.tracks = ingest(tracks);
  store.playlists = playlists;

  const tabs = [['songs', 'Songs'], ['albums', 'Albums'], ['artists', 'Artists'], ['playlists', 'Playlists']];
  if (!tabs.some(([k]) => k === tab)) tab = 'songs';
  const nav = `<h1 class="page-title">Your Library</h1><nav class="tabs" aria-label="Library sections">${tabs.map(([k, l]) => `<a href="#/library/${k}" class="${k === tab ? 'on' : ''}">${l}</a>`).join('')}</nav>`;

  if (tab === 'playlists') {
    return {
      html: nav + (playlists.length
        ? `<div class="actions"><button class="btn" data-action="new-playlist">${icon('plus')}<span>New playlist</span></button></div>` + grid(playlists.map(playlistCard).join(''))
        : empty({ title: 'No playlists yet', text: 'Create a playlist and add songs from your library.', action: 'new-playlist', label: 'Create playlist', iconName: 'library' })),
    };
  }
  if (!tracks.length)
    return { html: nav + empty({ title: 'Your library is empty', text: 'Upload MP3 files to build your collection.', action: 'upload', label: 'Upload music', iconName: 'upload' }) };

  if (tab === 'albums') {
    const albums = groupAlbums(tracks);
    return { html: nav + (albums.length ? grid(albums.map(albumCard).join('')) : empty({ title: 'No albums', text: 'Songs with an album name in their tags are grouped here. You can set one with “Edit details”.', iconName: 'music' })) };
  }
  if (tab === 'artists') return { html: nav + grid(groupArtists(tracks).map(artistCard).join('')) };

  const draw = (filter, sort) => {
    let list = tracks.filter((t) => !filter || `${t.title} ${t.artist} ${t.album}`.toLowerCase().includes(filter.toLowerCase()));
    if (sort === 'title') list = [...list].sort((a, b) => a.title.localeCompare(b.title));
    if (sort === 'artist') list = [...list].sort((a, b) => a.artist.localeCompare(b.artist) || a.title.localeCompare(b.title));
    setList('lib', list);
    return list.length ? trackTable(list, 'lib') : empty({ title: 'No matches', text: 'No songs match this filter.', iconName: 'search' });
  };

  return {
    html: `${nav}<div class="toolbar"><div class="searchbar">${icon('search')}<input id="lib-filter" type="search" placeholder="Filter songs" aria-label="Filter songs"></div>
      <select class="sel" id="lib-sort" aria-label="Sort songs"><option value="new">Recently added</option><option value="title">Title</option><option value="artist">Artist</option></select>
      <button class="btn primary" data-action="play-all" data-list="lib">${icon('play')}<span>Play all</span></button></div>
      <div id="lib-list">${draw('', 'new')}</div>`,
    mount() {
      const apply = () => { $('#lib-list').innerHTML = draw($('#lib-filter').value.trim(), $('#lib-sort').value); markPlaying(); };
      $('#lib-filter').addEventListener('input', debounce(apply, 120));
      $('#lib-sort').addEventListener('change', apply);
    },
  };
}

// ---------- Liked ----------
async function likedView() {
  const tracks = ingest(await api.get('/api/likes'));
  setList('liked', tracks);
  const heroHtml = hero({
    kind: 'Playlist', title: 'Liked Songs', seed: 'liked-songs',
    meta: `${plural(tracks.length, 'song', 'songs')}${tracks.length ? `, ${fmtTotal(sumSeconds(tracks))}` : ''}`,
    coverHtml: `<div class="cover ph" style="--h:335">${icon('heart')}</div>`,
  });
  return {
    html: heroHtml + (tracks.length
      ? playActions('liked') + trackTable(tracks, 'liked')
      : empty({ title: 'Songs you like will appear here', text: 'Tap the heart next to any song to save it.', iconName: 'heart' })),
  };
}

// ---------- Playlist ----------
async function playlistView(id) {
  const d = await api.get(`/api/playlists/${encodeURIComponent(id)}`);
  const p = d.playlist;
  ingest(d.tracks);
  setList('pl', d.tracks);
  const extra = `<button class="btn" data-action="add-songs" data-id="${p.id}">${icon('plus')}<span>Add songs</span></button>
    <button class="icon-btn" data-action="rename-playlist" data-id="${p.id}" aria-label="Rename playlist" title="Rename">${icon('edit')}</button>
    <button class="icon-btn" data-action="delete-playlist" data-id="${p.id}" aria-label="Delete playlist" title="Delete">${icon('trash')}</button>`;
  const meta = `${plural(p.trackCount, 'song', 'songs')}${p.trackCount ? `, ${fmtTotal(p.totalSeconds)}` : ''}`;
  return {
    html: hero({ kind: 'Playlist', title: p.name, desc: p.description, meta, coverHtml: mosaic(p.coverTrackIds, p.name), seed: p.name })
      + (d.tracks.length
        ? playActions('pl', extra) + trackTable(d.tracks, 'pl', { playlistId: p.id })
        : `<div class="actions">${extra}</div>` + empty({ title: 'This playlist is empty', text: 'Add songs from your library.', action: 'add-songs', label: 'Add songs', iconName: 'music' }).replace('data-action="add-songs"', `data-action="add-songs" data-id="${p.id}"`)),
  };
}

// ---------- Album / Artist ----------

async function albumView(artist, albumName) {
  store.tracks = ingest(await api.get('/api/tracks'));
  const list = store.tracks.filter((t) => t.artist === artist && t.album === albumName);
  if (!list.length) return { html: empty({ title: 'Album not found', text: 'It may have been deleted or renamed.', iconName: 'x' }) };
  setList('album', list);
  return {
    html: hero({
      kind: 'Album', title: albumName, seed: albumName + artist,
      meta: `${artist}, ${plural(list.length, 'song', 'songs')}, ${fmtTotal(sumSeconds(list))}`,
      coverHtml: cover(list.find((t) => t.hasCover)?.id ?? null, albumName + artist),
    }) + playActions('album') + trackTable(list, 'album'),
  };
}

async function artistView(name) {
  store.tracks = ingest(await api.get('/api/tracks'));
  const list = store.tracks.filter((t) => t.artist === name);
  if (!list.length) return { html: empty({ title: 'Artist not found', text: 'No songs by this artist in your library.', iconName: 'x' }) };
  setList('artist', list);
  const albums = groupAlbums(list);
  return {
    html: hero({
      kind: 'Artist', title: name, seed: name, meta: plural(list.length, 'song', 'songs'),
      coverHtml: cover(list.find((t) => t.hasCover)?.id ?? null, name),
    }) + playActions('artist') + trackTable(list, 'artist')
      + (albums.length ? `<div style="height:32px"></div>` + section('Albums', grid(albums.map(albumCard).join(''))) : ''),
  };
}
