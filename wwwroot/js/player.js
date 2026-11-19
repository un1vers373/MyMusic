import { api } from './api.js';
import { store, coverUrl, isLiked } from './store.js';
import { $, fmtTime, toast, icon, hue, esc } from './util.js';

const audio = $('#audio');
const el = {
  cover: $('#np-cover'), title: $('#np-title'), artist: $('#np-artist'), like: $('#np-like'),
  play: $('#btn-play'), prev: $('#btn-prev'), next: $('#btn-next'), shuffle: $('#btn-shuffle'), repeat: $('#btn-repeat'),
  seek: $('#seek'), cur: $('#t-cur'), dur: $('#t-dur'), vol: $('#vol'), mute: $('#btn-mute'),
};

// queue = tracks in play order source; order = indices into queue (shuffled when shuffle is on)
const S = { queue: [], order: [], pos: -1, shuffle: false, repeat: 'off', sourceKey: null, reported: false, errors: 0 };
let pendingSeek = 0;
let dragging = false;
let lastPersist = 0;
let storageKey = null;
let premuteVolume = 80;

const emit = () => document.dispatchEvent(new CustomEvent('player:state'));
const current = () => S.queue[S.order[S.pos]] ?? null;

export const player = {
  current,
  isPlaying: () => !audio.paused && !audio.ended && !!current(),
  sourceKey: () => S.sourceKey,
  playList, togglePlay, next, prev, removeTrack, stop, restore, refreshLike, init,
};

function shuffleInPlace(a) {
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function buildOrder(startIdx) {
  const idx = [...S.queue.keys()];
  if (S.shuffle) {
    S.order = [startIdx, ...shuffleInPlace(idx.filter((i) => i !== startIdx))];
    S.pos = 0;
  } else {
    S.order = idx;
    S.pos = startIdx;
  }
}

function playList(tracks, index = 0, key = null, opts = {}) {
  if (!tracks.length) return;
  S.queue = tracks.slice();
  S.sourceKey = key;
  if (opts.shuffle !== undefined) S.shuffle = opts.shuffle;
  if (opts.shuffle) index = Math.floor(Math.random() * tracks.length);
  buildOrder(index);
  S.errors = 0;
  load(true);
}

function load(autoplay, startAt = 0) {
  const t = current();
  if (!t) return;
  S.reported = false;
  pendingSeek = startAt;
  audio.src = `/api/tracks/${t.id}/stream`;
  if (autoplay) audio.play().catch(onPlayError);
  renderNowPlaying();
  updateMediaSession(t);
  persist(true);
  emit();
}

function onPlayError(err) {
  if (err?.name === 'AbortError') return;
  if (err?.name === 'NotAllowedError') toast('Press play to start');
}

function togglePlay() {
  if (!current()) {
    if (store.tracks.length) playList(store.tracks, 0, 'library');
    else toast('Upload a song first');
    return;
  }
  if (audio.paused) audio.play().catch(onPlayError);
  else audio.pause();
}

function next(auto = false) {
  if (!S.queue.length) return;
  if (auto && S.repeat === 'one') {
    audio.currentTime = 0;
    audio.play().catch(onPlayError);
    return;
  }
  if (S.pos < S.order.length - 1) {
    S.pos++;
    load(true);
  } else if (!auto || S.repeat === 'all') {
    const first = S.shuffle ? Math.floor(Math.random() * S.queue.length) : 0;
    buildOrder(first);
    load(true);
  } else {
    audio.pause();
    audio.currentTime = 0;
    renderControls();
    emit();
  }
}

function prev() {
  if (!S.queue.length) return;
  if (audio.currentTime > 3 || (S.pos === 0 && S.repeat !== 'all')) {
    audio.currentTime = 0;
    return;
  }
  S.pos = S.pos > 0 ? S.pos - 1 : S.order.length - 1;
  load(true);
}

function stop() {
  audio.pause();
  audio.removeAttribute('src');
  audio.load();
  S.queue = []; S.order = []; S.pos = -1; S.sourceKey = null;
  renderNowPlaying();
  renderProgress(0, 0);
  emit();
  if (storageKey) localStorage.removeItem(storageKey);
}

/** Called when a track is deleted from the library. */
function removeTrack(id) {
  const cur = current();
  if (!cur) return;
  if (cur.id === id) return stop();
  S.queue = S.queue.filter((t) => t.id !== id);
  buildOrder(S.queue.findIndex((t) => t.id === cur.id));
  persist(true);
}

// ---------- Rendering ----------
function coverMarkup(t) {
  return t.hasCover
    ? `<div class="cover"><img src="${coverUrl(t.id)}" alt=""></div>`
    : `<div class="cover ph" style="--h:${hue(t.title + t.artist)}">${icon('music')}</div>`;
}

function renderNowPlaying() {
  const t = current();
  el.title.textContent = t ? t.title : 'Nothing playing';
  el.artist.textContent = t ? t.artist : 'Pick a song to start';
  el.cover.innerHTML = t ? coverMarkup(t) : '';
  el.like.disabled = !t;
  refreshLike();
  renderControls();
}

function refreshLike() {
  const t = current();
  const liked = !!t && isLiked(t.id);
  el.like.classList.toggle('liked', liked);
  el.like.dataset.like = t ? t.id : '';
  el.like.dataset.id = t ? t.id : '';
  el.like.setAttribute('aria-label', liked ? 'Remove from Liked Songs' : 'Save to Liked Songs');
}

function renderControls() {
  const playing = !audio.paused && !!current();
  el.play.innerHTML = icon(playing ? 'pause' : 'play');
  el.play.setAttribute('aria-label', playing ? 'Pause' : 'Play');
  el.shuffle.classList.toggle('on', S.shuffle);
  el.repeat.classList.toggle('on', S.repeat !== 'off');
  el.repeat.innerHTML = icon(S.repeat === 'one' ? 'repeat1' : 'repeat');
  el.repeat.setAttribute('aria-label', `Repeat: ${S.repeat}`);
}

function renderProgress(cur, dur) {
  el.cur.textContent = fmtTime(cur);
  el.dur.textContent = fmtTime(dur);
  if (!dragging) {
    const v = dur > 0 ? Math.min(1000, (cur / dur) * 1000) : 0;
    el.seek.value = v;
    el.seek.style.setProperty('--p', `${v / 10}%`);
  }
}

function duration() {
  return Number.isFinite(audio.duration) && audio.duration > 0 ? audio.duration : (current()?.duration ?? 0);
}

function updateMediaSession(t) {
  if (!('mediaSession' in navigator)) return;
  navigator.mediaSession.metadata = new MediaMetadata({
    title: t.title, artist: t.artist, album: t.album || '',
    artwork: t.hasCover ? [{ src: coverUrl(t.id), sizes: '512x512' }] : [],
  });
}

// ---------- Persistence (queue and position survive navigation and reloads) ----------
function persist(force = false) {
  if (!storageKey) return;
  const now = Date.now();
  if (!force && now - lastPersist < 4000) return;
  lastPersist = now;
  try {
    localStorage.setItem(storageKey, JSON.stringify({
      queue: S.queue.slice(0, 500), order: S.order, pos: S.pos,
      shuffle: S.shuffle, repeat: S.repeat, key: S.sourceKey, time: audio.currentTime || 0,
    }));
  } catch { /* storage unavailable or full: playback state just isn't remembered */ }
}

function restore(userId, validIds) {
  storageKey = `mymusic.player.${userId}`;
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) || 'null');
    if (!saved?.queue?.length) return renderNowPlaying();
    const cur = saved.queue[saved.order?.[saved.pos]];
    if (!cur || !validIds.has(cur.id)) { localStorage.removeItem(storageKey); return renderNowPlaying(); }
    S.queue = saved.queue.filter((t) => validIds.has(t.id));
    S.shuffle = !!saved.shuffle;
    S.repeat = saved.repeat || 'off';
    S.sourceKey = saved.key ?? null;
    buildOrder(S.queue.findIndex((t) => t.id === cur.id));
    load(false, saved.time || 0);
  } catch {
    renderNowPlaying();
  }
}

// ---------- Wiring ----------
function init() {
  const vol = Number(localStorage.getItem('mymusic.volume') ?? 80);
  el.vol.value = vol;
  setVolume(vol);

  el.play.addEventListener('click', togglePlay);
  el.next.addEventListener('click', () => next(false));
  el.prev.addEventListener('click', prev);
  el.shuffle.addEventListener('click', () => {
    S.shuffle = !S.shuffle;
    const cur = S.order[S.pos];
    if (S.queue.length) buildOrder(cur);
    renderControls(); persist(true);
  });
  el.repeat.addEventListener('click', () => {
    S.repeat = S.repeat === 'off' ? 'all' : S.repeat === 'all' ? 'one' : 'off';
    renderControls(); persist(true);
  });

  el.seek.addEventListener('input', () => {
    dragging = true;
    el.seek.style.setProperty('--p', `${el.seek.value / 10}%`);
    el.cur.textContent = fmtTime((el.seek.value / 1000) * duration());
  });
  el.seek.addEventListener('change', () => {
    if (current()) audio.currentTime = (el.seek.value / 1000) * duration();
    dragging = false;
  });

  el.vol.addEventListener('input', () => setVolume(Number(el.vol.value)));
  el.mute.addEventListener('click', () => {
    if (audio.volume > 0) { premuteVolume = Number(el.vol.value) || 80; el.vol.value = 0; }
    else el.vol.value = premuteVolume;
    setVolume(Number(el.vol.value));
  });

  audio.addEventListener('play', () => { renderControls(); emit(); });
  audio.addEventListener('pause', () => { renderControls(); emit(); persist(true); });
  audio.addEventListener('playing', () => { S.errors = 0; });
  audio.addEventListener('ended', () => next(true));
  audio.addEventListener('loadedmetadata', () => {
    if (pendingSeek > 0 && pendingSeek < audio.duration) audio.currentTime = pendingSeek;
    pendingSeek = 0;
    renderProgress(audio.currentTime, duration());
  });
  audio.addEventListener('timeupdate', () => {
    renderProgress(audio.currentTime, duration());
    const t = current();
    if (t && !S.reported && audio.currentTime >= Math.min(5, (t.duration || 10) / 2)) {
      S.reported = true;
      api.post(`/api/tracks/${t.id}/played`).catch(() => {});
    }
    persist();
  });
  audio.addEventListener('error', () => {
    if (!current() || !audio.getAttribute('src')) return;
    S.errors++;
    toast(`Can't play “${current().title}”`, 'error');
    if (S.errors < S.queue.length && S.queue.length > 1) next(true);
  });

  document.addEventListener('keydown', (e) => {
    if (e.code !== 'Space' || e.target.closest('input, textarea, select, button, [contenteditable]')) return;
    if (document.querySelector('.backdrop')) return;
    e.preventDefault();
    togglePlay();
  });

  if ('mediaSession' in navigator) {
    const ms = navigator.mediaSession;
    ms.setActionHandler('play', () => audio.play().catch(onPlayError));
    ms.setActionHandler('pause', () => audio.pause());
    ms.setActionHandler('previoustrack', prev);
    ms.setActionHandler('nexttrack', () => next(false));
    ms.setActionHandler('seekto', (d) => { if (d.seekTime != null) audio.currentTime = d.seekTime; });
  }
  renderNowPlaying();
}

function setVolume(v) {
  audio.volume = Math.min(1, Math.max(0, v / 100));
  el.vol.style.setProperty('--p', `${v}%`);
  el.mute.innerHTML = icon(v === 0 ? 'mute' : 'volume');
  localStorage.setItem('mymusic.volume', String(v));
}
