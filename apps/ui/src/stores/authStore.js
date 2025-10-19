import { defineStore } from 'pinia';
import { resolveApiFromStore } from './_api';
import { useTasksStore } from './tasksStore';
import { useTaskCacheStore } from './taskCacheStore';

/**
 * Auth store
 * State:
 *  - userName: string | null
 *  - settings: Record<string, unknown>
 *  - isLoaded: boolean  (true after loadMe() settles once)
 *
 * Actions:
 *  - loadMe()
 *  - login(userName, password)
 *  - register(payload)
 *  - logout()
 */
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
        // Backend route is /auth/me (no /api prefix)
        const { data } = await api.get('/auth/me');
        // Expect `{ userName, settings }`
        this._applyMe(data);
      } catch {
        // Swallow errors; 401/403 handled by client redirect logic.
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

        // Backend: /auth/login (no /api prefix)
      await api.post('/auth/login', { userName, password });

      // Must verify the session actually exists
      await this.loadMe();

      if (!this.isAuthenticated) {
          // Give a useful error; Login.vue will show it.
          throw new Error('Login succeeded but session was not established. Check cookies and SameSite settings.');
      }
      return true;
    },

    /**
     * `payload` allows future expansion (email, displayName, etc.)
     * Expected minimal shape: { userName, password }
     */
    async register(payload) {
      if (!payload?.userName || !payload?.password) {
        throw new Error('Username and password are required.');
      }
      const api = resolveApiFromStore(this);
      // Backend route is /auth/register (no /api prefix)
      await api.post('/auth/register', payload);
      await this.loadMe();
      return true;
    },

    async logout() {
      const api = resolveApiFromStore(this);
      try {
        // Backend route is /auth/logout (no /api prefix)
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
