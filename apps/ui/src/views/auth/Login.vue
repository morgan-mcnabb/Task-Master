<script setup>
import { ref, watch, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../../stores/authStore';
import { ROUTE_NAMES } from '../../constants/routeNames';

const authStore = useAuthStore();
const route = useRoute();
const router = useRouter();

const userName = ref('');
const password = ref('');
const isSubmitting = ref(false);
const errorMessage = ref('');

function computeRedirectTarget() {
  return (typeof route.query.redirect === 'string' && route.query.redirect)
    ? route.query.redirect
    : { name: ROUTE_NAMES.TASKS };
}

async function handleLogin() {
  if (!userName.value || !password.value) {
    errorMessage.value = 'Username and password are required.';
    return;
  }

  isSubmitting.value = true;
  errorMessage.value = '';
  try {
    await authStore.login(userName.value, password.value);
    await router.replace(computeRedirectTarget());
  } catch (err) {
    errorMessage.value = err?.message ?? 'Login failed.';
  } finally {
    isSubmitting.value = false;
  }
}

// If already authenticated (or becomes so), leave this page immediately.
onMounted(() => {
  if (authStore.isAuthenticated) {
    router.replace(computeRedirectTarget());
  }
});
watch(() => authStore.isAuthenticated, (now) => {
  if (now) router.replace(computeRedirectTarget());
});
</script>

<template>
  <section class="mx-auto max-w-md p-6 space-y-6">
    <header>
      <h1 class="text-2xl font-semibold">Login</h1>
      <p class="text-gray-600">Sign in to your account</p>
    </header>

    <form @submit.prevent="handleLogin" class="space-y-4">
      <div>
        <label class="block text-sm font-medium text-gray-700">Username</label>
        <input
          v-model="userName"
          type="text"
          autocomplete="username"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700">Password</label>
        <input
          v-model="password"
          type="password"
          autocomplete="current-password"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>

      <p v-if="errorMessage" class="text-sm text-red-600">{{ errorMessage }}</p>

      <button
        type="submit"
        :disabled="isSubmitting"
        class="w-full rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {{ isSubmitting ? 'Signing in…' : 'Sign in' }}
      </button>
    </form>

    <p class="text-sm text-gray-600">
      Don’t have an account?
      <router-link
        :to="{ name: ROUTE_NAMES.REGISTER, query: { redirect: route.query.redirect ?? undefined } }"
        class="text-indigo-600 hover:text-indigo-700"
      >
        Create one
      </router-link>
    </p>
  </section>
</template>
