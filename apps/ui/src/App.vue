<script setup>
import AppLayout from './components/layout/AppLayout.vue'
import LoadingSpinner from './components/ui/LoadingSpinner.vue'
</script>

<template>
  <AppLayout>
    <RouterView v-slot="{ Component, route }">
      <!-- Only render when the router has a component -->
      <template v-if="Component">
        <!-- You can keep Suspense if you like, but it's optional.
             Lazy route components won't use it anyway. -->
        <!-- <Suspense> -->
          <component :is="Component" :key="route.fullPath" />
          <!--
          <template #fallback>
            <div class="p-8">
              <LoadingSpinner label="Loading page…" />
            </div>
          </template>
          -->
        <!-- </Suspense> -->
      </template>
      <!-- Optional: show a very quick fallback while Component is null -->
      <template v-else>
        <div class="p-8">
          <LoadingSpinner label="Loading page…" />
        </div>
      </template>
    </RouterView>
  </AppLayout>
</template>
