export function makeAbortable(taskFactory) {
  let controller = null;

  function run(...args) {
    // Abort previous task (latest-wins)
    if (controller) controller.abort();
    controller = new AbortController();
    const signal = controller.signal;

    const promise = Promise.resolve().then(() => taskFactory({ signal }, ...args))
      .finally(() => {
        // Clear only if it's still the same controller (avoid race)
        if (controller && controller.signal === signal) {
          controller = null;
        }
      });

    return { promise, signal };
  }

  function abort() {
    if (controller) {
      controller.abort();
      controller = null;
    }
  }

  function getSignal() {
    return controller ? controller.signal : null;
  }

  return { run, abort, getSignal };
}
