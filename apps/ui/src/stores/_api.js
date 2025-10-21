import { getApiClient } from '../api/registry';

export function resolveApiFromStore(store) {
  return store && store.$api ? store.$api : getApiClient();
}
