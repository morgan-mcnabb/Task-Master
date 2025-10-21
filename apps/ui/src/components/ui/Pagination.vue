<script setup>
const props = defineProps({
  pageNumber: { type: Number, required: true },
  pageSize: { type: Number, required: true },
  totalPages: { type: Number, required: true },
  totalCount: { type: Number, required: true },
  pageSizes: { type: Array, default: () => [10, 20, 50, 100] },
});

const emit = defineEmits(['update:pageNumber', 'update:pageSize']);

function goTo(target) {
  if (target < 1 || target > props.totalPages) return;
  emit('update:pageNumber', target);
}
</script>

<template>
  <div class="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
    <div class="text-sm text-gray-600">
      <span class="font-medium">{{ totalCount }}</span> results
    </div>

    <div class="flex items-center gap-3">
      <label class="text-sm text-gray-700">
        Page size:
        <select
          class="ml-2 rounded-md border border-gray-300 px-2 py-1 text-sm"
          :value="pageSize"
          @change="emit('update:pageSize', Number($event.target.value))"
        >
          <option v-for="size in pageSizes" :key="size" :value="size">{{ size }}</option>
        </select>
      </label>

      <nav class="flex items-center gap-1" aria-label="Pagination">
        <button
          class="rounded-md border border-gray-300 px-2 py-1 text-sm disabled:opacity-50"
          :disabled="pageNumber <= 1"
          @click="goTo(pageNumber - 1)"
        >
          Prev
        </button>

        <span class="px-2 text-sm text-gray-700">
          Page <strong>{{ pageNumber }}</strong> of <strong>{{ totalPages }}</strong>
        </span>

        <button
          class="rounded-md border border-gray-300 px-2 py-1 text-sm disabled:opacity-50"
          :disabled="pageNumber >= totalPages"
          @click="goTo(pageNumber + 1)"
        >
          Next
        </button>
      </nav>
    </div>
  </div>
</template>
