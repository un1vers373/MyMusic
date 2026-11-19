export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ESC[c]);

export function fmtTime(sec) {
  sec = Math.max(0, Math.floor(Number(sec) || 0));
  return `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`;
}

export function fmtTotal(sec) {
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  return h ? `${h} hr ${m} min` : `${Math.max(m, sec > 0 ? 1 : 0)} min`;
}

export function hue(str) {
  let h = 0;
  for (const ch of String(str ?? '')) h = (h * 31 + ch.codePointAt(0)) % 360000;
  return h % 360;
}

export const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;
export const enc = encodeURIComponent;

export function debounce(fn, ms) {
  let t;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), ms);
  };
}

export const icon = (name, cls = '') =>
  `<svg class="ic ${cls}" aria-hidden="true"><use href="#i-${name}"/></svg>`;

export function toast(message, kind = 'info') {
  const root = $('#toast-root');
  const el = document.createElement('div');
  el.className = `toast ${kind}`;
  el.textContent = message;
  root.appendChild(el);
  setTimeout(() => el.remove(), kind === 'error' ? 4500 : 2600);
}
