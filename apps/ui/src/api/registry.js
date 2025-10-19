/**
 * Very small service locator for the API client.
 * - main.js will call setApiClient(...)
 * - stores can always recover via getApiClient() if plugin injection failed.
 */

let currentClient = null;

/** Set the global API client instance (called from main.js). */
export function setApiClient(apiClient) {
  currentClient = apiClient;
}

/** Get the current API client instance or throw if unset. */
export function getApiClient() {
  if (!currentClient) {
    throw new Error('API client not initialized. Ensure setApiClient() is called in main.js.');
  }
  return currentClient;
}
