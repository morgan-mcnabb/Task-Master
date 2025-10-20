<script setup>
import { computed } from 'vue';
import { useAuthStore } from '@/stores/authStore';
import { ROUTE_NAMES } from '@/constants/routeNames';
import { useRouter } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();

const isAuthenticated = computed(() => authStore.isAuthenticated);
const userName = computed(() => authStore.userName);

// Brand link target: Tasks if signed in, otherwise Login
const brandTarget = computed(() =>
  isAuthenticated.value ? { name: ROUTE_NAMES.TASKS } : { name: ROUTE_NAMES.LOGIN }
);

async function handleLogout() {
  await authStore.logout();
  await router.replace({ name: ROUTE_NAMES.LOGIN });
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 text-gray-900">
    <nav class="border-b border-gray-200 bg-white">
      <div class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <router-link :to="brandTarget" class="text-lg font-semibold tracking-tight">
          Task&nbsp;Master
        </router-link>

        <div class="flex items-center gap-3 sm:gap-6">
          <template v-if="isAuthenticated">
            <span class="text-sm text-gray-600">Signed in as <strong>{{ userName }}</strong></span>
            <button
              class="rounded-md border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-50"
              @click="handleLogout"
            >
              Logout
            </button>
          </template>
          <template v-else>
            <router-link
              :to="{ name: ROUTE_NAMES.LOGIN }"
              class="text-gray-700 hover:text-indigo-600"
            >
              Login
            </router-link>
            <router-link
              :to="{ name: ROUTE_NAMES.REGISTER }"
              class="rounded-md bg-indigo-600 px-3 py-1.5 text-white hover:bg-indigo-700"
            >
              Register
            </router-link>
          </template>
        </div>
      </div>
    </nav>

    <main>
      <slot />
    </main>

    <footer class="mt-16 border-t border-gray-200 bg-white">
      <div class="mx-auto max-w-6xl px-4 py-6 text-sm text-gray-500">
        © {{ new Date().getFullYear() }} Task Master
      </div>
    </footer>
  </div>
</template>
