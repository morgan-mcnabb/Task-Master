import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';

import { createApiClient, resolveApiBaseUrl } from './api/client';
import { createPiniaApiPlugin } from './plugins/piniaApiPlugin';
import { ApiClientKey } from './keys';
import { installRouterGuards } from './router/guards';
import { setApiClient } from './api/registry';

// Tailwind entry (v4)
import './tailwind.css';

// API client: navigation-agnostic (guards handle redirects)
const apiClient = createApiClient({
  baseUrl: resolveApiBaseUrl(),
});

setApiClient(apiClient);

const pinia = createPinia();
pinia.use(createPiniaApiPlugin(apiClient));

installRouterGuards(router, pinia);

const app = createApp(App);
app.use(pinia);
app.use(router);
app.provide(ApiClientKey, apiClient);

// Ensure router resolves the initial route before mount for consistent first paint
router.isReady().then(() => {
  app.mount('#app');
});
