import { markRaw } from 'vue';
/**
 * Pinia plugin that injects an API client into every store as `store.$api`.
 * We both RETURN the property (Pinia-idiomatic) and also define it on the store
 * for extra robustness in dev/HMR edge cases.
 */
export function createPiniaApiPlugin(apiClient) {
  /*return ({ store }) => {
    // Define on the instance (non-configurable, non-writable) for stability.
    try {
      if (!Object.prototype.hasOwnProperty.call(store, '$api')) {
        Object.defineProperty(store, '$api', {
          value: apiClient,
          enumerable: false,
          configurable: false,
          writable: false,
        });
      }
    } catch {
      // In case of proxies/HMR weirdness, we'll still return it below.
    }

    // Pinia will merge this onto /*the store, too.
    return { $api: apiClient };
  };*/
  const rawClient = markRaw(apiClient);
  return () => ({$api: rawClient });
}
