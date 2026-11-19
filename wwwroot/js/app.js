import { api, setUnauthorizedHandler } from './api.js';
import { store, resetStore, ingest } from './store.js';
import { $, $$, toast, icon, hue } from './util.js';
import { player } from './player.js';
import { renderRoute } from './views.js';
import { markPlaying, closeMenu } from './ui.js';
import * as act from './actions.js';

// ---------- Screens ----------
function showAuth() {
  $('#app').hidden = true;
  $('#auth').hidden = false;
  $('#tab-register').hidden = !store.config.registrationOpen;
  setAuthMode('login');
  $('#auth-form').username.focus();
}

let authMode = 'login';
function setAuthMode(mode) {
  authMode = mode;
  $$('[data-auth-tab]').forEach((b) => b.classList.toggle('on', b.dataset.authTab === mode));
  $('#auth-submit').textContent = mode === 'login' ? 'Sign in' : 'Create account';
  $('#auth-form').password.autocomplete = mode === 'login' ? 'current-password' : 'new-password';
  $('#auth-error').textContent = '';
}

async function enterApp() {
  $('#auth').hidden = true;
  $('#app').hidden = false;
  $('#user-name').textContent = store.user.username;
  $('#user-avatar').textContent = store.user.username[0].toUpperCase();
  await act.refreshLibrary();
  player.restore(store.user.id, new Set(store.tracks.map((t) => t.id)));
  if (!location.hash) location.hash = '#/';
  await renderRoute();
}

async function logout() {
  try { await api.post('/api/auth/logout'); } catch { /* already signed out */ }
  leave();
}

function leave(message) {
  player.stop();
  resetStore();
  store.config = store.config ?? {};
  location.hash = '';
  showAuth();
  if (message) toast(message, 'error');
}

// ---------- Auth form ----------
$$('[data-auth-tab]').forEach((b) => b.addEventListener('click', () => setAuthMode(b.dataset.authTab)));
$('#auth-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const f = e.target;
  const err = $('#auth-error');
  err.textContent = '';
  const username = f.username.value.trim();
  if (username.length < 3) { err.textContent = 'Enter your username (at least 3 characters).'; return; }
  if (f.password.value.length < 8) { err.textContent = 'Password must be at least 8 characters.'; return; }
  const btn = $('#auth-submit');
  btn.disabled = true;
  try {
    store.user = await api.post(authMode === 'login' ? '/api/auth/login' : '/api/auth/register', { username, password: f.password.value });
    f.reset();
    await enterApp();
  } catch (ex) {
    err.textContent = ex.message;
  } finally {
    btn.disabled = false;
  }
});

// ---------- Global click handling (no inline handlers: the CSP forbids them) ----------
document.addEventListener('click', async (e) => {
  const actEl = e.target.closest('[data-action]');
  const link = e.target.closest('a[href]');
  if (link && (!actEl || actEl.contains(link))) return; // normal navigation

  if (!actEl) {
    const card = e.target.closest('[data-href]');
    if (card) location.hash = card.dataset.href;
    return;
  }
  const d = actEl.dataset;
  switch (d.action) {
    case 'play-list': return act.playFromList(d.list, Number(d.index));
    case 'play-all': return act.playAll(d.list, !!d.shuffle);
    case 'play-playlist': return act.playPlaylist(d.id);
    case 'like': return act.toggleLike(d.id);
    case 'menu': return act.trackMenu(actEl, d.id, d.playlist);
    case 'new-playlist': return act.createPlaylist();
    case 'rename-playlist': return act.renamePlaylist(d.id);
    case 'delete-playlist': return act.deletePlaylist(d.id);
    case 'add-songs': return act.addSongs(d.id);
    case 'upload': return act.uploadModal();
    case 'logout': return logout();
  }
});

document.addEventListener('keydown', (e) => {
  if (e.key !== 'Enter') return;
  const t = e.target;
  if (t.matches('.row[data-action], .card[data-action]')) { e.preventDefault(); t.click(); }
  else if (t.matches('.card[data-href]')) { location.hash = t.dataset.href; }
});

// Broken cover image -> placeholder
document.addEventListener('error', (e) => {
  const img = e.target;
  if (img.tagName !== 'IMG') return;
  const box = img.closest('.cover');
  if (!box) return;
  box.classList.add('ph');
  box.style.setProperty('--h', String(hue(img.src)));
  box.innerHTML = icon('music');
}, true);

document.addEventListener('likes:sync', () => player.refreshLike());
document.addEventListener('player:state', markPlaying);
window.addEventListener('hashchange', () => { closeMenu(); if (!$('#app').hidden) { renderRoute(); } });

setUnauthorizedHandler(() => { if (store.user) leave('Your session has expired. Sign in again.'); });

// ---------- Boot ----------
(async function boot() {
  player.init();
  try { store.config = await api.get('/api/auth/config'); } catch { /* keep defaults */ }
  try {
    store.user = await api.get('/api/auth/me');
    await enterApp();
  } catch {
    showAuth();
  }
})();
