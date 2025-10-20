/**
 * Tag normalization helpers
 * - normalizeTagsToStrings: convert arrays of strings or objects to a unique, trimmed string array
 *   Supported object shapes: { name }, { label }, { value }, or any object with at least one string value
 *   Duplicates are removed case-insensitively.
 */

/**
 * @param {unknown} input
 * @returns {string[]}
 */
export function normalizeTagsToStrings(input) {
  if (!Array.isArray(input)) return [];

  const uniqueLowercase = new Set();
  const normalized = [];

  for (const rawItem of input) {
    let candidate = null;

    if (typeof rawItem === 'string') {
      candidate = rawItem;
    } else if (rawItem && typeof rawItem === 'object') {
      // Prefer common keys, then fallback to the first string value we can find.
      const possibleValues = [
        /** @type {any} */(rawItem).name,
        /** @type {any} */(rawItem).label,
        /** @type {any} */(rawItem).value,
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

/**
 * Dedupe a string array case-insensitively, preserving the first occurrence's casing.
 * @param {string[]} items
 * @returns {string[]}
 */
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
