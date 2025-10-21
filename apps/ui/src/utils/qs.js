export function buildQueryString(params, { addPrefix = true } = {}) {
  if (!params || typeof params !== 'object') return addPrefix ? '' : '';

  const parts = [];

  const append = (key, value) => {
    parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`);
  };

  for (const [key, raw] of Object.entries(params)) {
    if (raw == null) continue; // skip null/undefined

    if (Array.isArray(raw)) {
      if (raw.length === 0) continue;
      for (const item of raw) {
        if (item == null) continue;
        append(key, item instanceof Date ? item.toISOString() : item);
      }
      continue;
    }

    const value = raw instanceof Date ? raw.toISOString() : raw;
    append(key, value);
  }

  if (parts.length === 0) return addPrefix ? '' : '';
  const qs = parts.join('&');
  return addPrefix ? `?${qs}` : qs;
}
