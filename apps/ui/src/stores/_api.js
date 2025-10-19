/**
 * Helper to safely obtain the API client inside Pinia actions.
 * Prefers `store.$api` (plugin), falls back to the global registry.
 */
import { getApiClient } from '../api/registry';

export function resolveApiFromStore(store) {
  // Prefer plugin injection, but recover via registry if needed
  return store && store.$api ? store.$api : getApiClient();
}
