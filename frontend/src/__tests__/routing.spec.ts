import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory } from 'vue-router'

let wrapper: VueWrapper | undefined
let fetchMock: ReturnType<typeof vi.fn<typeof fetch>>
const account = { userId: 'owner-a', email: 'anna@example.test' }
function response(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), { status })
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

describe('private routing', () => {
  it('never renders private identity before me resolves and coalesces concurrent session reads', async () => {
    let finish!: (value: Response) => void
    fetchMock.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    const { createAppRouter } = await import('@/router')
    const { session } = await import('@/auth/session')
    const { default: App } = await import('@/App.vue')
    const router = createAppRouter(createMemoryHistory())
    const navigation = router.push('/app')
    await flushPromises()
    const read = session.ensureUser()
    wrapper = mount(App, { global: { plugins: [router] } })
    expect(wrapper.text()).not.toContain(account.email)
    expect(wrapper.find('.account-home').exists()).toBe(false)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    finish(response(200, account))
    await Promise.all([navigation, read])
    await flushPromises()
    expect(wrapper.text()).toContain(account.email)
  })

  it('redirects an anonymous visitor to login with the original local URL', async () => {
    fetchMock.mockResolvedValueOnce(response(401))
    const { createAppRouter } = await import('@/router')
    const router = createAppRouter(createMemoryHistory())
    await router.push('/app?tab=history#today')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.returnUrl).toBe('/app?tab=history#today')
    const { session } = await import('@/auth/session')
    expect(session.state.status).toBe('anonymous')
    expect(session.state.user).toBeNull()
  })

  it.each(['network', 'server'])('offers retry for %s failure without declaring logout', async failure => {
    if (failure === 'network') fetchMock.mockRejectedValueOnce(new TypeError('offline'))
    else fetchMock.mockResolvedValueOnce(response(503))
    fetchMock.mockResolvedValueOnce(response(200, account))
    const { createAppRouter } = await import('@/router')
    const { session } = await import('@/auth/session')
    const { default: App } = await import('@/App.vue')
    const router = createAppRouter(createMemoryHistory())
    await router.push('/app?tab=history')
    wrapper = mount(App, { global: { plugins: [router] } })
    expect(session.state.status).toBe('error')
    expect(wrapper.text()).toContain('Nie udało się sprawdzić sesji')
    expect(wrapper.find('.account-home').exists()).toBe(false)
    await wrapper.get('button.secondary').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/app?tab=history')
    expect(wrapper.text()).toContain(account.email)
  })

  it('accepts an existing private return destination', async () => {
    const { createAppRouter, safeReturnUrl } = await import('@/router')
    const router = createAppRouter(createMemoryHistory())
    expect(safeReturnUrl('/app?tab=history#today', router)).toBe('/app?tab=history#today')
  })

  it.each([
    'https://evil.test/app', '//evil.test/app', '/\\evil.test/app',
    '%2f%2fevil.test', '/%2f%2fevil.test', '/%252f%252fevil.test',
    '/%5cevil.test', '/%255cevil.test', '/app%0a', '/login', '/register',
    '/unknown', '/', ['//evil.test'], undefined,
  ])('rejects unsafe or nonprivate return URL %j', async destination => {
    const { createAppRouter, safeReturnUrl } = await import('@/router')
    const router = createAppRouter(createMemoryHistory())
    expect(safeReturnUrl(destination, router)).toBe('/app')
  })
})
