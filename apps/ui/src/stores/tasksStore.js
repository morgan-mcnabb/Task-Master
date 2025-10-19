import { defineStore } from 'pinia';
import { useTaskCacheStore } from './taskCacheStore';
import { resolveApiFromStore } from './_api';

function mapDtoToUi(dto) {
  return {
    id: dto.id,
    title: dto.title,
    // "Done" means completed
    isCompleted: String(dto.status).toLowerCase() === 'done',
    _raw: dto, // keep raw if needed elsewhere
  };
}

function statusFor(isCompleted) {
  return isCompleted ? 'Done' : 'Todo';
}

export const useTasksStore = defineStore('tasks', {
  state: () => ({
    tasks: /** @type {Array<{id:string,title:string,isCompleted:boolean,_raw?:any}>} */ ([]),
    isLoading: false,
    errorMessage: '',
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
    async fetchTasks() {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      this.isLoading = true;
      this.errorMessage = '';
      try {
        // Server returns PagedResponse<TaskDto>
        const { data } = await api.get('/api/v1/tasks');
        const items = Array.isArray(data?.items) ? data.items : [];

        // Map to UI and cache ETags per item (from body dto.ETag)
        this.tasks = items.map(mapDtoToUi);
        for (const dto of items) {
          if (dto?.id && dto?.eTag) {
            cache.setETag(String(dto.id), dto.eTag);
          }
        }
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to load tasks.';
      } finally {
        this.isLoading = false;
      }
    },

    async createTask(title) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      if (!title || !title.trim()) return;
      this.errorMessage = '';

      try {
        const { data } = await api.post('/api/v1/tasks', {
          title: title.trim(),
          priority: 'Medium',
        });

        const uiTask = mapDtoToUi(data);
        this.tasks.unshift(uiTask);

        // Prefer ETag from body, then from header if present
        if (data?.eTag && data?.id) {
          cache.setETag(String(data.id), data.eTag);
        }
      } catch (error) {
        this.errorMessage = error?.message ?? 'Failed to create task.';
        throw error;
      }
    },

    async toggleTask(taskId) {
      const api = resolveApiFromStore(this);
      const cache = useTaskCacheStore();

      const task = this.tasks.find(t => String(t.id) === String(taskId));
      if (!task) return;

      const desiredCompleted = !task.isCompleted;

      // Optimistic update
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

        // Sync UI with authoritative response
        const updated = mapDtoToUi(data);
        const index = this.tasks.findIndex(t => String(t.id) === String(taskId));
        if (index >= 0) this.tasks[index] = updated;

        // Update ETag cache
        const nextEtag = data?.eTag || api.readETag(response);
        if (nextEtag) cache.setETag(String(taskId), nextEtag);
      } catch (error) {
        // Revert
        task.isCompleted = previous;
        this.errorMessage = error?.message ?? 'Failed to update task.';
        throw error;
      }
    },
  },
});
