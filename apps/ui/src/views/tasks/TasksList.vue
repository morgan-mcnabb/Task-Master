<script setup>
import { onMounted, ref, computed } from 'vue';
import { useTasksStore } from '../../stores/tasksStore';
import LoadingSpinner from '../../components/ui/LoadingSpinner.vue';
import ErrorMessage from '../../components/ui/ErrorMessage.vue';

const tasksStore = useTasksStore();
const newTaskTitle = ref('');

const sortedTasks = computed(() =>
  [...tasksStore.tasks].sort((a, b) => Number(a.isCompleted) - Number(b.isCompleted))
);

async function handleCreateTask() {
  const title = newTaskTitle.value.trim();
  if (!title) return;
  await tasksStore.createTask(title);
  newTaskTitle.value = '';
}

onMounted(() => {
  if (!tasksStore.hasTasks) {
    tasksStore.fetchTasks();
  }
});
</script>

<template>
  <section class="mx-auto max-w-3xl p-6 space-y-8">
    <header class="space-y-2">
      <h1 class="text-3xl font-semibold tracking-tight">Tasks</h1>
      <p class="text-gray-600">Vue + Pinia + DI-powered API client</p>
    </header>

    <form @submit.prevent="handleCreateTask" class="flex items-center gap-3">
      <input
        v-model="newTaskTitle"
        type="text"
        placeholder="Add a new task…"
        class="flex-1 rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
      />
      <button
        type="submit"
        class="rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
        :disabled="!newTaskTitle.trim()"
      >
        Add
      </button>
    </form>

    <ErrorMessage v-if="tasksStore.errorMessage" :message="tasksStore.errorMessage" />

    <div v-if="tasksStore.isLoading" class="py-16">
      <LoadingSpinner label="Loading tasks..." />
    </div>

    <ul v-else class="divide-y divide-gray-200 rounded-md border border-gray-200 bg-white">
      <li
        v-for="task in sortedTasks"
        :key="task.id"
        class="flex items-center justify-between gap-4 px-4 py-3"
      >
        <label class="flex items-center gap-3">
          <input
            type="checkbox"
            class="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-600"
            :checked="task.isCompleted"
            @change="tasksStore.toggleTask(task.id)"
          />
          <span :class="task.isCompleted ? 'line-through text-gray-400' : ''">
            {{ task.title }}
          </span>
        </label>
      </li>

      <li v-if="!tasksStore.hasTasks" class="p-6 text-center text-gray-500">
        No tasks yet. Add your first one above.
      </li>
    </ul>
  </section>
</template>
