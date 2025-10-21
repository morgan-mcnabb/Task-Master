import { markRaw } from 'vue';

export function createPiniaApiPlugin(apiClient) {
  const rawClient = markRaw(apiClient);
  return () => ({$api: rawClient });
}
