export function normalizeTagsToStrings(input) {
  if (!Array.isArray(input)) return [];

  const uniqueLowercase = new Set();
  const normalized = [];

  for (const rawItem of input) {
    let candidate = null;

    if (typeof rawItem === 'string') {
      candidate = rawItem;
    } else if (rawItem && typeof rawItem === 'object') {
      const possibleValues = [
        (rawItem).name,
        (rawItem).label,
        (rawItem).value,
      ];

      let chosen = possibleValues.find(v => typeof v === 'string' && v.trim().length > 0);
      if (!chosen) {
        for (const value of Object.values(rawItem)) {
          if (typeof value === 'string' && value.trim().length > 0) {
            chosen = value;
            break;
          }
        }
      }
      candidate = chosen ?? null;
    }

    if (typeof candidate === 'string') {
      const trimmed = candidate.trim();
      const lowered = trimmed.toLowerCase();
      if (trimmed.length > 0 && !uniqueLowercase.has(lowered)) {
        uniqueLowercase.add(lowered);
        normalized.push(trimmed);
      }
    }
  }

  return normalized;
}

export function dedupeCaseInsensitive(items) {
  if (!Array.isArray(items)) return [];
  const seen = new Set();
  const result = [];
  for (const raw of items) {
    const trimmed = typeof raw === 'string' ? raw.trim() : '';
    if (!trimmed) continue;
    const lowered = trimmed.toLowerCase();
    if (seen.has(lowered)) continue;
    seen.add(lowered);
    result.push(trimmed);
  }
  return result;
}
