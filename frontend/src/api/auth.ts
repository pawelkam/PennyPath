export interface Account {
  userId: string
  email: string
}

export interface Credentials {
  email: string
  password: string
}

export class AuthError extends Error {
  constructor(public status: number, public code: string) {
    super(code)
  }
}

export function authErrorMessage(error: unknown): string {
  if (!(error instanceof AuthError)) return 'Nie udało się połączyć z serwerem. Spróbuj ponownie.'
  if (error.code === 'https_required') return 'Do korzystania z konta wymagane jest bezpieczne połączenie HTTPS.'
  if (error.status === 429) return 'Zbyt wiele prób. Poczekaj minutę i spróbuj ponownie.'
  if (error.code === 'invalid_credentials') return 'Niepoprawny email lub hasło.'
  if (error.code === 'duplicate_email') return 'Ten adres email jest niedostępny.'
  if (error.code === 'csrf_invalid') return 'Nie udało się potwierdzić żądania. Spróbuj ponownie.'
  if (error.status === 400) return 'Sprawdź email i hasło. Hasło musi mieć od 12 do 128 znaków.'
  return 'Nie udało się wykonać operacji. Spróbuj ponownie.'
}

export function isSecureOrigin(): boolean {
  // return window.location.protocol === 'https:'
  return true
}

export function createAuthApi(onUnauthorized: () => void) {
  let csrfToken: string | null = null
  let csrfVersion = 0

  function invalidateCsrf() {
    csrfToken = null
    csrfVersion++
  }

  async function request(path: string, options: RequestInit = {}): Promise<Response> {
    const response = await fetch(`/api/auth/${path}`, {
      ...options,
      credentials: 'same-origin',
      cache: 'no-store',
    })
    if (!response.ok) {
      if (response.status === 401) {
        invalidateCsrf()
        onUnauthorized()
      }
      let code = 'request_failed'
      try {
        const problem: unknown = await response.json()
        if (problem && typeof problem === 'object' && 'code' in problem && typeof problem.code === 'string') {
          code = problem.code
        }
      } catch { /* Empty or non-JSON failures still carry their HTTP status. */ }
      if (code === 'csrf_invalid') invalidateCsrf()
      throw new AuthError(response.status, code)
    }
    return response
  }

  async function post(path: string, expectedStatus: number, credentials?: Credentials) {
    // Check before fetching CSRF or serializing credentials, including on localhost.
    if (!isSecureOrigin()) throw new AuthError(400, 'https_required')
    if (!csrfToken) {
      const version = csrfVersion
      const response = await request('csrf')
      const result: { requestToken: string } = await response.json()
      if (version !== csrfVersion) throw new AuthError(400, 'csrf_invalid')
      csrfToken = result.requestToken
    }
    const response = await request(path, {
      method: 'POST',
      headers: {
        'X-CSRF-TOKEN': csrfToken,
        ...(credentials ? { 'Content-Type': 'application/json' } : {}),
      },
      ...(credentials ? { body: JSON.stringify({ email: credentials.email.trim(), password: credentials.password }) } : {}),
    })
    if (response.status !== expectedStatus) throw new AuthError(response.status, 'unexpected_response')
    return response
  }

  return {
    async me(): Promise<Account> {
      return (await request('me')).json()
    },
    async register(credentials: Credentials) {
      await post('register', 201, credentials)
    },
    async login(credentials: Credentials): Promise<Account> {
      const response = await post('login', 200, credentials)
      invalidateCsrf()
      return response.json()
    },
    async logout() {
      try {
        await post('logout', 204)
      } catch (error) {
        if (!(error instanceof AuthError) || error.status !== 401) throw error
      }
      invalidateCsrf()
    },
    invalidateCsrf,
  }
}
