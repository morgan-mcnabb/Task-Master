<script setup>
import { useRoute } from 'vue-router';
import { ROUTE_NAMES } from '@/constants/routeNames';
import { computed } from 'vue';
import { useAuthStore } from '@/stores/authStore';

const route = useRoute();
const authStore = useAuthStore();
const isAuthenticated = computed(() => authStore.isAuthenticated);
</script>

<template>
  <section class="mx-auto max-w-6xl px-4 py-16">
    <div class="grid items-center gap-10 md:grid-cols-2">
      <div class="space-y-5">
        <h1 class="text-4xl font-bold tracking-tight">Get your tasks under control</h1>
        <p class="text-gray-600">
          A focused task manager with sane APIs and a clean UI. Nothing more, nothing less.
        </p>
        <div class="flex flex-wrap gap-3">
          <router-link
            :to="{ name: ROUTE_NAMES.TASKS }"
            class="rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700"
          >
            View Tasks
          </router-link>
          <router-link
            v-if="!isAuthenticated"
            :to="{ name: ROUTE_NAMES.REGISTER, query: { redirect: route.fullPath } }"
            class="rounded-md border border-gray-300 px-4 py-2 text-gray-800 hover:bg-gray-50"
          >
            Create Account
          </router-link>
        </div>
      </div>

      <div class="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <div class="text-sm text-gray-500">Why this is better now</div>
        <ul class="mt-3 list-disc space-y-1 pl-5 text-gray-700">
          <li>Proper auth flow (no redirect loop).</li>
          <li>Correct API routes and ETag handling.</li>
          <li>Clear navigation: Home / Tasks / Login / Register.</li>
        </ul>
      </div>
    </div>
  </section>
</template>
