import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory } from 'vue-router'

const account = { userId: 'owner-a', email: 'anna@example.test' }
const credentials = { email: 'anna@example.test', password: ' długa fraza bez zmian ' }
let wrapper: VueWrapper | undefined
let fetchMock: ReturnType<typeof vi.fn<typeof fetch>>

function response(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), { status })
}

async function open(path: string) {
  const { createAppRouter } = await import('@/router')
  const { default: App } = await import('@/App.vue')
  const router = createAppRouter(createMemoryHistory())
  await router.push(path)
  wrapper = mount(App, { global: { plugins: [router] } })
  await flushPromises()
  return router
}

async function fillLogin() {
  await wrapper!.get('#login-email').setValue(credentials.email)
  await wrapper!.get('#login-password').setValue(credentials.password)
}

beforeEach(() => {
  vi.resetModules()
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.unstubAllGlobals()
})

describe('account forms and protocol', () => {
  it('registers, requires separate login, preserves return URL and rotates CSRF across login/logout', async () => {
    fetchMock
      .mockResolvedValueOnce(response(200, { requestToken: 'anonymous-token' }))
      .mockResolvedValueOnce(response(201, account))
      .mockResolvedValueOnce(response(200, account))
      .mockResolvedValueOnce(response(200, { requestToken: 'owner-token' }))
      .mockResolvedValueOnce(response(204))
      .mockResolvedValueOnce(response(200, { requestToken: 'new-anonymous-token' }))
      .mockResolvedValueOnce(response(200, account))
    const router = await open('/register?returnUrl=%2Fapp%3Ftab%3Doverview')
    await wrapper!.get('#register-email').setValue(` ${credentials.email} `)
    await wrapper!.get('#register-password').setValue(credentials.password)
    await wrapper!.get('#register-confirmation').setValue(credentials.password)
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.returnUrl).toBe('/app?tab=overview')
    expect(wrapper!.text()).toContain('Konto zostało utworzone')
    expect(wrapper!.find('.account-home').exists()).toBe(false)
    const { session } = await import('@/auth/session')
    expect(session.state.user).toBeNull()
    await fillLogin()
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/app?tab=overview')
    expect(wrapper!.text()).toContain(account.email)
    await wrapper!.get('button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('login')
    expect(session.state.user).toBeNull()
    await fillLogin()
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    const posts = fetchMock.mock.calls.filter(([, options]) => options?.method === 'POST')
    expect(posts.map(([url]) => url)).toEqual(['/api/auth/register', '/api/auth/login', '/api/auth/logout', '/api/auth/login'])
    expect(posts.map(([, options]) => (options?.headers as Record<string, string>)['X-CSRF-TOKEN']))
      .toEqual(['anonymous-token', 'anonymous-token', 'owner-token', 'new-anonymous-token'])
    expect(JSON.parse(posts[0]![1]!.body as string)).toEqual(credentials)
    expect(fetchMock.mock.calls.every(([, options]) => options?.credentials === 'same-origin')).toBe(true)
  })

  it('rejects mismatched passwords without sending a request', async () => {
    await open('/register')
    await wrapper!.get('#register-email').setValue(credentials.email)
    await wrapper!.get('#register-password').setValue(credentials.password)
    await wrapper!.get('#register-confirmation').setValue('inne długie hasło')
    await wrapper!.get('form').trigger('submit')
    expect(wrapper!.get('#register-error').text()).toContain('Hasła muszą być identyczne')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it.each([
    [401, 'invalid_credentials', 'Niepoprawny email lub hasło'],
    [429, 'rate_limited', 'Zbyt wiele prób'],
    [503, 'server_error', 'Nie udało się wykonać operacji'],
  ])('shows a recoverable login failure for HTTP %i without retrying POST', async (status, code, message) => {
    fetchMock.mockResolvedValueOnce(response(200, { requestToken: 'token' }))
      .mockResolvedValueOnce(response(status, { code }))
    const router = await open('/login')
    await fillLogin()
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper!.get('#login-error').text()).toContain(message)
    expect(router.currentRoute.value.name).toBe('login')
    expect(fetchMock.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1)
    expect(wrapper!.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
    expect((wrapper!.get('#login-password').element as HTMLInputElement).value).toBe('')
  })

  it('disables duplicate submission while login is pending', async () => {
    let finish!: (value: Response) => void
    fetchMock.mockResolvedValueOnce(response(200, { requestToken: 'token' }))
      .mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    await open('/login')
    await fillLogin()
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper!.get('button[type="submit"]').attributes('disabled')).toBeDefined()
    await wrapper!.get('form').trigger('submit')
    expect(fetchMock).toHaveBeenCalledTimes(2)
    finish(response(200, account))
    await flushPromises()
  })

  it.each([503, 401, 204])('confirms logout only for 204 or expired session, status %i', async status => {
    fetchMock.mockResolvedValueOnce(response(200, account))
      .mockResolvedValueOnce(response(200, { requestToken: 'token' }))
      .mockResolvedValueOnce(response(status, status === 204 ? undefined : { code: 'server_error' }))
    const router = await open('/app')
    await wrapper!.get('button').trigger('click')
    await flushPromises()
    const { session } = await import('@/auth/session')
    if (status === 503) {
      expect(router.currentRoute.value.path).toBe('/app')
      expect(session.state.user?.email).toBe(account.email)
      expect(wrapper!.get('[role="alert"]').text()).toContain('Spróbuj ponownie')
    } else {
      expect(router.currentRoute.value.path).toBe('/login')
      expect(session.state.user).toBeNull()
      expect(wrapper!.find('.account-home').exists()).toBe(false)
    }
  })

  it('explains HTTPS in the form and disables submission on an HTTP origin', async () => {
    const api = await import('@/api/auth')
    vi.spyOn(api, 'isSecureOrigin').mockReturnValue(false)
    await open('/login')
    expect(wrapper!.get('#https-warning').text()).toContain('HTTPS')
    expect(wrapper!.get('button[type="submit"]').attributes('disabled')).toBeDefined()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('blocks credentials before any network request from HTTP, even if called directly', async () => {
    const { createAuthApi } = await import('@/api/auth')
    vi.stubGlobal('window', { location: { protocol: 'http:' } })
    const api = createAuthApi(() => {})
    await expect(api.login(credentials)).rejects.toMatchObject({ code: 'https_required' })
    await expect(api.register(credentials)).rejects.toMatchObject({ code: 'https_required' })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
