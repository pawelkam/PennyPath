import { createRouter, createWebHistory, type Router, type RouterHistory } from 'vue-router'
import { session, type AuthSession } from '@/auth/session'
import LoginView from '@/views/LoginView.vue'
import RegisterView from '@/views/RegisterView.vue'
import AccountHomeView from '@/views/AccountHomeView.vue'

export function safeReturnUrl(value: unknown, router: Router): string {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || /[\\\u0000-\u0020\u007f]/.test(value)) return '/app'
  // Reject encoded path delimiters, nested encoding and control characters.
  if (/%(?:2f|5c|25|0[0-9a-f]|1[0-9a-f]|7f)/i.test(value)) return '/app'
  if (value.split(/[?#]/, 1)[0]?.includes('%')) return '/app'
  const resolved = router.resolve(value)
  return resolved.matched.length > 0 && resolved.matched.every(record => !record.redirect) && resolved.meta.requiresAuth
    ? resolved.fullPath : '/app'
}

export function createAppRouter(history: RouterHistory = createWebHistory(), auth: AuthSession = session) {
  const router = createRouter({
    history,
    routes: [
      { path: '/', redirect: '/app' },
      { path: '/login', name: 'login', component: LoginView },
      { path: '/register', name: 'register', component: RegisterView },
      { path: '/app', name: 'account', component: AccountHomeView, meta: { requiresAuth: true } },
      { path: '/:pathMatch(.*)*', redirect: '/app' },
    ],
  })

  router.beforeEach(async to => {
    if (!to.meta.requiresAuth) return true
    try {
      if (await auth.ensureUser()) return true
    } catch {
      // Preserve the error state so an outage is offered as a retry, never a logout.
    }
    return { name: 'login', query: { returnUrl: to.fullPath } }
  })

  auth.onUnauthorized(() => {
    if (router.currentRoute.value.meta.requiresAuth) {
      void router.replace({ name: 'login', query: { returnUrl: router.currentRoute.value.fullPath } })
    }
  })
  return router
}
