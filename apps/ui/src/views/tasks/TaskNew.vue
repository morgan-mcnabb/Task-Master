<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';

import Toast from '@/components/ui/Toast.vue';
import TagPicker from '@/components/tags/TagPicker.vue';
import { TASK_PRIORITIES } from '@/constants/tasks';
import { useTasksStore } from '@/stores/tasksStore';
import { ROUTE_NAMES } from '@/constants/routeNames';

const router = useRouter();
const tasksStore = useTasksStore();

/** ---------- Form state ---------- */
const titleInput = ref('');
const descriptionInput = ref('');
const priorityInput = ref(TASK_PRIORITIES.includes('Medium') ? 'Medium' : TASK_PRIORITIES[0]);
const dueDateInput = ref(''); // yyyy-MM-dd from <input type="date">
const selectedTags = ref([]); // array<string>

/** ---------- Validation ---------- */
const MAX_TITLE = 120;
const MAX_DESCRIPTION = 2000;

const fieldErrors = ref({
  title: '',
  description: '',
  tags: '',
});

function validateTagsUniqueness(tags) {
  const lowered = tags.map(t => t.toLowerCase());
  const uniqueCount = new Set(lowered).size;
  return uniqueCount === lowered.length;
}

function runValidation() {
  fieldErrors.value = { title: '', description: '', tags: '' };

  const trimmedTitle = titleInput.value.trim();
  if (!trimmedTitle) {
    fieldErrors.value.title = 'Title is required.';
  } else if (trimmedTitle.length > MAX_TITLE) {
    fieldErrors.value.title = `Title must be at most ${MAX_TITLE} characters.`;
  }

  const trimmedDescription = descriptionInput.value.trim();
  if (trimmedDescription && trimmedDescription.length > MAX_DESCRIPTION) {
    fieldErrors.value.description = `Description must be at most ${MAX_DESCRIPTION} characters.`;
  }

  if (!validateTagsUniqueness(selectedTags.value)) {
    fieldErrors.value.tags = 'Tags must be unique (case-insensitive).';
  }

  return Object.values(fieldErrors.value).every(message => !message);
}

/** ---------- Submit ---------- */
const isSubmitting = ref(false);
const errorToast = ref({ show: false, message: '', correlationId: '' });

async function handleSubmit() {
  titleInput.value = titleInput.value.trim();
  descriptionInput.value = descriptionInput.value.trim();

  // Defensive dedupe (TagPicker already prevents duplicates)
  const seen = new Set();
  const uniqueTags = [];
  for (const tag of selectedTags.value) {
    const lowered = tag.toLowerCase();
    if (seen.has(lowered)) continue;
    seen.add(lowered);
    uniqueTags.push(tag);
  }
  selectedTags.value = uniqueTags;

  const isValid = runValidation();
  if (!isValid) return;

  isSubmitting.value = true;
  errorToast.value = { show: false, message: '', correlationId: '' };

  const payload = {
    title: titleInput.value,
    description: descriptionInput.value || undefined,
    priority: priorityInput.value,
    dueDate: dueDateInput.value || null,
    // Free-text tags are allowed; server will ensure/create them.
    tags: selectedTags.value,
  };

  try {
    await tasksStore.createTaskFull(payload);
    await router.replace({ name: ROUTE_NAMES.TASKS });
  } catch (err) {
    const correlationId = tasksStore.errorCorrelationId || '';
    errorToast.value = {
      show: true,
      message: err?.message ?? 'Failed to create task.',
      correlationId,
    };
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <section class="mx-auto max-w-3xl p-6 space-y-6">
    <header class="space-y-1">
      <h1 class="text-2xl font-semibold">New Task</h1>
      <p class="text-gray-600">Create a task with details and tags.</p>
    </header>

    <Toast
      :show="errorToast.show"
      title="Could not create task"
      :message="errorToast.message"
      :correlation-id="errorToast.correlationId"
      variant="error"
      @close="errorToast.show = false"
    />

    <form @submit.prevent="handleSubmit" class="space-y-6 rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
      <div>
        <label class="block text-sm font-medium text-gray-700">Title <span class="text-red-600">*</span></label>
        <input
          v-model="titleInput"
          type="text"
          maxlength="120"
          placeholder="Short, clear title"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
        <p v-if="fieldErrors.title" class="mt-1 text-sm text-red-600">{{ fieldErrors.title }}</p>
        <p class="mt-1 text-xs text-gray-500">Max {{ 120 }} characters.</p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Description</label>
        <textarea
          v-model="descriptionInput"
          rows="4"
          maxlength="2000"
          placeholder="Optional details"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
        <p v-if="fieldErrors.description" class="mt-1 text-sm text-red-600">{{ fieldErrors.description }}</p>
        <p class="mt-1 text-xs text-gray-500">Max {{ 2000 }} characters.</p>
      </div>

      <div class="grid gap-4 md:grid-cols-2">
        <div>
          <label class="block text-sm font-medium text-gray-700">Priority</label>
          <select
            v-model="priorityInput"
            class="mt-1 w-full rounded-md border border-gray-300 pl-3 pr-8 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
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
          />
        </div>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Tags</label>
        <TagPicker v-model="selectedTags" :limit="10" />
        <p v-if="fieldErrors.tags" class="mt-1 text-sm text-red-600">{{ fieldErrors.tags }}</p>
      </div>

      <div class="flex items-center gap-3">
        <button
          type="submit"
          :disabled="isSubmitting"
          class="rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {{ isSubmitting ? 'Creating…' : 'Create task' }}
        </button>

        <router-link
          :to="{ name: ROUTE_NAMES.TASKS }"
          class="text-sm text-gray-700 underline"
        >
          Cancel
        </router-link>
      </div>
    </form>
  </section>
</template>
