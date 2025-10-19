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
  // RFC4122-ish fallback; fine for correlation, not for cryptographic use.
  // eslint-disable-next-line no-bitwise
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    // eslint-disable-next-line no-bitwise
    const r = (Math.random() * 16) | 0;
    // eslint-disable-next-line no-bitwise
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function resolveApiBaseUrl() {
  const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim();
  // Empty string means same-origin
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

/**
 * ProblemDetails structure:
 * https://datatracker.ietf.org/doc/html/rfc7807
 */
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

/** Notifier (DI-friendly, minimal default) */
function createDefaultNotifier() {
  return {
    toast(message) {
      try { console.warn('[toast]', message); } catch {}
    },
    lockoutStart(totalSeconds) {
      try { console.warn(`[lockout] You can retry in ${totalSeconds}s`); } catch {}
    },
    lockoutTick(secondsRemaining) {
      try { console.warn(`[lockout] ${secondsRemaining}s remaining`); } catch {}
    },
    lockoutEnd() {
      try { console.warn('[lockout] ended'); } catch {}
    },
  };
}

/** Parse Retry-After header (seconds or HTTP-date). */
function parseRetryAfter(headerValue) {
  if (!headerValue) return null;
  const asInt = Number.parseInt(headerValue, 10);
  if (!Number.isNaN(asInt)) return Math.max(0, asInt);
  const retryDate = new Date(headerValue);
  if (Number.isNaN(retryDate.getTime())) return null;
  const diffMs = retryDate.getTime() - Date.now();
  return Math.max(0, Math.ceil(diffMs / 1000));
}

/** Simple countdown; returns cancel function. */
function startCountdown(seconds, notifier) {
  if (!(seconds > 0)) return () => {};
  let remaining = seconds;
  notifier.lockoutStart(remaining);
  const timerId = setInterval(() => {
    remaining -= 1;
    if (remaining > 0) {
      notifier.lockoutTick(remaining);
    } else {
      clearInterval(timerId);
      notifier.lockoutEnd();
    }
  }, 1000);
  return () => clearInterval(timerId);
}

/**
 * Handle non-2xx side effects WITHOUT navigation.
 * 423 and 429 get user feedback; 401/403 are intentionally NO-OP here.
 */
function handleResponseSideEffects(response, notifier) {
  const status = response.status;

  if (status === 423) {
    const retryHeader = response.headers.get('Retry-After');
    const seconds = parseRetryAfter(retryHeader);
    if (typeof seconds === 'number' && seconds > 0) {
      startCountdown(seconds, notifier);
    } else {
      notifier.toast('Your account is temporarily locked. Please try again later.');
    }
    return;
  }

  if (status === 429) {
    notifier.toast('Too many requests; try again shortly');
  }
}

/**
 * createApiClient
 *
 * Options:
 *  - baseUrl?: string
 *  - fetchImpl?: typeof fetch
 *  - notifier?: { toast(msg), lockoutStart(n), lockoutTick(n), lockoutEnd() }
 *  - defaultHeaders?: Record<string,string>
 */
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

    // Only set content-type when sending a non-FormData/Blob body
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

    // 204: no content, still "ok"
    if (response.status === 204) {
      return { ok: true, data: null, response };
    }

    // Parse response body (tolerate empty)
    const contentType = response.headers.get('content-type') || '';
    const isJsonResponse =
      contentType.includes('application/json') ||
      contentType.includes('application/problem+json');
    const parsed = isJsonResponse
      ? await response.json().catch(() => null)
      : await response.text().catch(() => null);

    if (response.ok) {
      return { ok: true, data: parsed, response };
    }

    // Build an ApiError with ProblemDetails if present
    const problem = isJsonResponse ? parseProblemDetails(parsed, response.status) : null;
    const error = new ApiError({
      message: problem?.title || (parsed && parsed.message) || `Request failed with status ${response.status}`,
      status: response.status,
      url,
      method,
      data: parsed,
      problem,
    });

    // Non-navigational side effects (toasts/lockout), no redirects here
    handleResponseSideEffects(response, notifier);

    throw error;
  }

  return {
    request: requestJson,
    get: (path, options = {}) => requestJson(path, { ...options, method: 'GET' }),
    post: (path, body, options = {}) => requestJson(path, { ...options, method: 'POST', body }),
    put: (path, body, options = {}) => requestJson(path, { ...options, method: 'PUT', body }),
    patch: (path, body, options = {}) => requestJson(path, { ...options, method: 'PATCH', body }),
    delete: (path, options = {}) => requestJson(path, { ...options, method: 'DELETE' }),

    // Utilities surfaced for consumers:
    readETag,
    withIfMatch,
  };
}
