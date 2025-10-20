import { defineStore } from 'pinia';
import { useTaskCacheStore } from './taskCacheStore';
import { resolveApiFromStore } from './_api';
import { buildQueryString } from '@/utils/qs';
import { normalizeTagsToStrings } from '@/utils/tags';
import { TASK_STATUSES } from '@/constants/tasks';

function mapDtoToUi(dto) {
  return {
    id: dto.id,
    title: dto.title,
    status: dto.status,
    priority: dto.priority,
    dueDate: dto.dueDate || null,
    // Normalize to string[] regardless of backend shape (strings or { id, name } objects).
    tags: normalizeTagsToStrings(dto.tags),
    isCompleted: String(dto.status).toLowerCase() === 'done',
    _raw: dto,
  };
}

function statusFor(isCompleted) {
  return isCompleted ? 'Done' : 'Todo';
}

export const useTasksStore = defineStore('tasks', {
  state: () => ({
    tasks: /** @type {Array<{id:string,title:string,status:string,priority:string,dueDate:string|null,tags:string[],isCompleted:boolean,_raw?:any}>} */ ([]),
    isLoading: false,
    /** Used to avoid UI stutter: keep content and show a light refresher after first load */
    hasLoadedOnce: false,
    errorMessage: '',
    errorCorrelationId: '',
    pageNumber: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
    sortBy: 'dueDate',
    sortDirection: 'asc',
    lastRequestParams: /** @type {Record<string, any>|null} */ (null),
  }),

  getters: {
    hasTasks(state) {
      return state.tasks.length > 0;
    },
    completedCount(state) {
      return state.tasks.filter(task => task.isCompleted).length;
    },
    activeCount(state) {
      return state.tasks.filter(task => !task.isCompleted).length;
    },
  },

  actions: {
    async fetchTasks(params, { signal } = {}) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      this.isLoading = true;
      this.errorMessage = '';
      this.errorCorrelationId = '';
      this.lastRequestParams = params || null;

      try {
        const queryString = buildQueryString(params ?? {});
        const { data, response } = await api.get(`/api/v1/tasks${queryString}`, { signal });

        const items = Array.isArray(data?.items) ? data.items : [];
        this.tasks = items.map(mapDtoToUi);

        for (const dto of items) {
          if (dto?.id && dto?.eTag) {
            cache.setETag(String(dto.id), dto.eTag);
          }
        }

        this.pageNumber = Number(data?.pageNumber ?? params?.pageNumber ?? this.pageNumber);
        this.pageSize = Number(data?.pageSize ?? params?.pageSize ?? this.pageSize);
        this.totalCount = Number(data?.totalCount ?? this.tasks.length);
        this.totalPages = Math.max(1, Math.ceil(this.totalCount / Math.max(1, this.pageSize)));
        this.sortBy = String(params?.sortBy ?? this.sortBy);
        this.sortDirection = String(params?.sortDirection ?? this.sortDirection);

        this.hasLoadedOnce = true;

        return { data, response };
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to load tasks.';
        try { this.errorCorrelationId = api.correlationId || ''; } catch { this.errorCorrelationId = ''; }
        throw error;
      } finally {
        this.isLoading = false;
      }
    },

    /**
     * Simple quick-create retained for potential inline-add UIs.
     */
    async createTask(title) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      if (!title || !title.trim()) return;
      this.errorMessage = '';
      this.errorCorrelationId = '';

      try {
        const { data, response } = await api.post('/api/v1/tasks', {
          title: title.trim(),
          priority: 'Medium',
        });

        const uiTask = mapDtoToUi(data);
        this.tasks.unshift(uiTask);

        const nextEtag = data?.eTag || api.readETag(response);
        if (nextEtag && data?.id) {
          cache.setETag(String(data.id), nextEtag);
        }

        this.hasLoadedOnce = true;

        return uiTask;
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to create task.';
        try { this.errorCorrelationId = api.correlationId || ''; } catch {}
        throw error;
      }
    },

    /**
     * Full create for the TaskNew view.
     * payload shape: { title, description?, priority, dueDate?, tags?[] }
     */
    async createTaskFull(payload) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      const trimmedTitle = String(payload?.title ?? '').trim();
      if (!trimmedTitle) {
        throw new Error('Title is required.');
      }

      this.errorMessage = '';
      this.errorCorrelationId = '';

      const body = {
        title: trimmedTitle,
        description: payload?.description ? String(payload.description).trim() : undefined,
        priority: payload?.priority || 'Medium',
        dueDate: payload?.dueDate || null, // yyyy-MM-dd or null
        // selectedTags in the form already guarantees strings; keep it explicit for clarity.
        tags: Array.isArray(payload?.tags) ? payload.tags : [],
      };

      try {
        const { data, response } = await api.post('/api/v1/tasks', body);

        const uiTask = mapDtoToUi(data);
        this.tasks.unshift(uiTask);

        const nextEtag = data?.eTag || api.readETag(response);
        if (nextEtag && data?.id) {
          cache.setETag(String(data.id), nextEtag);
        }

        this.hasLoadedOnce = true;

        return uiTask;
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to create task.';
        try { this.errorCorrelationId = api.correlationId || ''; } catch {}
        throw error;
      }
    },

    async toggleTask(taskId) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      const task = this.tasks.find(t => String(t.id) === String(taskId));
      if (!task) return;

      const desiredCompleted = !task.isCompleted;
      const previous = task.isCompleted;
      task.isCompleted = desiredCompleted;

      try {
        const currentEtag = cache.getETag(String(taskId));
        const headers = api.withIfMatch({}, currentEtag);
        const payload = { status: statusFor(desiredCompleted) };

        const { data, response } = await api.patch(
          `/api/v1/tasks/${encodeURIComponent(taskId)}`,
          payload,
          { headers }
        );

        const updated = mapDtoToUi(data);
        const index = this.tasks.findIndex(t => String(t.id) === String(taskId));
        if (index >= 0) this.tasks[index] = updated;

        const nextEtag = data?.eTag || api.readETag(response);
        if (nextEtag) cache.setETag(String(taskId), nextEtag);

        this.hasLoadedOnce = true;
      } catch (error) {
        task.isCompleted = previous;
        this.errorMessage = error?.message ?? 'Failed to update task.';
        try { this.errorCorrelationId = api.correlationId || ''; } catch {}
        throw error;
      }
    },

    /**
     * Update the status of a task (inline control, all states).
     * - Optimistic UI
     * - Uses If-Match with cached ETag
     * - On 412, revert and refresh current page to resolve conflicts
     */
    async updateTaskStatus(taskId, nextStatus) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      const stringId = String(taskId);
      const index = this.tasks.findIndex(t => String(t.id) === stringId);
      if (index < 0) return null;

      const normalizedStatus = String(nextStatus);
      if (!TASK_STATUSES.includes(normalizedStatus)) {
        throw new Error('Invalid status value.');
      }

      const previousTask = this.tasks[index];
      const previousStatus = previousTask.status;
      const previousIsCompleted = previousTask.isCompleted;

      // Optimistic update
      const optimisticIsCompleted = normalizedStatus.toLowerCase() === 'done';
      this.tasks.splice(index, 1, {
        ...previousTask,
        status: normalizedStatus,
        isCompleted: optimisticIsCompleted,
      });

      try {
        const currentEtag = cache.getETag(stringId);
        const headers = api.withIfMatch({}, currentEtag);

        const { data, response } = await api.patch(
          `/api/v1/tasks/${encodeURIComponent(stringId)}`,
          { status: normalizedStatus },
          { headers }
        );

        const updated = mapDtoToUi(data);
        this.tasks.splice(index, 1, updated);

        const nextEtag = data?.eTag || api.readETag(response);
        if (nextEtag) cache.setETag(stringId, nextEtag);

        this.hasLoadedOnce = true;
        return updated;
      } catch (error) {
        // Revert on failure
        this.tasks.splice(index, 1, {
          ...previousTask,
          status: previousStatus,
          isCompleted: previousIsCompleted,
        });
        this.errorMessage = error?.message ?? 'Failed to update status.';
        try { this.errorCorrelationId = api.correlationId || ''; } catch {}

        // If concurrency error, try to refresh the current page to show latest data
        if (error?.status === 412) {
          try {
            const params = this.lastRequestParams || {
              pageNumber: this.pageNumber,
              pageSize: this.pageSize,
              sortBy: this.sortBy,
              sortDirection: this.sortDirection,
            };
            await this.fetchTasks(params);
          } catch {}
        }

        throw error;
      }
    },
  },
});
