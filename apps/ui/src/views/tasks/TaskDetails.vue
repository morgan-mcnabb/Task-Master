<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import Toast from '@/components/ui/Toast.vue';
import TagPicker from '@/components/tags/TagPicker.vue';
import { TASK_PRIORITIES, TASK_STATUSES } from '@/constants/tasks';
import { useApi } from '@/useApi';
import { useTaskCacheStore } from '@/stores/taskCacheStore';
import { normalizeTagsToStrings, dedupeCaseInsensitive } from '@/utils/tags';
import { ROUTE_NAMES } from '@/constants/routeNames';
import { useTasksStore } from '@/stores/tasksStore';
import { getStatusBadgeClasses, getStatusButtonClasses } from '@/utils/statusStyles';

const route = useRoute();
const router = useRouter();
const api = useApi();
const cache = useTaskCacheStore();
const tasksStore = useTasksStore();

const taskId = String(route.params.id || '').trim();

/** ---------- Page state ---------- */
const isLoading = ref(false);
const isSaving = ref(false);
const isDeleting = ref(false);
const loadErrorMessage = ref('');

/** ---------- Form state ---------- */
const titleInput = ref('');
const descriptionInput = ref('');
const priorityInput = ref(TASK_PRIORITIES.includes('Medium') ? 'Medium' : TASK_PRIORITIES[0]);
const dueDateInput = ref(''); // yyyy-MM-dd
const selectedTags = ref([]); // string[]

/**
 * We keep two separate status refs to avoid confusing the user:
 * - savedStatus   → reflects the last persisted value (what header displays)
 * - draftStatus   → what the user is currently selecting (only saved on submit)
 */
const savedStatus = ref('Todo');
const draftStatus = ref('Todo');

/** Snapshot to compute dirtiness and to support Reset */
let snapshot = {
  title: '',
  description: '',
  priority: priorityInput.value,
  dueDate: '',
  tags: [],
  status: 'Todo', // snapshot of the last persisted status
};

/** ---------- Toasts ---------- */
const successToast = ref({ show: false, message: '' });
const errorToast = ref({ show: false, message: '', correlationId: '' });

function showSuccess(message) {
  successToast.value = { show: true, message };
}
function showError(message) {
  const correlationId = api.correlationId || '';
  errorToast.value = { show: true, message, correlationId };
}

/** ---------- Helpers ---------- */
function takeSnapshot() {
  snapshot = {
    title: titleInput.value,
    description: descriptionInput.value,
    priority: priorityInput.value,
    dueDate: dueDateInput.value || '',
    tags: [...selectedTags.value],
    status: savedStatus.value, // snapshot tracks the last persisted status
  };
}

function applyDtoToForm(dto) {
  titleInput.value = String(dto?.title || '');
  descriptionInput.value = String(dto?.description || '');
  priorityInput.value = String(dto?.priority || (TASK_PRIORITIES.includes('Medium') ? 'Medium' : TASK_PRIORITIES[0]));
  dueDateInput.value = dto?.dueDate ? String(dto.dueDate) : '';
  selectedTags.value = dedupeCaseInsensitive(normalizeTagsToStrings(dto?.tags || []));
  savedStatus.value = String(dto?.status || 'Todo');
  draftStatus.value = savedStatus.value;
  takeSnapshot();
}

const isDirty = computed(() => {
  const now = {
    title: titleInput.value,
    description: descriptionInput.value,
    priority: priorityInput.value,
    dueDate: dueDateInput.value || '',
    tags: [...selectedTags.value],
    status: draftStatus.value, // compare the *draft* against the persisted snapshot
  };
  return JSON.stringify(now) !== JSON.stringify(snapshot);
});

function currentIfMatchHeaders() {
  const etag = cache.getETag(taskId);
  return api.withIfMatch({}, etag);
}

async function handleConcurrencyAndReload(contextMessage) {
  try {
    await loadTask();
    showError(`${contextMessage} The task was updated elsewhere; the latest version has been loaded. Re-apply your changes and try again.`);
  } catch {
    showError(`${contextMessage} Additionally, failed to reload the latest task state.`);
  }
}

/** ---------- Load ---------- */
async function loadTask() {
  if (!taskId) {
    loadErrorMessage.value = 'Invalid task id.';
    return;
  }
  isLoading.value = true;
  loadErrorMessage.value = '';
  try {
    const { data, response } = await api.get(`/api/v1/tasks/${encodeURIComponent(taskId)}`);
    applyDtoToForm(data);

    const nextEtag = data?.eTag || api.readETag(response);
    if (nextEtag) cache.setETag(taskId, nextEtag);
  } catch (error) {
    loadErrorMessage.value = error?.message || 'Failed to load task.';
  } finally {
    isLoading.value = false;
  }
}

/** ---------- Save (PUT) ---------- */
const MAX_TITLE = 120;
const MAX_DESCRIPTION = 2000;

const fieldErrors = ref({ title: '', description: '', tags: '' });

function runValidation() {
  fieldErrors.value = { title: '', description: '', tags: '' };

  const trimmedTitle = titleInput.value.trim();
  if (!trimmedTitle) {
    fieldErrors.value.title = 'Title is required.';
  } else if (trimmedTitle.length > MAX_TITLE) {
    fieldErrors.value.title = `Title must be at most ${MAX_TITLE} characters.`;
  }

  const trimmedDescription = (descriptionInput.value || '').trim();
  if (trimmedDescription.length > MAX_DESCRIPTION) {
    fieldErrors.value.description = `Description must be at most ${MAX_DESCRIPTION} characters.`;
  }

  if (selectedTags.value.length !== dedupeCaseInsensitive(selectedTags.value).length) {
    fieldErrors.value.tags = 'Tags must be unique (case-insensitive).';
  }

  return Object.values(fieldErrors.value).every(m => !m);
}

async function handleSave() {
  titleInput.value = titleInput.value.trim();
  descriptionInput.value = descriptionInput.value.trim();

  const uniqueTags = dedupeCaseInsensitive(selectedTags.value);
  selectedTags.value = uniqueTags;

  if (!runValidation()) return;

  isSaving.value = true;
  errorToast.value = { show: false, message: '', correlationId: '' };

  const payload = {
    id: taskId,
    title: titleInput.value,
    description: descriptionInput.value || undefined,
    priority: priorityInput.value,
    status: draftStatus.value, // save the draft
    dueDate: dueDateInput.value || null,
    tags: selectedTags.value,
  };

  try {
    const headers = currentIfMatchHeaders();
    const { data, response } = await api.put(`/api/v1/tasks/${encodeURIComponent(taskId)}`, payload, { headers });

    applyDtoToForm(data); // updates savedStatus + draftStatus and snapshot
    const nextEtag = data?.eTag || api.readETag(response);
    if (nextEtag) cache.setETag(taskId, nextEtag);

    // Soft-update list item if present
    try {
      const index = tasksStore.tasks.findIndex(t => String(t.id) === taskId);
      if (index >= 0) {
        const updated = { ...tasksStore.tasks[index] };
        updated.title = data?.title ?? updated.title;
        updated.priority = data?.priority ?? updated.priority;
        updated.status = data?.status ?? updated.status;
        updated.isCompleted = String(updated.status).toLowerCase() === 'done';
        updated.dueDate = data?.dueDate ?? updated.dueDate ?? null;
        updated.tags = normalizeTagsToStrings(data?.tags ?? updated.tags ?? []);
        tasksStore.tasks.splice(index, 1, updated);
      }
    } catch {}

    await router.replace({ name: ROUTE_NAMES.TASKS });
  } catch (error) {
    if (error?.status === 412) {
      await handleConcurrencyAndReload('Save was rejected due to an outdated version.');
      return;
    }
    showError(error?.message || 'Failed to save task.');
  } finally {
    isSaving.value = false;
  }
}

/** ---------- Status selection (local only; save to persist) ---------- */
function selectStatus(nextStatus) {
  if (!nextStatus || isSaving.value || isDeleting.value) return;
  draftStatus.value = nextStatus;
}

/** ---------- Delete with If-Match ---------- */
async function handleDelete() {
  const confirmed = window.confirm('Delete this task? This cannot be undone.');
  if (!confirmed) return;

  isDeleting.value = true;
  try {
    const headers = currentIfMatchHeaders();
    await api.delete(`/api/v1/tasks/${encodeURIComponent(taskId)}`, { headers });

    try { cache.clearETag(taskId); } catch {}
    try {
      const index = tasksStore.tasks.findIndex(t => String(t.id) === taskId);
      if (index >= 0) tasksStore.tasks.splice(index, 1);
    } catch {}

    router.replace({ name: ROUTE_NAMES.TASKS });
  } catch (error) {
    if (error?.status === 412) {
      await handleConcurrencyAndReload('Delete was rejected due to an outdated version.');
      return;
    }
    showError(error?.message || 'Failed to delete task.');
  } finally {
    isDeleting.value = false;
  }
}

/** ---------- Reset ---------- */
function handleReset() {
  titleInput.value = snapshot.title;
  descriptionInput.value = snapshot.description;
  priorityInput.value = snapshot.priority;
  dueDateInput.value = snapshot.dueDate;
  selectedTags.value = [...snapshot.tags];
  draftStatus.value = snapshot.status; // revert draft to the last persisted status
}

/** ---------- Status UI helpers ---------- */
function statusLabel(status) {
  return String(status);
}

/** ---------- Init ---------- */
onMounted(() => {
  loadTask();
});
</script>

<template>
  <section class="mx-auto max-w-3xl p-6 space-y-6">
    <header class="space-y-1">
      <h1 class="text-2xl font-semibold">Task Details</h1>
      <p class="text-gray-600">View and edit task details. Changes use ETags for concurrency safety.</p>
    </header>

    <Toast
      :show="successToast.show"
      title="Success"
      :message="successToast.message"
      variant="success"
      @close="successToast.show = false"
    />
    <Toast
      :show="errorToast.show"
      title="Something went wrong"
      :message="errorToast.message"
      :correlation-id="errorToast.correlationId"
      variant="error"
      @close="errorToast.show = false"
    />

    <div v-if="isLoading" class="rounded-lg border border-gray-200 bg-white p-5">
      <div class="h-4 w-1/3 animate-pulse rounded bg-gray-200"></div>
      <div class="mt-3 space-y-2">
        <div class="h-10 animate-pulse rounded bg-gray-100"></div>
        <div class="h-24 animate-pulse rounded bg-gray-100"></div>
        <div class="grid gap-3 md:grid-cols-2">
          <div class="h-10 animate-pulse rounded bg-gray-100"></div>
          <div class="h-10 animate-pulse rounded bg-gray-100"></div>
        </div>
        <div class="h-10 animate-pulse rounded bg-gray-100"></div>
      </div>
    </div>

    <div v-else-if="loadErrorMessage" class="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-red-700">
      {{ loadErrorMessage }}
      <div class="mt-3">
        <router-link :to="{ name: ROUTE_NAMES.TASKS }" class="text-sm underline">Back to list</router-link>
      </div>
    </div>

    <form
      v-else
      class="space-y-6 rounded-lg border border-gray-200 bg-white p-5 shadow-sm"
      @submit.prevent="handleSave"
    >
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div class="text-sm text-gray-600">
          Task ID: <code class="rounded bg-gray-50 px-1 py-0.5">{{ taskId }}</code>
        </div>

        <div class="inline-flex items-center gap-2">
          <span class="text-sm text-gray-600">Current status:</span>
          <span :class="['rounded-full px-2 py-0.5 text-xs', getStatusBadgeClasses(savedStatus)]">
            {{ savedStatus }}
          </span>
        </div>
      </div>

      <div class="space-y-2">
        <label class="block text-sm font-medium text-gray-700">Set status</label>
        <div
          class="flex flex-wrap gap-2"
          role="radiogroup"
          aria-label="Task status"
        >
          <button
            v-for="status in TASK_STATUSES"
            :key="status"
            type="button"
            :class="getStatusButtonClasses(status, draftStatus === status)"
            :aria-pressed="draftStatus === status ? 'true' : 'false'"
            :disabled="isSaving || isDeleting"
            @click="selectStatus(status)"
          >
            <span class="inline-flex items-center gap-2">
              <span>{{ statusLabel(status) }}</span>
              <span
                v-if="draftStatus === status"
                aria-hidden="true"
                class="text-xs opacity-80"
              >
                ✓
              </span>
            </span>
          </button>
        </div>
        <p class="text-xs text-gray-500">
          Selecting a status does not save immediately. Click <strong>Save changes</strong> to persist.
        </p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Title <span class="text-red-600">*</span></label>
        <input
          v-model="titleInput"
          type="text"
          maxlength="120"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          :disabled="isSaving || isDeleting"
        />
        <p v-if="fieldErrors.title" class="mt-1 text-sm text-red-600">{{ fieldErrors.title }}</p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Description</label>
        <textarea
          v-model="descriptionInput"
          rows="4"
          maxlength="2000"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          :disabled="isSaving || isDeleting"
        />
        <p v-if="fieldErrors.description" class="mt-1 text-sm text-red-600">{{ fieldErrors.description }}</p>
      </div>

      <div class="grid gap-4 md:grid-cols-2">
        <div>
          <label class="block text-sm font-medium text-gray-700">Priority</label>
          <select
            v-model="priorityInput"
            class="mt-1 w-full rounded-md border border-gray-300 pl-3 pr-8 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            :disabled="isSaving || isDeleting"
          >
            <option v-for="priority in TASK_PRIORITIES" :key="priority" :value="priority">{{ priority }}</option>
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Due date</label>
          <input
            v-model="dueDateInput"
            type="date"
            class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            :disabled="isSaving || isDeleting"
          />
        </div>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Tags</label>
        <TagPicker v-model="selectedTags" :limit="10" :disabled="isSaving || isDeleting" />
        <p v-if="fieldErrors.tags" class="mt-1 text-sm text-red-600">{{ fieldErrors.tags }}</p>
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <button
          type="submit"
          class="rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
          :disabled="isSaving || isDeleting || !isDirty"
        >
          {{ isSaving ? 'Saving…' : 'Save changes' }}
        </button>

        <button
          type="button"
          class="rounded-md border border-gray-300 px-4 py-2 hover:bg-gray-50 disabled:opacity-50"
          :disabled="isSaving || isDeleting || !isDirty"
          @click="handleReset"
        >
          Reset
        </button>

        <div class="flex-1"></div>

        <router-link
          :to="{ name: ROUTE_NAMES.TASKS }"
          class="text-sm text-gray-700 underline"
        >
          Back to list
        </router-link>

        <button
          type="button"
          class="rounded-md border border-red-300 px-4 py-2 text-red-700 hover:bg-red-50 disabled:opacity-50"
          :disabled="isSaving || isDeleting"
          @click="handleDelete"
        >
          {{ isDeleting ? 'Deleting…' : 'Delete' }}
        </button>
      </div>
    </form>
  </section>
</template>
