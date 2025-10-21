<script setup>
import AppLayout from './components/layout/AppLayout.vue'
import LoadingSpinner from './components/ui/LoadingSpinner.vue'
</script>

<template>
  <AppLayout>
    <RouterView v-slot="{ Component, route }">
      <template v-if="Component">
          <component :is="Component" />
      </template>
      <template v-else>
        <div class="p-8">
          <LoadingSpinner label="Loading page…" />
        </div>
      </template>
    </RouterView>
  </AppLayout>
</template>
