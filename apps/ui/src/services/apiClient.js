/**
 * Minimal, testable HTTP client with dependency injection.
 * - Uses fetch by default; pass a mock for tests.
 * - JSON by default; throws ApiError on non-2xx responses.
 */

export class ApiError extends Error {
  constructor({ message, status, url, method, data }) {
    super(message ?? 'API Error');
    this.name = 'ApiError';
    this.status = status;
    this.url = url;
    this.method = method;
    this.data = data;
  }
}

/**
 * Resolves base URL from env. Empty string ⇒ same-origin.
 */
export function resolveApiBaseUrl() {
  // Ensure no trailing slash; empty string is valid (same-origin).
  const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim();
  return raw.replace(/\/$/, '');
}

/**
 * Safe path join that respects absolute URLs.
 */
function buildUrl(baseUrl, path) {
  if (!path) return baseUrl;
  const isAbsolute = /^https?:\/\//i.test(path);
  if (isAbsolute) return path;
  if (!baseUrl) return path.startsWith('/') ? path : `/${path}`;
  const normalizedBase = baseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}

/**
 * Creates an API client. Pass `fetchImpl` for tests.
 */
export function createApiClient({
  baseUrl = resolveApiBaseUrl(),
  defaultHeaders = {},
  fetchImpl = fetch,
} = {}) {
  async function request(path, { method = 'GET', headers = {}, body, signal } = {}) {
    const url = buildUrl(baseUrl, path);
    const mergedHeaders = {
      'Accept': 'application/json',
      ...(body != null && !(body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}),
      ...defaultHeaders,
      ...headers,
    };

    const response = await fetchImpl(url, {
      method,
      headers: mergedHeaders,
      body: body != null && !(body instanceof FormData) ? JSON.stringify(body) : body,
      signal,
      credentials: 'include', // tweak if you do not want cookies / auth flows
    });

    const contentType = response.headers.get('content-type') || '';
    const isJson = contentType.includes('application/json');
    const payload = isJson ? await response.json().catch(() => null) : await response.text();

    if (!response.ok) {
      throw new ApiError({
        message: (payload && payload.message) || `Request failed with status ${response.status}`,
        status: response.status,
        url,
        method,
        data: payload,
      });
    }
    return payload;
  }

  return {
    request,
    get: (path, options = {}) => request(path, { ...options, method: 'GET' }),
    post: (path, body, options = {}) => request(path, { ...options, method: 'POST', body }),
    put: (path, body, options = {}) => request(path, { ...options, method: 'PUT', body }),
    patch: (path, body, options = {}) => request(path, { ...options, method: 'PATCH', body }),
    delete: (path, options = {}) => request(path, { ...options, method: 'DELETE' }),

    /**
     * Returns a new client with Authorization header set.
     */
    withAuth(token) {
      return createApiClient({
        baseUrl,
        fetchImpl,
        defaultHeaders: {
          ...defaultHeaders,
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });
    },
  };
}
