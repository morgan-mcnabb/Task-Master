import { defineStore } from 'pinia';
import { resolveApiFromStore } from './_api';
import { useTasksStore } from './tasksStore';
import { useTaskCacheStore } from './taskCacheStore';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    userName: /** @type {string|null} */ (null),
    settings: /** @type {Record<string, unknown>} */ ({}),
    isLoaded: false,
  }),

  getters: {
    isAuthenticated(state) {
      return Boolean(state.userName);
    },
  },

  actions: {
    _applyMe(me) {
      this.userName = me?.userName ?? null;
      this.settings = me?.settings ?? {};
    },

    async loadMe() {
      const api = resolveApiFromStore(this);
      try {
        const { data } = await api.get('/auth/me');
        this._applyMe(data);
      } catch {
        this.userName = null;
        this.settings = {};
      } finally {
        this.isLoaded = true;
      }
    },

    async login(userName, password) {
      if (!userName || !password) {
          throw new Error('Username and password are required.');
      }
      const api = resolveApiFromStore(this);

      await api.post('/auth/login', { userName, password });

      await this.loadMe();

      if (!this.isAuthenticated) {
          throw new Error('Login succeeded but session was not established. Check cookies and SameSite settings.');
      }
      return true;
    },

    async register(payload) {
      if (!payload?.userName || !payload?.password) {
        throw new Error('Username and password are required.');
      }
      const api = resolveApiFromStore(this);
      await api.post('/auth/register', payload);
      await this.loadMe();
      return true;
    },

    async logout() {
      const api = resolveApiFromStore(this);
      try {
        await api.post('/auth/logout');
      } finally {
        this.userName = null;
        this.settings = {};
        this.isLoaded = false;

        try {
            const tasks = useTasksStore();
            const cache = useTaskCacheStore();
            
            tasks.$reset();
            cache.$reset();
        } catch {}
    }
    },
  },
});
