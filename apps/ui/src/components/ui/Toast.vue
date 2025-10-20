<script setup>
const props = defineProps({
  title: { type: String, default: '' },
  message: { type: String, default: '' },
  correlationId: { type: String, default: '' },
  variant: { type: String, default: 'error' }, // 'error' | 'info' | 'success'
  show: { type: Boolean, default: false },
});
const emit = defineEmits(['close']);

function copyCorrelationId() {
  if (!props.correlationId) return;
  try { navigator.clipboard.writeText(props.correlationId); } catch {}
}
</script>

<template>
  <transition name="fade">
    <div
      v-if="show"
      class="fixed right-4 top-4 z-50 w-[28rem] max-w-[90vw] rounded-lg border p-4 shadow-lg"
      :class="{
        'bg-red-50 border-red-200 text-red-800': variant === 'error',
        'bg-blue-50 border-blue-200 text-blue-800': variant === 'info',
        'bg-green-50 border-green-200 text-green-800': variant === 'success',
      }"
      role="alert"
    >
      <div class="flex items-start justify-between gap-3">
        <div>
          <div v-if="title" class="text-sm font-semibold">{{ title }}</div>
          <div class="mt-1 text-sm">{{ message }}</div>
          <div v-if="correlationId" class="mt-2 flex items-center gap-2">
            <span class="text-xs opacity-80">Correlation ID:</span>
            <code class="rounded bg-white/50 px-1.5 py-0.5 text-xs">{{ correlationId }}</code>
            <button class="text-xs underline" @click="copyCorrelationId">Copy</button>
          </div>
        </div>
        <button class="text-sm underline opacity-70 hover:opacity-100" @click="emit('close')">
          Dismiss
        </button>
      </div>
    </div>
  </transition>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity .15s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
