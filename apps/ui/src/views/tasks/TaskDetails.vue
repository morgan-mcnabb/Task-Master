<script setup>
import { useRoute } from 'vue-router';

const route = useRoute();
const taskId = route.params.id;
</script>

<template>
  <section class="mx-auto max-w-3xl p-6">
    <h1 class="text-2xl font-semibold">Task Details</h1>
    <p class="mt-2 text-gray-600">Task ID: <strong>{{ taskId }}</strong></p>
    <p class="mt-2 text-gray-600">Details view coming soon.</p>
  </section>
</template>
