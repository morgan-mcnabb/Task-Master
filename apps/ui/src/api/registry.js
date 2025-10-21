let currentClient = null;

export function setApiClient(apiClient) {
  currentClient = apiClient;
}

export function getApiClient() {
  if (!currentClient) {
    throw new Error('API client not initialized. Ensure setApiClient() is called in main.js.');
  }
  return currentClient;
}
