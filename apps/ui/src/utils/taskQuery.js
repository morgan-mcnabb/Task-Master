import { DEFAULT_PAGINATION, TASK_SORT_FIELDS, SORT_DIRECTIONS } from '@/constants/tasks';

function coerceArray(value) {
  if (value == null) return [];
  return Array.isArray(value) ? value : [value];
}
function coerceNumber(value, fallback) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? n : fallback;
}
function coerceEnum(value, allowed, fallback) {
  if (!value || typeof value !== 'string') return fallback;
  return allowed.includes(value) ? value : fallback;
}
function normalizeDate(value) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return '';
  return value;
}

export function parseRouteQueryToFilters(routeQuery) {
  return {
    pageNumber: coerceNumber(routeQuery.pageNumber, DEFAULT_PAGINATION.pageNumber),
    pageSize: coerceNumber(routeQuery.pageSize, DEFAULT_PAGINATION.pageSize),
    statuses: coerceArray(routeQuery.statuses),
    priorities: coerceArray(routeQuery.priorities),
    tags: coerceArray(routeQuery.tags),
    dueOnOrAfter: normalizeDate(routeQuery.dueOnOrAfter),
    dueOnOrBefore: normalizeDate(routeQuery.dueOnOrBefore),
    search: typeof routeQuery.search === 'string' ? routeQuery.search : '',
    sortBy: coerceEnum(routeQuery.sortBy, TASK_SORT_FIELDS, 'dueDate'),
    sortDirection: coerceEnum(routeQuery.sortDirection, SORT_DIRECTIONS, 'asc'),
  };
}

export function buildRouterQueryFromFilters(filters) {
  const query = {
    pageNumber: String(filters.pageNumber),
    pageSize: String(filters.pageSize),
    sortBy: filters.sortBy,
    sortDirection: filters.sortDirection,
  };
  if (filters.search) query.search = filters.search;
  if (filters.dueOnOrAfter) query.dueOnOrAfter = filters.dueOnOrAfter;
  if (filters.dueOnOrBefore) query.dueOnOrBefore = filters.dueOnOrBefore;
  if (Array.isArray(filters.statuses) && filters.statuses.length > 0) query.statuses = filters.statuses;
  if (Array.isArray(filters.priorities) && filters.priorities.length > 0) query.priorities = filters.priorities;
  if (Array.isArray(filters.tags) && filters.tags.length > 0) query.tags = filters.tags;
  return query;
}

export function buildFetchParamsFromFilters(filters) {
  const params = {
    pageNumber: filters.pageNumber,
    pageSize: filters.pageSize,
    includeTags: true,
    sortBy: filters.sortBy,
    sortDirection: filters.sortDirection,
  };
  if (filters.search) params.search = filters.search;
  if (filters.dueOnOrAfter) params.dueOnOrAfter = filters.dueOnOrAfter;
  if (filters.dueOnOrBefore) params.dueOnOrBefore = filters.dueOnOrBefore;
  if (filters.statuses?.length) params.statuses = filters.statuses;
  if (filters.priorities?.length) params.priorities = filters.priorities;
  if (filters.tags?.length) params.tags = filters.tags;
  return params;
}
