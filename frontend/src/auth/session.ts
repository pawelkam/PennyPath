import { reactive, readonly } from 'vue'
import { AuthError, createAuthApi, type Account, type Credentials } from '@/api/auth'

type SessionStatus = 'unknown' | 'loading' | 'authenticated' | 'anonymous' | 'error'

export function createAuthSession() {
  const state = reactive({
    status: 'unknown' as SessionStatus,
    user: null as Account | null,
    error: null as unknown,
  })
  const unauthorizedListeners = new Set<() => void>()
  let revision = 0
  let pending: Promise<Account | null> | null = null

  function clear() {
    revision++
    state.user = null
    state.error = null
    state.status = 'anonymous'
  }

  const api = createAuthApi(() => {
    clear()
    unauthorizedListeners.forEach(listener => listener())
  })

  function ensureUser(): Promise<Account | null> {
    if (pending) return pending
    if (state.status === 'authenticated' || state.status === 'anonymous') return Promise.resolve(state.user)
    const started = revision
    state.status = 'loading'
    state.error = null
    pending = api.me().then(user => {
      if (started === revision) {
        state.user = user
        state.status = 'authenticated'
      }
      return state.user
    }).catch((error: unknown) => {
      if (error instanceof AuthError && error.status === 401) return null
      if (started === revision) {
        state.error = error
        state.status = 'error'
      }
      throw error
    }).finally(() => { pending = null })
    return pending
  }

  return {
    state: readonly(state),
    ensureUser,
    async login(credentials: Credentials) {
      const user = await api.login(credentials)
      revision++
      state.user = user
      state.status = 'authenticated'
      state.error = null
    },
    register: api.register,
    async logout() {
      await api.logout()
      clear()
    },
    onUnauthorized(listener: () => void) {
      unauthorizedListeners.add(listener)
      return () => unauthorizedListeners.delete(listener)
    },
  }
}

export type AuthSession = ReturnType<typeof createAuthSession>
export const session = createAuthSession()
