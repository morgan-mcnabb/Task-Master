<script setup>
defineProps({
  message: {
    type: String,
    required: true,
  },
});
</script>

<template>
  <div class="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-red-700">
    {{ message }}
  </div>
</template>
