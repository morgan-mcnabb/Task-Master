<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { debounce } from '@/utils/debounce';
import { makeAbortable } from '@/utils/makeAbortable';
import { normalizeTagsToStrings, dedupeCaseInsensitive } from '@/utils/tags';
import { useApi } from '@/useApi';

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => [],
  },
  placeholder: {
    type: String,
    default: 'Add tag…',
  },
  limit: {
    type: Number,
    default: 10,
  },
  debounceMs: {
    type: Number,
    default: 250,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  fetcher: {
    type: Function,
    default: null,
  },
  showClearButton: {
    type: Boolean,
    default: true,
  },
  clearButtonLabel: {
    type: String,
    default: 'Clear',
  },
});

const emit = defineEmits(['update:modelValue']);

const apiClient = useApi();

const rootRef = ref(null);
const inputRef = ref(null);
const inputValue = ref('');
const suggestionItems = ref([]); // string[]
const isLoading = ref(false);
const isOpen = ref(false);
const highlightedIndex = ref(-1);
const errorMessage = ref('');

const selectedTags = computed(() => dedupeCaseInsensitive(props.modelValue));

function setModelValue(nextValues) {
  const deduped = dedupeCaseInsensitive(nextValues);
  emit('update:modelValue', deduped);
}

function alreadySelected(candidate) {
  const lowered = String(candidate).trim().toLowerCase();
  return selectedTags.value.some(existing => existing.toLowerCase() === lowered);
}

function addTag(tagText) {
  const normalized = String(tagText || '').trim();
  if (!normalized) return;
  if (alreadySelected(normalized)) {
    inputValue.value = '';
    closeSuggestions();
    return;
  }
  setModelValue([...selectedTags.value, normalized]);
  inputValue.value = '';
  closeSuggestions();
}

function removeTag(tagToRemove) {
  const next = selectedTags.value.filter(t => t.toLowerCase() !== String(tagToRemove).toLowerCase());
  setModelValue(next);
}

function clearAllTags() {
  if (props.disabled) return;
  if (selectedTags.value.length === 0) return;
  setModelValue([]);
  inputValue.value = '';
  closeSuggestions();
  try { inputRef.value?.focus(); } catch {}
}

function openSuggestions() {
  if (suggestionItems.value.length > 0) {
    isOpen.value = true;
  }
}
function closeSuggestions() {
  isOpen.value = false;
  highlightedIndex.value = -1;
}

function onInputKeydown(event) {
  if (props.disabled) return;

  const key = event.key;

  if (key === 'Enter' || key === 'Tab' || key === ',') {
    if (highlightedIndex.value >= 0 && highlightedIndex.value < suggestionItems.value.length) {
      event.preventDefault();
      addTag(suggestionItems.value[highlightedIndex.value]);
      return;
    }
    const candidate = inputValue.value;
    if (candidate) {
      event.preventDefault();
      addTag(candidate);
      return;
    }
  }

  if (key === 'ArrowDown') {
    if (!isOpen.value) {
      openSuggestions();
      return;
    }
    event.preventDefault();
    const next = Math.min(suggestionItems.value.length - 1, highlightedIndex.value + 1);
    highlightedIndex.value = next;
    return;
  }

  if (key === 'ArrowUp') {
    if (!isOpen.value) return;
    event.preventDefault();
    const next = Math.max(-1, highlightedIndex.value - 1);
    highlightedIndex.value = next;
    return;
  }

  if (key === 'Escape') {
    if (isOpen.value) {
      event.preventDefault();
      closeSuggestions();
    }
  }
}

function onSuggestionClick(item) {
  addTag(item);
}

function onInputFocus() {
  if (suggestionItems.value.length) {
    isOpen.value = true;
  }
}

async function defaultFetchSuggestions(query, limit, signal) {
  const searchParam = encodeURIComponent(query);
  const limitParam = encodeURIComponent(String(limit));
  const { data } = await apiClient.get(`/api/v1/tags?search=${searchParam}&limit=${limitParam}`, { signal });
  const rawItems = Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : [];
  const normalized = normalizeTagsToStrings(rawItems);
  return normalized;
}

const runFetchAbortable = makeAbortable(async ({ signal }, query) => {
  isLoading.value = true;
  errorMessage.value = '';
  try {
    const fetchFn = typeof props.fetcher === 'function' ? props.fetcher : defaultFetchSuggestions;
    const fetched = await fetchFn(query, props.limit, signal);
    const filtered = fetched.filter(name => !alreadySelected(name));
    suggestionItems.value = filtered.slice(0, props.limit);
    if (inputValue.value.trim().length > 0 && suggestionItems.value.length > 0) {
      isOpen.value = true;
    } else {
      closeSuggestions();
    }
  } catch (error) {
    if (error?.name !== 'AbortError') {
      errorMessage.value = error?.message ?? 'Failed to load suggestions.';
      closeSuggestions();
    }
  } finally {
    isLoading.value = false;
  }
});

const debouncedFetch = debounce((query) => {
  if (!query || !query.trim()) {
    suggestionItems.value = [];
    closeSuggestions();
    return;
  }
  runFetchAbortable.run(query.trim());
}, props.debounceMs);

watch(inputValue, (current) => {
  highlightedIndex.value = -1;
  debouncedFetch(current);
});

watch(() => props.modelValue, () => {
  suggestionItems.value = suggestionItems.value.filter(s => !alreadySelected(s));
  if (suggestionItems.value.length === 0) {
    closeSuggestions();
  }
});

function handleGlobalClick(event) {
  const rootEl = rootRef.value;
  if (!rootEl) return;
  if (event && rootEl !== event.target && !rootEl.contains(event.target)) {
    closeSuggestions();
  }
}

onMounted(() => {
  window.addEventListener('click', handleGlobalClick, { capture: true });
});
onBeforeUnmount(() => {
  window.removeEventListener('click', handleGlobalClick, { capture: true });
});
</script>

<template>
  <div ref="rootRef" class="w-full">
    <div class="flex flex-wrap items-start gap-2">
      <div class="relative w-56 flex-shrink-0">
        <input
          ref="inputRef"
          v-model="inputValue"
          type="text"
          :placeholder="placeholder"
          :disabled="disabled"
          class="w-56 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-60"
          @keydown="onInputKeydown"
          @focus="onInputFocus"
        />

        <div
          v-if="isOpen"
          class="absolute z-10 mt-1 max-h-48 w-56 overflow-auto rounded-md border border-gray-200 bg-white shadow"
          role="listbox"
        >
          <div v-if="isLoading" class="px-3 py-2 text-sm text-gray-500">Loading…</div>
          <div v-else-if="errorMessage" class="px-3 py-2 text-sm text-red-600">{{ errorMessage }}</div>
          <template v-else>
            <button
              v-for="(item, index) in suggestionItems"
              :key="item"
              type="button"
              class="block w-full cursor-pointer px-3 py-1.5 text-left text-sm hover:bg-gray-50"
              :class="{ 'bg-gray-100': index === highlightedIndex }"
              role="option"
              @mousedown.prevent="onSuggestionClick(item)"
            >
              {{ item }}
            </button>
          </template>
        </div>
      </div>

      <div class="flex min-w-0 flex-1 flex-wrap items-center gap-2">
        <button
          v-if="showClearButton && selectedTags.length > 0"
          type="button"
          class="inline-flex items-center gap-1 rounded-md border border-gray-300 px-2 py-1 text-xs text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          :disabled="disabled"
          @click="clearAllTags"
          aria-label="Clear tags"
          :title="clearButtonLabel"
        >
          ✕ <span>{{ clearButtonLabel }}</span>
        </button>

        <div
          v-for="tag in selectedTags"
          :key="tag"
          class="inline-flex items-center gap-2 rounded-full bg-indigo-50 px-3 py-1 text-sm text-indigo-700"
        >
          <span class="truncate max-w-[12rem]">{{ tag }}</span>
          <button
            type="button"
            class="opacity-70 hover:opacity-100"
            title="Remove"
            :disabled="disabled"
            @click="removeTag(tag)"
          >✕</button>
        </div>
      </div>
    </div>

    <div class="mt-1 text-xs text-gray-500">
      Press Enter, comma, or Tab to add a tag. Suggestions update as you type.
    </div>
  </div>
</template>
