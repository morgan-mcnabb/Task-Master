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

export function resolveApiBaseUrl() {
  // Ensure no trailing slash; empty string is valid (same-origin).
  const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim();
  return raw.replace(/\/$/, '');
}

function buildUrl(baseUrl, path) {
  if (!path) return baseUrl;
  const isAbsolute = /^https?:\/\//i.test(path);
  if (isAbsolute) return path;
  if (!baseUrl) return path.startsWith('/') ? path : `/${path}`;
  const normalizedBase = baseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}

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
      credentials: 'include',
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
