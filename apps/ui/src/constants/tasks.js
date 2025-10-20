export const TASK_STATUSES = Object.freeze([
  'Todo',
  'InProgress',
  'Done',
  'Archived',
]);

export const TASK_PRIORITIES = Object.freeze([
  'Low',
  'Medium',
  'High'
]);

/**
 * Sort options for the Tasks list:
 * - Keep values aligned with backend field names.
 * - Labels are normalized for UI.
 * - We intentionally exclude "createdUtc/createdAt" because the column is not shown.
 */
export const TASK_SORT_OPTIONS = Object.freeze([
  { value: 'dueDate', label: 'Due date' },
  { value: 'priority', label: 'Priority' },
  { value: 'status', label: 'Status' },
  { value: 'title', label: 'Title' },
]);

// Allowed values for parsing/validation (used by taskQuery)
export const TASK_SORT_FIELDS = Object.freeze(TASK_SORT_OPTIONS.map(option => option.value));

export const SORT_DIRECTIONS = Object.freeze(['asc', 'desc']);

export const DEFAULT_PAGINATION = Object.freeze({
  pageNumber: 1,
  pageSize: 20,
});

export const PAGE_SIZE_OPTIONS = Object.freeze([10, 20, 50, 100]);
