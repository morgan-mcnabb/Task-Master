/**
 * Centralized styles for task statuses.
 * Keep this tiny and dumb: return Tailwind utility strings for reuse.
 * This file is the single source of truth for status colors across the app.
 */

export const STATUS_STYLE_CONFIG = Object.freeze({
  Todo: {
    badge: 'bg-gray-100 text-gray-700',
    buttonSelected: 'bg-gray-900 text-white hover:bg-gray-800',
    dot: 'bg-gray-400',
    selectAccent: 'border-gray-300 bg-gray-50 text-gray-800 focus:ring-gray-500 focus:border-gray-500',
  },
  InProgress: {
    badge: 'bg-amber-100 text-amber-800',
    buttonSelected: 'bg-amber-600 text-white hover:bg-amber-700',
    dot: 'bg-amber-500',
    selectAccent: 'border-amber-300 bg-amber-50 text-amber-900 focus:ring-amber-500 focus:border-amber-500',
  },
  Done: {
    badge: 'bg-green-100 text-green-700',
    buttonSelected: 'bg-green-600 text-white hover:bg-green-700',
    dot: 'bg-green-600',
    selectAccent: 'border-green-300 bg-green-50 text-green-900 focus:ring-green-600 focus:border-green-600',
  },
  Archived: {
    badge: 'bg-slate-200 text-slate-700',
    buttonSelected: 'bg-slate-600 text-white hover:bg-slate-700',
    dot: 'bg-slate-500',
    selectAccent: 'border-slate-300 bg-slate-50 text-slate-800 focus:ring-slate-500 focus:border-slate-500',
  },
});

const DEFAULT_BADGE = 'bg-gray-100 text-gray-700';
const DEFAULT_BUTTON_SELECTED = 'bg-gray-900 text-white hover:bg-gray-800';
const DEFAULT_DOT = 'bg-gray-400';
const DEFAULT_SELECT_ACCENT = 'border-gray-300 bg-gray-50 text-gray-800 focus:ring-gray-500 focus:border-gray-500';

const BUTTON_BASE = 'px-3 py-1.5 rounded-md text-sm';
const BUTTON_UNSELECTED =
  'px-3 py-1.5 rounded-md text-sm border border-gray-300 text-gray-800 hover:bg-gray-50';

/**
 * Returns Tailwind classes for a small pill/badge representing the status.
 * @param {string} status
 * @returns {string}
 */
export function getStatusBadgeClasses(status) {
  const style = STATUS_STYLE_CONFIG[status];
  return style ? style.badge : DEFAULT_BADGE;
}

/**
 * Returns Tailwind classes for a status button in the details page.
 * Unselected buttons use a neutral outline; selected uses the status color.
 * @param {string} status
 * @param {boolean} isSelected
 * @returns {string}
 */
export function getStatusButtonClasses(status, isSelected) {
  if (isSelected) {
    const style = STATUS_STYLE_CONFIG[status];
    const selectedColorClass = style ? style.buttonSelected : DEFAULT_BUTTON_SELECTED;
    return `${BUTTON_BASE} ${selectedColorClass}`;
  }
  return BUTTON_UNSELECTED;
}

/**
 * Returns Tailwind classes for a small circular dot representing the status.
 * @param {string} status
 * @returns {string}
 */
export function getStatusDotClasses(status) {
  const style = STATUS_STYLE_CONFIG[status];
  return style ? style.dot : DEFAULT_DOT;
}

/**
 * Returns only the accent classes for a <select> showing the status.
 * Keep the base sizing/shape in the component; this function supplies color + focus styles.
 * @param {string} status
 * @returns {string}
 */
export function getStatusSelectAccentClasses(status) {
  const style = STATUS_STYLE_CONFIG[status];
  return style ? style.selectAccent : DEFAULT_SELECT_ACCENT;
}
