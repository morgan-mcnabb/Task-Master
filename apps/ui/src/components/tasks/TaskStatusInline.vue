<script setup>
import { ref, watch, computed } from 'vue';
import { TASK_STATUSES } from '@/constants/tasks';
import { useTasksStore } from '@/stores/tasksStore';
import { getStatusDotClasses, getStatusSelectAccentClasses } from '@/utils/statusStyles';

const props = defineProps({
  taskId: {
    type: [String, Number],
    required: true,
  },
  status: {
    type: String,
    required: true,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
});

const tasksStore = useTasksStore();

const selectedStatus = ref(String(props.status));
const isSaving = ref(false);
const errorMessage = ref('');

watch(
  () => props.status,
  (current) => {
    selectedStatus.value = String(current);
  }
);

const isDisabled = computed(() => props.disabled || isSaving.value);
const selectAccentClasses = computed(() => getStatusSelectAccentClasses(selectedStatus.value));

async function onChange() {
  const next = String(selectedStatus.value);
  if (next === props.status) return;

  isSaving.value = true;
  errorMessage.value = '';

  try {
    await tasksStore.updateTaskStatus(props.taskId, next);
  } catch (error) {
    selectedStatus.value = String(props.status);
    errorMessage.value = error?.message ?? 'Failed to update status.';
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <div class="inline-flex items-center gap-2">
    <span
      aria-hidden="true"
      class="h-2.5 w-2.5 rounded-full"
      :class="getStatusDotClasses(selectedStatus)"
    ></span>

    <select
      v-model="selectedStatus"
      class="rounded-md border pl-2 pr-8 py-1 text-xs focus:outline-none focus:ring-1 disabled:opacity-50"
      :class="selectAccentClasses"
      :disabled="isDisabled"
      @change="onChange"
      aria-label="Change status"
    >
      <option v-for="statusOption in TASK_STATUSES" :key="statusOption" :value="statusOption">
        {{ statusOption }}
      </option>
    </select>

    <svg
      v-if="isSaving"
      class="h-4 w-4 animate-spin text-gray-600"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
    </svg>

    <span v-if="errorMessage" class="text-[11px] text-red-700">{{ errorMessage }}</span>
  </div>
</template>
