<script setup>
import { ref, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useTasksStore } from '@/stores/tasksStore';

import Toast from '@/components/ui/Toast.vue';
import TagPicker from '@/components/tags/TagPicker.vue';
import TaskStatusInline from '@/components/tasks/TaskStatusInline.vue';

import { debounce } from '@/utils/debounce';
import { makeAbortable } from '@/utils/makeAbortable';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  TASK_SORT_OPTIONS,
  SORT_DIRECTIONS,
  PAGE_SIZE_OPTIONS,
  DEFAULT_PAGINATION,
} from '@/constants/tasks';
import {
  parseRouteQueryToFilters,
  buildRouterQueryFromFilters,
  buildFetchParamsFromFilters,
} from '@/utils/taskQuery';
import { ROUTE_NAMES } from '@/constants/routeNames';

const tasksStore = useTasksStore();

const route = useRoute();
const router = useRouter();
const filters = ref(parseRouteQueryToFilters(route.query));

function normalizeQueryObject(obj) {
  const clone = JSON.parse(JSON.stringify(obj ?? {}));
  if (Array.isArray(clone.statuses)) clone.statuses = [...clone.statuses].sort();
  if (Array.isArray(clone.priorities)) clone.priorities = [...clone.priorities].sort();
  if (Array.isArray(clone.tags)) clone.tags = [...clone.tags].sort();
  return clone;
}
function areQueriesEqual(a, b) {
  return JSON.stringify(normalizeQueryObject(a)) === JSON.stringify(normalizeQueryObject(b));
}

async function navigateWithFilters({ usePush = false } = {}) {
  if (filters.value.pageNumber < 1) filters.value.pageNumber = 1;

  const nextQuery = buildRouterQueryFromFilters(filters.value);
  const currentQuery = route.query;

  if (areQueriesEqual(nextQuery, currentQuery)) {
    return;
  }
  return usePush ? router.push({ query: nextQuery }) : router.replace({ query: nextQuery });
}

function resetPageAndNavigate(options = { usePush: false }) {
  filters.value.pageNumber = DEFAULT_PAGINATION.pageNumber;
  return navigateWithFilters(options);
}

function clearFilterArray(key) {
  if (!filters.value[key]) return;
  filters.value[key] = [];
  return resetPageAndNavigate();
}

const fetchRunner = makeAbortable(async ({ signal }, currentFilters) => {
  const params = buildFetchParamsFromFilters(currentFilters);
  await tasksStore.fetchTasks(params, { signal });
});

watch(
  () => route.query,
  (query) => {
    filters.value = parseRouteQueryToFilters(query);
    fetchRunner.run(filters.value);
  },
  { immediate: true }
);

const sortFieldOptions = TASK_SORT_OPTIONS;
const sortDirections = SORT_DIRECTIONS;
const pageSizeOptions = PAGE_SIZE_OPTIONS;

function onStatusesChange() { return resetPageAndNavigate(); }
function onPrioritiesChange() { return resetPageAndNavigate(); }
function onDateRangeChange() { return resetPageAndNavigate(); }
function onSortChange() { return resetPageAndNavigate(); }
function onPageSizeChange() { return resetPageAndNavigate(); }

function clearStatuses() { return clearFilterArray('statuses'); }
function clearPriorities() { return clearFilterArray('priorities'); }

watch(
  () => filters.value.tags,
  () => resetPageAndNavigate(),
  { deep: true }
);

const debouncedSearchRouteUpdate = debounce(() => resetPageAndNavigate(), 350);
function onSearchInput(event) {
  filters.value.search = event.target.value;
  debouncedSearchRouteUpdate();
}

function goToPage(targetPage) {
  if (targetPage < 1 || targetPage > tasksStore.totalPages) return;
  filters.value.pageNumber = targetPage;
  return navigateWithFilters({ usePush: true });
}
function goPrevPage() { return goToPage(tasksStore.pageNumber - 1); }
function goNextPage() { return goToPage(tasksStore.pageNumber + 1); }

const isInitialLoading = computed(() => tasksStore.isLoading && !tasksStore.hasLoadedOnce);
const isRefreshing = computed(() => tasksStore.isLoading && tasksStore.hasLoadedOnce);
</script>

<template>
  <section class="mx-auto max-w-6xl p-6 space-y-6">
    <header class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div class="space-y-2">
        <h1 class="text-3xl font-semibold tracking-tight">Tasks</h1>
        <p class="text-gray-600">Filter, search, sort, and page through your tasks.</p>
      </div>
      <router-link
        :to="{ name: ROUTE_NAMES.TASKS_NEW }"
        class="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700"
      >
        New Task
      </router-link>
    </header>

    <div class="grid gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm md:grid-cols-3 lg:grid-cols-4">
      <div class="md:col-span-1 lg:col-span-2">
        <label class="block text-sm font-medium text-gray-700">Search</label>
        <input
          :value="filters.search"
          @input="onSearchInput"
          type="text"
          placeholder="Search title, tags…"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>

      <div class="md:col-span-1">
        <div class="flex items-center justify-between">
          <label class="block text-sm font-medium text-gray-700">Statuses</label>
          <button
            type="button"
            class="inline-flex items-center gap-1 rounded-md border border-gray-300 px-2 py-1 text-xs text-gray-700 hover:bg-gray-50"
            @click="clearStatuses"
            aria-label="Clear statuses"
            title="Clear statuses"
          >
            ✕ <span>Clear</span>
          </button>
        </div>
        <div class="mt-2 space-y-2">
          <label
            v-for="status in TASK_STATUSES"
            :key="status"
            class="flex items-center gap-2 text-sm text-gray-700"
          >
            <input
              type="checkbox"
              class="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
              v-model="filters.statuses"
              :value="status"
              @change="onStatusesChange"
            />
            <span>{{ status }}</span>
          </label>
        </div>
        <div class="mt-1 text-xs text-gray-500">Choose any; empty means all.</div>
      </div>

      <div class="md:col-span-1">
        <div class="flex items-center justify-between">
          <label class="block text-sm font-medium text-gray-700">Priorities</label>
          <button
            type="button"
            class="inline-flex items-center gap-1 rounded-md border border-gray-300 px-2 py-1 text-xs text-gray-700 hover:bg-gray-50"
            @click="clearPriorities"
            aria-label="Clear priorities"
            title="Clear priorities"
          >
            ✕ <span>Clear</span>
          </button>
        </div>
        <div class="mt-2 space-y-2">
          <label
            v-for="priority in TASK_PRIORITIES"
            :key="priority"
            class="flex items-center gap-2 text-sm text-gray-700"
          >
            <input
              type="checkbox"
              class="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
              v-model="filters.priorities"
              :value="priority"
              @change="onPrioritiesChange"
            />
            <span>{{ priority }}</span>
          </label>
        </div>
        <div class="mt-1 text-xs text-gray-500">Choose any; empty means all.</div>
      </div>

      <div class="md:col-span-1">
        <label class="block text-sm font-medium text-gray-700">Due on or after</label>
        <input
          v-model="filters.dueOnOrAfter"
          type="date"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          @change="onDateRangeChange"
        />
      </div>
      <div class="md:col-span-1">
        <label class="block text-sm font-medium text-gray-700">Due on or before</label>
        <input
          v-model="filters.dueOnOrBefore"
          type="date"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          @change="onDateRangeChange"
        />
      </div>

      <div class="md:col-span-3 lg:col-span-4">
        <label class="block text-sm font-medium text-gray-700">Tags</label>
        <div class="mt-1">
          <TagPicker v-model="filters.tags" :limit="10" :show-clear-button="true" clear-button-label="Clear" />
        </div>
      </div>

      <div class="md:col-span-1">
        <label class="block text-sm font-medium text-gray-700">Sort by</label>
        <select
          v-model="filters.sortBy"
          class="mt-1 w-full rounded-md border border-gray-300 pl-3 pr-8 py-2 text-sm"
          @change="onSortChange"
        >
          <option
            v-for="option in sortFieldOptions"
            :key="option.value"
            :value="option.value"
          >
            {{ option.label }}
          </option>
        </select>
      </div>
      <div class="md:col-span-1">
        <label class="block text-sm font-medium text-gray-700">Direction</label>
        <select
          v-model="filters.sortDirection"
          class="mt-1 w-full rounded-md border border-gray-300 pl-3 pr-8 py-2 text-sm"
          @change="onSortChange"
        >
          <option v-for="direction in sortDirections" :key="direction" :value="direction">{{ direction }}</option>
        </select>
      </div>
      <div class="md:col-span-1">
        <label class="block text-sm font-medium text-gray-700">Page size</label>
        <select
          v-model.number="filters.pageSize"
          class="mt-1 w-full rounded-md border border-gray-300 pl-3 pr-8 py-2 text-sm"
          @change="onPageSizeChange"
        >
          <option v-for="size in pageSizeOptions" :key="size" :value="size">{{ size }}</option>
        </select>
      </div>
    </div>

    <Toast
      :show="Boolean(tasksStore.errorMessage)"
      title="Could not load tasks"
      :message="tasksStore.errorMessage"
      :correlation-id="tasksStore.errorCorrelationId"
      variant="error"
      @close="tasksStore.errorMessage = ''"
    />

    <div v-if="isInitialLoading" class="space-y-3 rounded-lg border border-gray-200 bg-white p-4">
      <div class="h-4 w-1/3 animate-pulse rounded bg-gray-200"></div>
      <div class="space-y-2">
        <div v-for="skeletonIndex in 6" :key="skeletonIndex" class="h-10 animate-pulse rounded bg-gray-100"></div>
      </div>
    </div>

    <template v-else>
      <div v-if="isRefreshing" class="h-1 w-full animate-pulse rounded bg-indigo-200"></div>

      <div
        v-if="!tasksStore.hasTasks && !tasksStore.errorMessage"
        class="rounded-lg border border-gray-200 bg-white p-8 text-center text-gray-600"
      >
        <div
          v-if="
            filters.search ||
            filters.statuses.length ||
            filters.priorities.length ||
            filters.tags.length ||
            filters.dueOnOrAfter ||
            filters.dueOnOrBefore
          "
        >
          No results match your filters.
        </div>
        <div v-else>
          No tasks yet.
          <router-link
            :to="{ name: ROUTE_NAMES.TASKS_NEW }"
            class="text-indigo-700 underline"
          >
            Create one
          </router-link>
          or remove filters.
        </div>
      </div>

      <div
        v-else
        class="overflow-hidden rounded-lg border border-gray-200 bg-white"
        :aria-busy="isRefreshing ? 'true' : 'false'"
      >
        <div class="hidden grid-cols-12 gap-2 border-b bg-gray-50 px-4 py-2 text-sm font-medium text-gray-700 md:grid">
          <div class="col-span-5">Title</div>
          <div class="col-span-2">Priority</div>
          <div class="col-span-2">Status</div>
          <div class="col-span-2">Due</div>
          <div class="col-span-1">Tags</div>
        </div>

        <ul class="divide-y">
          <li
            v-for="task in tasksStore.tasks"
            :key="task.id"
            class="grid grid-cols-1 gap-2 px-4 py-3 md:grid-cols-12"
          >
            <div class="col-span-5">
              <router-link
                :to="`/tasks/${encodeURIComponent(task.id)}`"
                class="font-medium text-indigo-700 hover:underline"
              >
                {{ task.title }}
              </router-link>
            </div>
            <div class="col-span-2">
              <span class="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                {{ task.priority || '—' }}
              </span>
            </div>
            <div class="col-span-2">
              <TaskStatusInline :task-id="task.id" :status="task.status" />
            </div>
            <div class="col-span-2 text-sm text-gray-700">
              {{ task.dueDate ? new Date(task.dueDate).toLocaleDateString() : '—' }}
            </div>
            <div class="col-span-1 flex flex-wrap items-center gap-1">
              <span
                v-for="tag in task.tags"
                :key="tag"
                class="rounded bg-indigo-50 px-1.5 py-0.5 text-[11px] text-indigo-700"
              >
                {{ tag }}
              </span>
            </div>
          </li>
        </ul>
      </div>

      <div class="mt-4 flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div class="text-sm text-gray-600">
          <span class="font-medium">{{ tasksStore.totalCount }}</span> results
        </div>

        <div class="flex items-center gap-3">
          <button
            class="rounded-md border border-gray-300 px-2 py-1 text-sm disabled:opacity-50"
            :disabled="tasksStore.pageNumber <= 1"
            @click="goPrevPage"
          >
            Prev
          </button>

          <span class="px-2 text-sm text-gray-700">
            Page <strong>{{ tasksStore.pageNumber }}</strong> of <strong>{{ tasksStore.totalPages }}</strong>
          </span>

          <button
            class="rounded-md border border-gray-300 px-2 py-1 text-sm disabled:opacity-50"
            :disabled="tasksStore.pageNumber >= tasksStore.totalPages"
            @click="goNextPage"
          >
            Next
          </button>
        </div>
      </div>
    </template>
  </section>
</template>
