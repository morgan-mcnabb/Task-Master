import { ROUTE_NAMES } from '../constants/routeNames';
import { useAuthStore } from '../stores/authStore';

export function installRouterGuards(router, pinia) {
  const authStore = useAuthStore(pinia);

  let loadMeInFlight = null;
  function primeAuthIfNeeded() {
    if (authStore.isLoaded || loadMeInFlight) return;
    loadMeInFlight = authStore.loadMe().finally(() => {
      loadMeInFlight = null;
    });
  }

  router.beforeEach((to) => {
    const requiresAuth = to.matched.some(r => r.meta?.requiresAuth === true);
    const isPublicAuthPage =
      to.name === ROUTE_NAMES.LOGIN || to.name === ROUTE_NAMES.REGISTER;

    if (!requiresAuth) {
      primeAuthIfNeeded();

      if (isPublicAuthPage && authStore.isAuthenticated) {
        const redirectTarget =
          typeof to.query.redirect === 'string' && to.query.redirect
            ? to.query.redirect
            : { name: ROUTE_NAMES.TASKS };
        return redirectTarget;
      }

      return true;
    }

    if (authStore.isAuthenticated) {
      return true;
    }

    if (!authStore.isLoaded) {
      primeAuthIfNeeded();
      return {
        name: ROUTE_NAMES.LOGIN,
        query: { redirect: to.fullPath || '/' },
        replace: true,
      };
    }

    return {
      name: ROUTE_NAMES.LOGIN,
      query: { redirect: to.fullPath || '/' },
      replace: true,
    };
  });
}
