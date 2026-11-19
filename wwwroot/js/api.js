export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

let onUnauthorized = () => {};
export const setUnauthorizedHandler = (fn) => { onUnauthorized = fn; };

async function parseError(res) {
  try {
    const data = await res.json();
    return data.error || data.title || 'Request failed.';
  } catch {
    return res.status === 413 ? 'The upload is too large.' : `Request failed (${res.status}).`;
  }
}

async function request(method, url, body) {
  const opts = { method, credentials: 'same-origin', headers: {} };
  if (body instanceof FormData) {
    opts.body = body;
  } else if (body !== undefined) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  let res;
  try {
    res = await fetch(url, opts);
  } catch {
    throw new ApiError(0, 'Cannot reach the server. Check your connection.');
  }
  if (!res.ok) {
    if (res.status === 401) onUnauthorized();
    throw new ApiError(res.status, await parseError(res));
  }
  if (res.status === 204) return null;
  return res.json();
}

export const api = {
  get: (url) => request('GET', url),
  post: (url, body) => request('POST', url, body ?? {}),
  put: (url, body) => request('PUT', url, body),
  del: (url) => request('DELETE', url),

  /** multipart upload with progress (fetch cannot report upload progress). */
  upload(url, formData, onProgress) {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', url);
      xhr.responseType = 'json';
      xhr.upload.onprogress = (e) => { if (e.lengthComputable) onProgress?.(e.loaded / e.total); };
      xhr.onerror = () => reject(new ApiError(0, 'Upload failed. Check your connection.'));
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) return resolve(xhr.response);
        if (xhr.status === 401) onUnauthorized();
        const msg = xhr.response?.error || (xhr.status === 413 ? 'The file is too large.' : `Upload failed (${xhr.status}).`);
        reject(new ApiError(xhr.status, msg));
      };
      xhr.send(formData);
    });
  },
};
