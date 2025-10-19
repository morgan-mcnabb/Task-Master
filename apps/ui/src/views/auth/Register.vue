<script setup>
import { ref } from 'vue';
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

async function handleRegister() {
  if (!userName.value || !password.value) {
    errorMessage.value = 'Username and password are required.';
    return;
  }

  isSubmitting.value = true;
  errorMessage.value = '';
  try {
    await authStore.register({ userName: userName.value, password: password.value });

    const redirectTarget = typeof route.query.redirect === 'string' && route.query.redirect
      ? route.query.redirect
      : { name: ROUTE_NAMES.TASKS };

    await router.replace(redirectTarget);
  } catch (err) {
    errorMessage.value = err?.message ?? 'Registration failed.';
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <section class="mx-auto max-w-md p-6 space-y-6">
    <header>
      <h1 class="text-2xl font-semibold">Create an account</h1>
      <p class="text-gray-600">Register to start using Task Master</p>
    </header>

    <form @submit.prevent="handleRegister" class="space-y-4">
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
          autocomplete="new-password"
          class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>

      <p v-if="errorMessage" class="text-sm text-red-600">{{ errorMessage }}</p>

      <button
        type="submit"
        :disabled="isSubmitting"
        class="w-full rounded-md bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {{ isSubmitting ? 'Creating…' : 'Create account' }}
      </button>
    </form>

    <p class="text-sm text-gray-600">
      Already have an account?
      <router-link
        :to="{ name: ROUTE_NAMES.LOGIN, query: { redirect: route.query.redirect ?? undefined } }"
        class="text-indigo-600 hover:text-indigo-700"
      >
        Sign in
      </router-link>
    </p>
  </section>
</template>
