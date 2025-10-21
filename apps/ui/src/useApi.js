import { inject } from 'vue';
import { ApiClientKey } from './keys'; 

export function useApi() {
  const apiClient = inject(ApiClientKey, null);
  if (!apiClient) {
    throw new Error('API client was not provided. Ensure main.js provides ApiClientKey.');
  }
  return apiClient;
}
