import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';

import { createApiClient, resolveApiBaseUrl } from './api/client';
import { createPiniaApiPlugin } from './plugins/piniaApiPlugin';
import { ApiClientKey } from './keys';
import { installRouterGuards } from './router/guards';
import { setApiClient } from './api/registry';

import './tailwind.css';

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

router.isReady().then(() => {
  app.mount('#app');
});
