
export function debounce(fn, wait = 300, options = {}) {
  const { leading = false, trailing = true } = options;
  let timerId = null;
  let lastArgs = null;
  let lastThis = null;
  let leadingInvoked = false;

  const invoke = () => {
    const args = lastArgs;
    const ctx = lastThis;
    lastArgs = lastThis = null;
    fn.apply(ctx, args);
  };

  function debounced(...args) {
    lastArgs = args;
    lastThis = this;

    if (timerId) clearTimeout(timerId);

    if (leading && !leadingInvoked) {
      leadingInvoked = true;
      fn.apply(lastThis, lastArgs);
      lastArgs = lastThis = null;
    }

    timerId = setTimeout(() => {
      timerId = null;
      leadingInvoked = false;
      if (trailing && lastArgs) {
        invoke();
      }
    }, wait);
  }

  debounced.cancel = () => {
    if (timerId) clearTimeout(timerId);
    timerId = null;
    lastArgs = lastThis = null;
    leadingInvoked = false;
  };

  debounced.flush = () => {
    if (timerId) {
      clearTimeout(timerId);
      timerId = null;
      if (trailing && lastArgs) {
        invoke();
      }
      leadingInvoked = false;
    }
  };

  return debounced;
}
