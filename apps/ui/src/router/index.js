import { createRouter, createWebHistory } from 'vue-router';
import { ROUTE_NAMES } from '../constants/routeNames';

// Auth (public)
const LoginView = () => import('../views/auth/Login.vue');
const RegisterView = () => import('../views/auth/Register.vue');

// Tasks (protected)
const TasksListView = () => import('../views/tasks/TasksList.vue');
const TaskNewView = () => import('../views/tasks/TaskNew.vue');
const TaskDetailsView = () => import('../views/tasks/TaskDetails.vue');

const NotFoundView = () => import('../views/NotFound.vue');

const routes = [
  { path: '/', redirect: { name: ROUTE_NAMES.LOGIN } },

  // Public
  { path: '/login', name: ROUTE_NAMES.LOGIN, component: LoginView, meta: { public: true } },
  { path: '/register', name: ROUTE_NAMES.REGISTER, component: RegisterView, meta: { public: true } },

  // Protected
  { path: '/tasks', name: ROUTE_NAMES.TASKS, component: TasksListView, meta: { requiresAuth: true } },
  { path: '/tasks/new', name: ROUTE_NAMES.TASKS_NEW, component: TaskNewView, meta: { requiresAuth: true } },
  { path: '/tasks/:id', name: ROUTE_NAMES.TASK_DETAILS, component: TaskDetailsView, meta: { requiresAuth: true } },

  { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

export default router;
