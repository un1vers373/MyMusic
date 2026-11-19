// User actions: likes, playlists, upload, edit, delete. Every action talks to the real API.
import { api } from './api.js';
import { store, ingest, findTrack, getList, coverUrl } from './store.js';
import { $, $$, esc, icon, toast, fmtTime, plural } from './util.js';
import {
  openModal, promptText, confirmDialog, openMenu, updateLikeButtons, cover, mosaic, trackCover, markPlaying,
} from './ui.js';
import { player } from './player.js';
import { renderRoute } from './views.js';

const fail = (err) => toast(err.message || 'Something went wrong.', 'error');

export async function refreshLibrary() {
  const [tracks, playlists] = await Promise.all([api.get('/api/tracks'), api.get('/api/playlists')]);
  store.tracks = ingest(tracks);
  store.playlists = playlists;
  renderSidebar();
}

export function renderSidebar() {
  const ul = $('#side-playlists');
  ul.innerHTML = store.playlists.length
    ? store.playlists.map((p) => `<li><a href="#/playlist/${p.id}" data-id="${p.id}">
        <div style="width:44px;flex:none">${mosaic(p.coverTrackIds, p.name)}</div>
        <div class="meta"><div class="t">${esc(p.name)}</div><div class="s">${plural(p.trackCount, 'song', 'songs')}</div></div></a></li>`).join('')
    : '<li class="side-empty">No playlists yet. Use + to create one.</li>';
  const m = location.hash.match(/^#\/playlist\/([^/?]+)/);
  if (m) $$('#side-playlists a').forEach((a) => a.classList.toggle('active', a.dataset.id === m[1]));
}

async function reloadAll() {
  await refreshLibrary();
  await renderRoute();
}

// ---------- Likes ----------
export async function toggleLike(id) {
  const liked = !store.likes.has(id);
  if (liked) store.likes.add(id); else store.likes.delete(id);
  updateLikeButtons(id, liked);
  player.refreshLike();
  try {
    if (liked) await api.put(`/api/tracks/${id}/like`); else await api.del(`/api/tracks/${id}/like`);
    if (location.hash.startsWith('#/liked')) renderRoute();
  } catch (err) {
    if (liked) store.likes.delete(id); else store.likes.add(id);
    updateLikeButtons(id, !liked);
    player.refreshLike();
    fail(err);
  }
}

// ---------- Playback helpers ----------
export function playFromList(listKey, index) {
  const list = getList(listKey);
  const t = list[index];
  if (!t) return;
  if (player.current()?.id === t.id && player.sourceKey() === listKey) player.togglePlay();
  else player.playList(list, index, listKey);
}

export function playAll(listKey, shuffle) {
  const list = getList(listKey);
  if (!list.length) return toast('Nothing to play here yet');
  player.playList(list, 0, listKey, { shuffle: !!shuffle });
}

export async function playPlaylist(id) {
  try {
    const d = await api.get(`/api/playlists/${id}`);
    if (!d.tracks.length) return toast('This playlist is empty');
    ingest(d.tracks);
    player.playList(d.tracks, 0, `playlist:${id}`);
  } catch (err) { fail(err); }
}

// ---------- Playlists ----------
export async function createPlaylist({ thenAdd } = {}) {
  const r = await promptText({ title: 'Create playlist', label: 'Name', confirm: 'Create', description: '' });
  if (!r) return null;
  try {
    const p = await api.post('/api/playlists', { name: r.value, description: r.description || null });
    if (thenAdd) await api.post(`/api/playlists/${p.id}/tracks`, { trackId: thenAdd });
    await refreshLibrary();
    toast(thenAdd ? `Added to “${p.name}”` : `Created “${p.name}”`);
    if (!thenAdd) location.hash = `#/playlist/${p.id}`;
    else renderRoute();
    return p;
  } catch (err) { fail(err); return null; }
}

export async function renamePlaylist(id) {
  const p = store.playlists.find((x) => x.id === id);
  if (!p) return;
  const r = await promptText({ title: 'Edit playlist', label: 'Name', value: p.name, description: p.description ?? '' });
  if (!r) return;
  try {
    await api.put(`/api/playlists/${id}`, { name: r.value, description: r.description || null });
    toast('Playlist updated');
    await reloadAll();
  } catch (err) { fail(err); }
}

export async function deletePlaylist(id) {
  const p = store.playlists.find((x) => x.id === id);
  if (!await confirmDialog({ title: 'Delete playlist', text: `Delete “${p?.name ?? 'this playlist'}”? Your songs stay in the library.` })) return;
  try {
    await api.del(`/api/playlists/${id}`);
    await refreshLibrary();
    toast('Playlist deleted');
    if (location.hash.startsWith('#/playlist/')) location.hash = '#/library/playlists';
    else renderRoute();
  } catch (err) { fail(err); }
}

export async function removeFromPlaylist(playlistId, trackId) {
  try {
    await api.del(`/api/playlists/${playlistId}/tracks/${trackId}`);
    toast('Removed from playlist');
    await reloadAll();
  } catch (err) { fail(err); }
}

/** Modal: choose which playlist a track goes into. */
export function addToPlaylist(trackId) {
  const t = findTrack(trackId);
  const m = openModal({
    title: 'Add to playlist',
    html: `<div class="pick pick-scroll">
      <button class="pick-row pick-new" data-new>${icon('plus')}<span>New playlist</span></button>
      ${store.playlists.map((p) => `<button class="pick-row" data-pl="${p.id}">${mosaic(p.coverTrackIds, p.name)}
        <div class="meta"><div class="t">${esc(p.name)}</div><div class="s">${plural(p.trackCount, 'song', 'songs')}</div></div></button>`).join('')}</div>`,
  });
  m.body.addEventListener('click', async (e) => {
    const row = e.target.closest('.pick-row');
    if (!row) return;
    if (row.dataset.new !== undefined) { m.close(); await createPlaylist({ thenAdd: trackId }); return; }
    try {
      await api.post(`/api/playlists/${row.dataset.pl}/tracks`, { trackId });
      m.close();
      toast(`Added${t ? ` “${t.title}”` : ''}`);
      await refreshLibrary();
      if (location.hash.startsWith('#/playlist/')) renderRoute();
    } catch (err) { fail(err); }
  });
}

/** Modal: add songs from the library to a playlist. */
export async function addSongs(playlistId) {
  let detail;
  try {
    [detail] = await Promise.all([api.get(`/api/playlists/${playlistId}`), refreshLibrary()]);
  } catch (err) { return fail(err); }
  if (!store.tracks.length) return toast('Upload some songs first');

  const inList = new Set(detail.tracks.map((t) => t.id));
  let changed = false;
  const m = openModal({
    title: `Add songs to “${detail.playlist.name}”`, wide: true,
    html: `<label class="field">Filter<input type="search" id="as-q" placeholder="Title, artist or album"></label>
      <div class="pick pick-scroll" id="as-list"></div>
      <div class="modal-f"><button class="btn primary" data-modal-close>Done</button></div>`,
  });
  const list = $('#as-list', m.body);
  const draw = () => {
    const q = $('#as-q', m.body).value.trim().toLowerCase();
    list.innerHTML = store.tracks
      .filter((t) => !q || `${t.title} ${t.artist} ${t.album}`.toLowerCase().includes(q))
      .map((t) => `<button class="pick-row ${inList.has(t.id) ? 'done' : ''}" data-id="${t.id}" ${inList.has(t.id) ? 'disabled' : ''}>
        ${trackCover(t)}<div class="meta"><div class="t">${esc(t.title)}</div><div class="s">${esc(t.artist)}</div></div>
        <span class="tag">${inList.has(t.id) ? 'Added' : 'Add'}</span></button>`).join('')
      || '<p class="text muted">No matching songs.</p>';
  };
  draw();
  $('#as-q', m.body).addEventListener('input', draw);
  list.addEventListener('click', async (e) => {
    const row = e.target.closest('.pick-row');
    if (!row || row.disabled) return;
    try {
      await api.post(`/api/playlists/${playlistId}/tracks`, { trackId: row.dataset.id });
      inList.add(row.dataset.id);
      changed = true;
      row.classList.add('done'); row.disabled = true;
      $('.tag', row).textContent = 'Added';
    } catch (err) { fail(err); }
  });
  await m.closed;
  if (changed) await reloadAll();
}

// ---------- Track editing / deletion ----------
export function editTrack(id) {
  const t = findTrack(id);
  if (!t) return;
  const m = openModal({
    title: 'Edit details',
    html: `<form class="m-form" style="display:contents">
      <label class="field">Title<input type="text" name="title" maxlength="300" required value="${esc(t.title)}"></label>
      <label class="field">Artist<input type="text" name="artist" maxlength="200" value="${esc(t.artist)}"></label>
      <label class="field">Album<input type="text" name="album" maxlength="200" value="${esc(t.album)}"></label>
      <div class="edit-cover">${trackCover(t)}<div style="flex:1;display:flex;flex-direction:column;gap:6px">
        <label class="field">Cover image<input type="file" name="cover" accept="image/jpeg,image/png,image/webp"></label>
        ${t.hasCover ? '<label class="check"><input type="checkbox" name="rmcover"> Remove current cover</label>' : ''}</div></div>
      <p class="form-error" role="alert"></p>
      <div class="modal-f"><button type="button" class="btn" data-modal-close>Cancel</button><button class="btn primary" type="submit">Save</button></div></form>`,
  });
  const form = $('.m-form', m.body);
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const err = $('.form-error', form);
    err.textContent = '';
    const els = form.elements;
    const file = els.cover.files[0];
    if (file && file.size > store.config.maxCoverMb * 1024 * 1024) { err.textContent = `Cover must be under ${store.config.maxCoverMb} MB.`; return; }
    try {
      await api.put(`/api/tracks/${id}`, { title: els.title.value, artist: els.artist.value, album: els.album.value });
      if (file) {
        const fd = new FormData();
        fd.append('cover', file);
        await api.put(`/api/tracks/${id}/cover`, fd);
        store.coverVer[id] = Date.now();
      } else if (els.rmcover?.checked) {
        await api.del(`/api/tracks/${id}/cover`);
        store.coverVer[id] = Date.now();
      }
      m.close(true);
      toast('Saved');
      await reloadAll();
    } catch (ex) { err.textContent = ex.message; }
  });
}

export async function deleteTrack(id) {
  const t = findTrack(id);
  if (!await confirmDialog({ title: 'Delete song', text: `Delete “${t?.title ?? 'this song'}” from your library? This also removes it from playlists and cannot be undone.` })) return;
  try {
    await api.del(`/api/tracks/${id}`);
    player.removeTrack(id);
    toast('Song deleted');
    await reloadAll();
  } catch (err) { fail(err); }
}

export function trackMenu(anchor, id, playlistId) {
  const liked = store.likes.has(id);
  const items = [
    { label: 'Add to playlist', icon: 'plus', action: () => addToPlaylist(id) },
    { label: liked ? 'Remove from Liked Songs' : 'Save to Liked Songs', icon: 'heart', action: () => toggleLike(id) },
    { label: 'Edit details', icon: 'edit', action: () => editTrack(id) },
  ];
  if (playlistId) items.push({ label: 'Remove from this playlist', icon: 'x', action: () => removeFromPlaylist(playlistId, id) });
  items.push({ label: 'Delete from library', icon: 'trash', danger: true, action: () => deleteTrack(id) });
  openMenu(anchor, items);
}

// ---------- Upload ----------
export function uploadModal() {
  const maxMb = store.config.maxAudioMb;
  const m = openModal({
    title: 'Upload music', wide: true,
    html: `<div class="drop" id="drop">${icon('upload', 'lg')}<strong>Drop MP3 files here</strong>
        <span>or <button type="button" class="link" id="pick">browse your computer</button> (up to ${maxMb} MB each)</span>
        <input id="file-input" type="file" accept=".mp3,audio/mpeg" multiple hidden></div>
      <label class="field">Cover image for these songs (optional)<input id="cover-input" type="file" accept="image/jpeg,image/png,image/webp"></label>
      <p class="text muted">Title, artist, album and the embedded cover are read from the file’s tags. You can change them later with “Edit details”.</p>
      <ul class="uplist" id="uplist"></ul>
      <div class="modal-f"><button class="btn primary" data-modal-close>Done</button></div>`,
  });
  const drop = $('#drop', m.body);
  const input = $('#file-input', m.body);
  const coverInput = $('#cover-input', m.body);
  const listEl = $('#uplist', m.body);
  let uploaded = 0;
  let chain = Promise.resolve();

  const add = (files) => {
    const cover = coverInput.files[0] ?? null;
    for (const file of files) {
      const li = document.createElement('li');
      li.className = 'up';
      li.innerHTML = `<span class="name">${esc(file.name)}</span><span class="st">Waiting…</span><div class="bar"><i></i></div>`;
      listEl.appendChild(li);
      const st = $('.st', li);
      const bar = $('.bar i', li);
      const fail_ = (msg) => { li.classList.add('fail'); st.textContent = msg; };

      if (!/\.mp3$/i.test(file.name)) { fail_('Only MP3 files'); continue; }
      if (file.size > maxMb * 1024 * 1024) { fail_(`Over ${maxMb} MB`); continue; }
      if (cover && cover.size > store.config.maxCoverMb * 1024 * 1024) { fail_('Cover is too large'); continue; }

      chain = chain.then(async () => {
        st.textContent = 'Uploading…';
        const fd = new FormData();
        fd.append('file', file);
        if (cover) fd.append('cover', cover);
        try {
          await api.upload('/api/tracks', fd, (p) => { bar.style.width = `${Math.round(p * 100)}%`; });
          bar.style.width = '100%';
          li.classList.add('ok');
          st.textContent = 'Added';
          uploaded++;
        } catch (err) { fail_(err.message); }
      });
    }
  };

  $('#pick', m.body).addEventListener('click', () => input.click());
  input.addEventListener('change', () => { add([...input.files]); input.value = ''; });
  for (const ev of ['dragenter', 'dragover']) drop.addEventListener(ev, (e) => { e.preventDefault(); drop.classList.add('over'); });
  for (const ev of ['dragleave', 'drop']) drop.addEventListener(ev, (e) => { e.preventDefault(); drop.classList.remove('over'); });
  drop.addEventListener('drop', (e) => add([...e.dataTransfer.files]));

  m.closed.then(async () => {
    await chain;
    if (uploaded) { toast(`${plural(uploaded, 'song', 'songs')} added`); await reloadAll(); }
  });
}
