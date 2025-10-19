import { ROUTE_NAMES } from '../constants/routeNames';
import { useAuthStore } from '../stores/authStore';

/**
 * Global router guards with "non-blocking auth priming".
 *
 * Principles:
 *  - Public routes must never be blocked by an auth probe.
 *  - Protected routes should be conservative: if auth is unknown, redirect
 *    immediately to /login and load auth in the background.
 *  - Avoid double fetches and race conditions via a single in-flight load.
 */
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

    // ✅ Public routes: never block; just start auth check in the background if needed
    if (!requiresAuth) {
      primeAuthIfNeeded();

      // If user is already authenticated and trying to view /login or /register,
      // bounce them to their intended page or the Tasks list.
      if (isPublicAuthPage && authStore.isAuthenticated) {
        const redirectTarget =
          typeof to.query.redirect === 'string' && to.query.redirect
            ? to.query.redirect
            : { name: ROUTE_NAMES.TASKS };
        return redirectTarget;
      }

      return true;
    }

    // 🔒 Protected routes:
    // If we already know the user is authenticated, allow navigation.
    if (authStore.isAuthenticated) {
      return true;
    }

    // If we don't yet know (fresh page load; auth not primed), *do not block*.
    // Kick off auth load in the background and immediately redirect to /login.
    if (!authStore.isLoaded) {
      primeAuthIfNeeded();
      return {
        name: ROUTE_NAMES.LOGIN,
        query: { redirect: to.fullPath || '/' },
        replace: true,
      };
    }

    // We know state and it's unauthenticated → go to login.
    return {
      name: ROUTE_NAMES.LOGIN,
      query: { redirect: to.fullPath || '/' },
      replace: true,
    };
  });
}
