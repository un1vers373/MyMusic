// Reusable HTML builders, modals and menus.
import { store, coverUrl, isLiked } from './store.js';
import { $, $$, esc, icon, fmtTime, hue, enc, plural } from './util.js';
import { player } from './player.js';

// ---------- Covers ----------
export function cover(id, seed, cls = '') {
  if (id) return `<div class="cover ${cls}"><img src="${coverUrl(id)}" alt="" loading="lazy"></div>`;
  return `<div class="cover ph ${cls}" style="--h:${hue(seed)}">${icon('music')}</div>`;
}
export const trackCover = (t, cls = '') => cover(t.hasCover ? t.id : null, t.title + t.artist, cls);

export function mosaic(ids, seed) {
  if (ids.length >= 4) return `<div class="cover mosaic">${ids.slice(0, 4).map((id) => cover(id, seed)).join('')}</div>`;
  return cover(ids[0] ?? null, seed);
}

// ---------- Links ----------
export const artistHref = (name) => `#/artist/${enc(name)}`;
export const albumHref = (artist, album) => `#/album/${enc(artist)}/${enc(album)}`;

// ---------- Cards ----------
export function trackCard(t, i, listKey) {
  return `<article class="card" tabindex="0" data-action="play-list" data-list="${listKey}" data-index="${i}" data-track="${t.id}">
    <div class="cover-wrap">${trackCover(t)}<span class="fab">${icon('play')}</span></div>
    <div class="t">${esc(t.title)}</div><div class="s">${esc(t.artist)}</div></article>`;
}

export function playlistCard(p) {
  return `<article class="card" tabindex="0" data-href="#/playlist/${p.id}">
    <div class="cover-wrap">${mosaic(p.coverTrackIds, p.name)}<button class="fab" data-action="play-playlist" data-id="${p.id}" aria-label="Play ${esc(p.name)}">${icon('play')}</button></div>
    <div class="t">${esc(p.name)}</div><div class="s">${plural(p.trackCount, 'song', 'songs')}</div></article>`;
}

export function albumCard(a) {
  return `<article class="card" tabindex="0" data-href="${albumHref(a.artist, a.name)}">
    <div class="cover-wrap">${cover(a.coverId, a.name + a.artist)}</div>
    <div class="t">${esc(a.name)}</div><div class="s">${esc(a.artist)}</div></article>`;
}

export function artistCard(a) {
  return `<article class="card artist" tabindex="0" data-href="${artistHref(a.name)}">
    <div class="cover-wrap">${cover(a.coverId, a.name, 'round')}</div>
    <div class="t">${esc(a.name)}</div><div class="s">${plural(a.count, 'song', 'songs')}</div></article>`;
}

// ---------- Track rows ----------
export function trackRow(t, i, listKey, opts = {}) {
  const liked = isLiked(t.id);
  return `<div class="row" role="button" tabindex="0" data-action="play-list" data-list="${listKey}" data-index="${i}" data-track="${t.id}">
    <div class="row-n"><span class="num">${i + 1}</span><span class="eq" aria-hidden="true"><i></i><i></i><i></i></span><span class="row-play">${icon('play')}</span></div>
    <div class="row-main">${trackCover(t)}<div style="min-width:0">
      <div class="row-title">${esc(t.title)}</div>
      <a class="row-artist" href="${artistHref(t.artist)}">${esc(t.artist)}</a></div></div>
    ${t.album ? `<a class="row-album" href="${albumHref(t.artist, t.album)}">${esc(t.album)}</a>` : '<span class="row-album">—</span>'}
    <button class="icon-btn like ${liked ? 'liked' : ''}" data-action="like" data-id="${t.id}" data-like="${t.id}" aria-label="${liked ? 'Remove from Liked Songs' : 'Save to Liked Songs'}">${icon('heart')}</button>
    <span class="row-dur">${fmtTime(t.duration)}</span>
    <button class="icon-btn" data-action="menu" data-id="${t.id}" ${opts.playlistId ? `data-playlist="${opts.playlistId}"` : ''} aria-label="More options for ${esc(t.title)}">${icon('more')}</button>
  </div>`;
}

export function trackTable(tracks, listKey, opts = {}) {
  return `<div class="tl"><div class="tl-head"><span>#</span><span>Title</span><span class="alb">Album</span><span></span><span class="r">Time</span><span></span></div>
    ${tracks.map((t, i) => trackRow(t, i, listKey, opts)).join('')}</div>`;
}

// ---------- Page pieces ----------
export function section(title, inner, { href, linkText = 'Show all' } = {}) {
  return `<section class="sec"><div class="sec-h"><h2>${esc(title)}</h2>${href ? `<a class="sec-link" href="${href}">${linkText}</a>` : ''}</div>${inner}</section>`;
}

export function empty({ title, text, action, label, iconName = 'music' }) {
  return `<div class="empty">${icon(iconName, 'lg')}<h3>${esc(title)}</h3><p>${esc(text ?? '')}</p>
    ${action ? `<button class="btn primary" data-action="${action}">${esc(label)}</button>` : ''}</div>`;
}

export function hero({ kind, title, desc, meta, coverHtml, seed }) {
  return `<section class="hero" style="--h:${hue(seed ?? title)}"><div class="hero-cover">${coverHtml}</div>
    <div class="hero-text"><span class="hero-kind">${esc(kind)}</span><h1>${esc(title)}</h1>
    ${desc ? `<p class="hero-desc">${esc(desc)}</p>` : ''}<p class="hero-meta">${esc(meta)}</p></div></section>`;
}

/** Highlights the row/card of the track that is currently loaded. */
export function markPlaying() {
  const cur = player.current();
  const playing = player.isPlaying();
  for (const n of $$('[data-track]')) {
    const isCur = !!cur && n.dataset.track === cur.id;
    n.classList.toggle('is-current', isCur);
    n.classList.toggle('is-playing', isCur && playing);
  }
}

export function updateLikeButtons(id, liked) {
  for (const b of $$(`[data-like="${id}"]`)) {
    b.classList.toggle('liked', liked);
    b.setAttribute('aria-label', liked ? 'Remove from Liked Songs' : 'Save to Liked Songs');
  }
}

// ---------- Modals ----------
export function openModal({ title, html, wide = false }) {
  const wrap = document.createElement('div');
  wrap.className = 'backdrop';
  wrap.innerHTML = `<div class="modal ${wide ? 'wide' : ''}" role="dialog" aria-modal="true" aria-label="${esc(title)}">
    <header class="modal-h"><h2>${esc(title)}</h2><button class="icon-btn" data-modal-close aria-label="Close">${icon('x')}</button></header>
    <div class="modal-b">${html}</div></div>`;
  $('#modal-root').appendChild(wrap);
  const previous = document.activeElement;
  let resolve;
  const closed = new Promise((r) => { resolve = r; });

  const close = (result) => {
    document.removeEventListener('keydown', onKey);
    wrap.remove();
    previous?.focus?.();
    resolve(result);
  };
  const onKey = (e) => { if (e.key === 'Escape') close(undefined); };
  document.addEventListener('keydown', onKey);
  wrap.addEventListener('mousedown', (e) => { if (e.target === wrap) close(undefined); });
  wrap.addEventListener('click', (e) => { if (e.target.closest('[data-modal-close]')) close(undefined); });

  const body = $('.modal-b', wrap);
  (body.querySelector('input:not([type=file]):not([hidden]), textarea') || $('[data-modal-close]', wrap))?.focus();
  return { body, close, closed };
}

export function promptText({ title, label, value = '', confirm = 'Save', description }) {
  const m = openModal({ title, html: `<form class="m-form" style="display:contents">
    <label class="field">${esc(label)}<input type="text" name="v" maxlength="100" value="${esc(value)}" required></label>
    ${description === undefined ? '' : `<label class="field">Description (optional)<textarea name="d" maxlength="300">${esc(description)}</textarea></label>`}
    <p class="form-error" role="alert"></p>
    <div class="modal-f"><button type="button" class="btn" data-modal-close>Cancel</button><button class="btn primary" type="submit">${esc(confirm)}</button></div></form>` });
  const form = $('.m-form', m.body);
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const v = form.v.value.trim();
    if (!v) { $('.form-error', form).textContent = 'Enter a name.'; return; }
    m.close({ value: v, description: form.d?.value.trim() ?? null });
  });
  return m.closed;
}

export function confirmDialog({ title, text, confirm = 'Delete', danger = true }) {
  const m = openModal({ title, html: `<p class="text">${esc(text)}</p>
    <div class="modal-f"><button class="btn" data-modal-close>Cancel</button><button class="btn ${danger ? 'danger' : 'primary'}" data-m-ok>${esc(confirm)}</button></div>` });
  $('[data-m-ok]', m.body).addEventListener('click', () => m.close(true));
  return m.closed.then((r) => r === true);
}

// ---------- Context menu ----------
let menuEl = null;
const outside = (e) => { if (menuEl && !menuEl.contains(e.target)) closeMenu(); };
const onMenuKey = (e) => { if (e.key === 'Escape') closeMenu(); };

export function closeMenu() {
  if (!menuEl) return;
  menuEl.remove();
  menuEl = null;
  document.removeEventListener('pointerdown', outside, true);
  document.removeEventListener('keydown', onMenuKey);
  window.removeEventListener('scroll', closeMenu, true);
  window.removeEventListener('resize', closeMenu);
}

export function openMenu(anchor, items) {
  closeMenu();
  const m = document.createElement('div');
  m.className = 'menu';
  m.setAttribute('role', 'menu');
  m.innerHTML = items.map((it, i) => `<button role="menuitem" data-i="${i}" class="${it.danger ? 'danger' : ''}">${icon(it.icon)}<span>${esc(it.label)}</span></button>`).join('');
  document.body.appendChild(m);
  const r = anchor.getBoundingClientRect();
  const left = Math.max(8, Math.min(r.right - m.offsetWidth, innerWidth - m.offsetWidth - 8));
  const top = r.bottom + 4 + m.offsetHeight > innerHeight ? Math.max(8, r.top - m.offsetHeight - 4) : r.bottom + 4;
  m.style.left = `${left}px`;
  m.style.top = `${top}px`;
  m.addEventListener('click', (e) => {
    const b = e.target.closest('button');
    if (!b) return;
    const item = items[Number(b.dataset.i)];
    closeMenu();
    item.action();
  });
  menuEl = m;
  document.addEventListener('pointerdown', outside, true);
  document.addEventListener('keydown', onMenuKey);
  window.addEventListener('scroll', closeMenu, true);
  window.addEventListener('resize', closeMenu);
  m.querySelector('button')?.focus();
}
