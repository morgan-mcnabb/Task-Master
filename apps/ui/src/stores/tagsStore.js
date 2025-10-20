import { defineStore } from 'pinia';
import { resolveApiFromStore } from './_api';
import { makeAbortable } from '@/utils/makeAbortable';
import { normalizeTagsToStrings } from '@/utils/tags';

export const useTagsStore = defineStore('tags', {
  state: () => ({
    isLoading: false,
    suggestions: /** @type {string[]} */ ([]),
    lastQuery: '',
    errorMessage: '',
  }),

  actions: {
    async fetchSuggestions(query) {
      const api = resolveApiFromStore(this);
      this.isLoading = true;
      this.errorMessage = '';
      this.lastQuery = query ?? '';

      try {
        const queryString = this.lastQuery ? `?suggest=${encodeURIComponent(this.lastQuery)}` : '';
        const { data } = await api.get(`/api/v1/tags${queryString}`);
        const rawItems = Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : [];
        const normalized = normalizeTagsToStrings(rawItems);
        this.suggestions = normalized;
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to load tag suggestions.';
      } finally {
        this.isLoading = false;
      }
    },

    createSuggestRunner() {
      return makeAbortable(async ({ signal }, query) => {
        const api = resolveApiFromStore(this);
        this.isLoading = true;
        this.errorMessage = '';
        this.lastQuery = query ?? '';

        try {
          const queryString = this.lastQuery ? `?suggest=${encodeURIComponent(this.lastQuery)}` : '';
          const { data } = await api.get(`/api/v1/tags${queryString}`, { signal });
          const rawItems = Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : [];
          const normalized = normalizeTagsToStrings(rawItems);
          this.suggestions = normalized;
        } catch (error) {
          if (error?.name !== 'AbortError') {
            this.errorMessage = error?.message ?? 'Failed to load tag suggestions.';
          }
        } finally {
          this.isLoading = false;
        }
      });
    },
  },
});
