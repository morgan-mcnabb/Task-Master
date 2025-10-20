/**
 * API Client (fetch-based, navigation-agnostic)
 * - Base URL from VITE_API_BASE_URL ('' => same-origin)
 * - credentials: 'include'
 * - X-Correlation-Id (uuid v4) persisted per tab in sessionStorage
 * - JSON helpers: get/post/put/patch/delete (Content-Type only when sending a body)
 * - ProblemDetails handler: surfaces { title, errors, status } cleanly
 * - Status handling (non-navigational):
 *    423 → lockout notifier (supports Retry-After header)
 *    429 → toast “Too many requests; try again shortly”
 * - ETag helpers: readETag(response) and withIfMatch(headers, etag)
 *
 * IMPORTANT: This client does NOT perform router navigation on 401/403.
 *            Route guards are responsible for redirecting to /login, etc.
 */

const CORRELATION_ID_STORAGE_KEY = 'tm.correlationId';

function getOrCreateCorrelationId() {
  try {
    const existing = sessionStorage.getItem(CORRELATION_ID_STORAGE_KEY);
    if (existing) return existing;
    const generated =
      (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function')
        ? crypto.randomUUID()
        : generateUuidV4Fallback();
    sessionStorage.setItem(CORRELATION_ID_STORAGE_KEY, generated);
    return generated;
  } catch {
    return (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function')
      ? crypto.randomUUID()
      : generateUuidV4Fallback();
  }
}

function generateUuidV4Fallback() {
  // RFC4122-ish fallback
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function resolveApiBaseUrl() {
  const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim();
  return raw.replace(/\/$/, '');
}

function joinUrl(baseUrl, path) {
  if (!path) return baseUrl || '';
  const isAbsolute = /^https?:\/\//i.test(path);
  if (isAbsolute) return path;
  if (!baseUrl) return path.startsWith('/') ? path : `/${path}`;
  const normalizedBase = baseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}

/** RFC7807 helper */
function parseProblemDetails(maybeJson, status) {
  const title = maybeJson?.title || 'Request failed';
  const errors = maybeJson?.errors || null;
  return { title, errors, status };
}

export class ApiError extends Error {
  constructor({ message, status, url, method, data, problem }) {
    super(message ?? 'API Error');
    this.name = 'ApiError';
    this.status = status;
    this.url = url;
    this.method = method;
    this.data = data;
    this.problem = problem ?? null;
  }
}

/** ETag helpers */
export function readETag(response) {
  return response.headers.get('ETag') || response.headers.get('etag') || null;
}
export function withIfMatch(headers = {}, etag) {
  if (!etag) return headers;
  return { ...headers, 'If-Match': etag };
}

/** Notifier (DI-friendly) */
function createDefaultNotifier() {
  return {
    toast(message) { try { console.warn('[toast]', message); } catch {} },
    lockoutStart(total) { try { console.warn(`[lockout] retry in ${total}s`); } catch {} },
    lockoutTick(remaining) { try { console.warn(`[lockout] ${remaining}s`); } catch {} },
    lockoutEnd() { try { console.warn('[lockout] ended'); } catch {} },
  };
}

function parseRetryAfter(headerValue) {
  if (!headerValue) return null;
  const asInt = Number.parseInt(headerValue, 10);
  if (!Number.isNaN(asInt)) return Math.max(0, asInt);
  const retryDate = new Date(headerValue);
  if (Number.isNaN(retryDate.getTime())) return null;
  const diffMs = retryDate.getTime() - Date.now();
  return Math.max(0, Math.ceil(diffMs / 1000));
}
function startCountdown(seconds, notifier) {
  if (!(seconds > 0)) return () => {};
  let remaining = seconds;
  notifier.lockoutStart(remaining);
  const id = setInterval(() => {
    remaining -= 1;
    if (remaining > 0) notifier.lockoutTick(remaining);
    else { clearInterval(id); notifier.lockoutEnd(); }
  }, 1000);
  return () => clearInterval(id);
}

/** Non-2xx side effects (no navigation) */
function handleResponseSideEffects(response, notifier) {
  const status = response.status;
  if (status === 423) {
    const retryHeader = response.headers.get('Retry-After');
    const seconds = parseRetryAfter(retryHeader);
    if (typeof seconds === 'number' && seconds > 0) startCountdown(seconds, notifier);
    else notifier.toast('Your account is temporarily locked. Please try again later.');
    return;
  }
  if (status === 429) {
    notifier.toast('Too many requests; try again shortly');
  }
}

/** Factory */
export function createApiClient({
  baseUrl = resolveApiBaseUrl(),
  fetchImpl = fetch,
  notifier = createDefaultNotifier(),
  defaultHeaders = {},
} = {}) {
  const correlationId = getOrCreateCorrelationId();

  async function requestJson(path, {
    method = 'GET',
    headers = {},
    body,
    signal,
  } = {}) {
    const url = joinUrl(baseUrl, path);
    const mergedHeaders = {
      Accept: 'application/json',
      'X-Correlation-Id': correlationId,
      ...defaultHeaders,
      ...headers,
    };
    const hasJsonBody = body != null && !(body instanceof FormData) && !(body instanceof Blob);
    const finalHeaders = hasJsonBody
      ? { 'Content-Type': 'application/json', ...mergedHeaders }
      : mergedHeaders;

    const response = await fetchImpl(url, {
      method,
      headers: finalHeaders,
      body: hasJsonBody ? JSON.stringify(body) : body,
      signal,
      credentials: 'include',
    });

    if (response.status === 204) {
      return { ok: true, data: null, response };
    }

    const contentType = response.headers.get('content-type') || '';
    const isJson =
      contentType.includes('application/json') ||
      contentType.includes('application/problem+json');
    const parsed = isJson
      ? await response.json().catch(() => null)
      : await response.text().catch(() => null);

    if (response.ok) {
      return { ok: true, data: parsed, response };
    }

    const problem = isJson ? parseProblemDetails(parsed, response.status) : null;
    const error = new ApiError({
      message: problem?.title || (parsed && parsed.message) || `Request failed with status ${response.status}`,
      status: response.status,
      url,
      method,
      data: parsed,
      problem,
    });

    handleResponseSideEffects(response, notifier);
    throw error;
  }

  return {
    correlationId, // ← expose for error toasts

    request: requestJson,
    get: (path, options = {}) => requestJson(path, { ...options, method: 'GET' }),
    post: (path, body, options = {}) => requestJson(path, { ...options, method: 'POST', body }),
    put: (path, body, options = {}) => requestJson(path, { ...options, method: 'PUT', body }),
    patch: (path, body, options = {}) => requestJson(path, { ...options, method: 'PATCH', body }),
    delete: (path, options = {}) => requestJson(path, { ...options, method: 'DELETE' }),

    readETag,
    withIfMatch,
  };
}
