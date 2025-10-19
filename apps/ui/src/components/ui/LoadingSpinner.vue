<script setup>
defineProps({
  label: {
    type: String,
    default: 'Loading…',
  },
});
</script>

<template>
  <div class="flex items-center justify-center gap-3 text-gray-600">
    <svg class="h-5 w-5 animate-spin" viewBox="0 0 24 24" fill="none">
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
    </svg>
    <span>{{ label }}</span>
  </div>
</template>
