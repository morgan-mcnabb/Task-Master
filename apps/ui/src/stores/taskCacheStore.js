import { defineStore } from 'pinia';

export const useTaskCacheStore = defineStore('taskCache', {
  state: () => ({
    etagById: /** @type {Map<string, string>} */ (new Map()),
  }),

  actions: {
    setETag(taskId, etag) {
      if (!taskId || !etag) return;
      this.etagById.set(String(taskId), String(etag));
    },
    getETag(taskId) {
      if (!taskId) return null;
      return this.etagById.get(String(taskId)) ?? null;
    },
    clearETag(taskId) {
      if (!taskId) return;
      this.etagById.delete(String(taskId));
    },
    reset() {
      this.etagById.clear();
    },
  },
});
