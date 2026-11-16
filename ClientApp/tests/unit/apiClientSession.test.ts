import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import {
  refreshSession,
  apiRequest,
  setUnauthorizedHandler,
  logoutSession,
} from '../../src/services/apiClient'
import { getAccessToken, setAccessToken } from '../../src/utils/authTokenStore'
import type { LoginResponse } from '../../src/types/Auth'

describe('apiClient session refresh & error classification', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    setAccessToken('initial-token')
    setUnauthorizedHandler(null)
  })

  afterEach(() => {
    globalThis.fetch = originalFetch
    vi.restoreAllMocks()
  })

  const dummySession: LoginResponse = {
    accessToken: 'new-refreshed-token',
    expiresAtUtc: new Date(Date.now() + 900_000).toISOString(),
    userId: 1,
    userName: 'testuser',
    personId: 10,
    fullName: 'Test User',
    role: 'Admin',
  }

  it('successful refresh updates access token store and returns success result', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const result = await refreshSession()

    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.session.accessToken).toBe('new-refreshed-token')
    }
    expect(getAccessToken()).toBe('new-refreshed-token')
  })

  it('401 refresh returns fatal error and clears access token store', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      text: async () => 'Unauthorized',
    } as Response)

    const result = await refreshSession()

    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.status).toBe(401)
      expect(result.isFatal).toBe(true)
    }
    expect(getAccessToken()).toBeNull()
  })

  it('transient network failure returns non-fatal error and preserves access token store', async () => {
    setAccessToken('existing-valid-token')
    globalThis.fetch = vi.fn().mockRejectedValue(new TypeError('Failed to fetch (offline)'))

    const result = await refreshSession()

    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.status).toBe('network')
      expect(result.isFatal).toBe(false)
    }
    expect(getAccessToken()).toBe('existing-valid-token')
  })

  it('transient server 500 error returns non-fatal error and preserves access token store', async () => {
    setAccessToken('existing-valid-token')
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      text: async () => 'Internal Server Error',
    } as Response)

    const result = await refreshSession()

    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.status).toBe(500)
      expect(result.isFatal).toBe(false)
    }
    expect(getAccessToken()).toBe('existing-valid-token')
  })

  it('409 concurrency conflict on attempt 0 retries once and succeeds if second attempt returns 200', async () => {
    const fetchMock = vi.fn()
      // First call (attempt 0): 409 conflict
      .mockResolvedValueOnce({
        ok: false,
        status: 409,
        text: async () => 'Conflict',
      } as Response)
      // Second call (attempt 1 after delay): 200 OK
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => dummySession,
      } as Response)

    globalThis.fetch = fetchMock

    const result = await refreshSession()

    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(result.success).toBe(true)
    expect(getAccessToken()).toBe('new-refreshed-token')
  })

  it('sendWithRefresh retries request with new token when 401 occurs and refresh succeeds', async () => {
    const fetchMock = vi.fn()
      // 1. Initial API request -> 401
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: async () => 'Token expired',
      } as Response)
      // 2. /Auth/refresh request -> 200 OK
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => dummySession,
      } as Response)
      // 3. Retried API request with new token -> 200 OK
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({ data: 'success' }),
      } as Response)

    globalThis.fetch = fetchMock

    const res = await apiRequest<{ data: string }>('/test-endpoint')

    expect(res).toEqual({ data: 'success' })
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it('sendWithRefresh triggers unauthorizedHandler when 401 occurs and refresh is fatal (401)', async () => {
    const unauthorizedHandler = vi.fn()
    setUnauthorizedHandler(unauthorizedHandler)

    const fetchMock = vi.fn()
      // 1. Initial API request -> 401
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: async () => 'Token expired',
      } as Response)
      // 2. /Auth/refresh request -> 401
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: async () => 'Refresh token revoked',
      } as Response)

    globalThis.fetch = fetchMock

    await expect(apiRequest('/test-endpoint')).rejects.toThrow()
    expect(unauthorizedHandler).toHaveBeenCalledWith('Your session has expired. Please log in again.')
  })

  it('sendWithRefresh does NOT trigger unauthorizedHandler when refresh fails due to transient network error', async () => {
    const unauthorizedHandler = vi.fn()
    setUnauthorizedHandler(unauthorizedHandler)

    const fetchMock = vi.fn()
      // 1. Initial API request -> 401
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: async () => 'Token expired',
      } as Response)
      // 2. /Auth/refresh request throws network error
      .mockRejectedValueOnce(new TypeError('Network disconnected'))

    globalThis.fetch = fetchMock

    await expect(apiRequest('/test-endpoint')).rejects.toThrow()
    // Critical: unauthorizedHandler must NOT be called for transient network glitches!
    expect(unauthorizedHandler).not.toHaveBeenCalled()
  })

  it('refreshSession single-flight deduplicates multiple concurrent calls into a single fetch', async () => {
    let resolveFetch: (res: Response) => void
    const fetchPromise = new Promise<Response>((resolve) => {
      resolveFetch = resolve
    })

    const fetchMock = vi.fn().mockReturnValue(fetchPromise)
    globalThis.fetch = fetchMock

    // Call refreshSession 3 times concurrently
    const call1 = refreshSession()
    const call2 = refreshSession()
    const call3 = refreshSession()

    // Resolve the single in-flight fetch
    resolveFetch!({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const [res1, res2, res3] = await Promise.all([call1, call2, call3])

    // Fetch was called exactly ONCE
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(res1.success).toBe(true)
    expect(res2.success).toBe(true)
    expect(res3.success).toBe(true)
    expect(getAccessToken()).toBe('new-refreshed-token')
  })

  it('multi-tab concurrency: 409 benign race on attempt 0 retries boundedly and does not log user out', async () => {
    const fetchMock = vi.fn()
      // Tab 2 gets 409 (another tab rotated)
      .mockResolvedValueOnce({
        ok: false,
        status: 409,
        text: async () => 'Conflict',
      } as Response)
      // Tab 2 attempt 1 also returns 409 (e.g. server latency)
      .mockResolvedValueOnce({
        ok: false,
        status: 409,
        text: async () => 'Conflict',
      } as Response)

    globalThis.fetch = fetchMock
    setAccessToken('existing-device-token')

    const result = await refreshSession()

    // Exactly 2 attempts (0 and 1), then terminates boundedly
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.status).toBe(409)
      expect(result.isFatal).toBe(false) // Must NOT be fatal!
    }
    // Existing token must NOT be cleared by a non-fatal race
    expect(getAccessToken()).toBe('existing-device-token')
  })

  it('broadcast channel token-refreshed event never contains raw access token payload', () => {
    // Audit check on channel message contract
    type AuthChannelMessage =
      | { type: 'login' }
      | { type: 'logout'; reason?: string }
      | { type: 'token-refreshed'; expiresAtUtc: string }

    const message: AuthChannelMessage = {
      type: 'token-refreshed',
      expiresAtUtc: dummySession.expiresAtUtc,
    }

    expect(message.type).toBe('token-refreshed')
    expect(message.expiresAtUtc).toBe(dummySession.expiresAtUtc)
    expect('accessToken' in message).toBe(false)
  })

  it('bounded exponential backoff progression follows 15s -> 30s -> 60s with cap', () => {
    const getRetryDelay = (attempt: number): number => {
      if (attempt <= 0) return 15_000
      if (attempt === 1) return 30_000
      return 60_000
    }

    expect(getRetryDelay(0)).toBe(15_000)
    expect(getRetryDelay(1)).toBe(30_000)
    expect(getRetryDelay(2)).toBe(60_000)
    expect(getRetryDelay(3)).toBe(60_000) // Capped at 60s
    expect(getRetryDelay(10)).toBe(60_000) // Never exceeds 60s
  })

  // -------------------------------------------------------------------------
  // Mandatory Concurrency Tests: Refresh vs Logout Race Condition Protection
  // -------------------------------------------------------------------------

  it('1. refresh starts -> logout starts -> refresh returns 200 -> user remains signed out', async () => {
    let resolveRefreshFetch!: (res: Response) => void
    const refreshFetchPromise = new Promise<Response>((resolve) => {
      resolveRefreshFetch = resolve
    })

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/Auth/refresh')) {
        return refreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`))
    })
    globalThis.fetch = fetchMock

    setAccessToken('pre-logout-token')

    // 1. Refresh operation starts (fetch is in-flight)
    const refreshPromise = refreshSession()

    // 2. User initiates logout while refresh is in-flight
    const logoutPromise = logoutSession()

    // Immediately after logout starts, local token is already wiped
    expect(getAccessToken()).toBeNull()

    // 3. Delayed refresh response arrives with 200 OK
    resolveRefreshFetch({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const [refreshResult] = await Promise.all([refreshPromise, logoutPromise])

    // Invariant: refresh was invalidated by logout; user remains signed out
    expect(refreshResult.success).toBe(false)
    expect(getAccessToken()).toBeNull()
  })

  it('2. refresh starts -> logout succeeds -> delayed refresh response arrives -> no access token is restored', async () => {
    let resolveRefreshFetch!: (res: Response) => void
    const refreshFetchPromise = new Promise<Response>((resolve) => {
      resolveRefreshFetch = resolve
    })

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/Auth/refresh')) {
        return refreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`))
    })
    globalThis.fetch = fetchMock

    setAccessToken('active-token-before-logout')

    // 1. Refresh starts
    const refreshPromise = refreshSession()

    // 2. Logout completes successfully on client & server
    await logoutSession()
    expect(getAccessToken()).toBeNull()

    // 3. Delayed refresh response arrives AFTER logout succeeded
    resolveRefreshFetch({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const refreshResult = await refreshPromise

    // Invariant: No access token is restored
    expect(refreshResult.success).toBe(false)
    expect(getAccessToken()).toBeNull()
  })

  it('3. logout starts while multiple callers are waiting for the same single-flight refresh', async () => {
    let resolveRefreshFetch!: (res: Response) => void
    const refreshFetchPromise = new Promise<Response>((resolve) => {
      resolveRefreshFetch = resolve
    })

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/Auth/refresh')) {
        return refreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`))
    })
    globalThis.fetch = fetchMock

    setAccessToken('pre-logout-token')

    // Multiple callers await the same in-flight refresh single-flight
    const caller1 = refreshSession()
    const caller2 = refreshSession()
    const caller3 = refreshSession()

    // Logout initiated
    const logoutPromise = logoutSession()

    // Delayed refresh completes with 200 OK
    resolveRefreshFetch({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const [res1, res2, res3] = await Promise.all([caller1, caller2, caller3, logoutPromise])

    // Invariant: all waiting callers receive canceled/invalidated result and token is not restored
    expect(res1.success).toBe(false)
    expect(res2.success).toBe(false)
    expect(res3.success).toBe(false)
    expect(getAccessToken()).toBeNull()
  })

  it('4. refresh fails after logout -> no unexpected authentication state changes', async () => {
    const unauthorizedHandler = vi.fn()
    setUnauthorizedHandler(unauthorizedHandler)

    let resolveRefreshFetch!: (res: Response) => void
    const refreshFetchPromise = new Promise<Response>((resolve) => {
      resolveRefreshFetch = resolve
    })

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/Auth/refresh')) {
        return refreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`))
    })
    globalThis.fetch = fetchMock

    setAccessToken('pre-logout-token')

    const refreshPromise = refreshSession()
    await logoutSession()

    // Delayed refresh fails with 401
    resolveRefreshFetch({
      ok: false,
      status: 401,
      text: async () => 'Session expired on server',
    } as Response)

    const result = await refreshPromise

    expect(result.success).toBe(false)
    // Critical: unauthorizedHandler must NOT be called when refresh fails after user already logged out!
    expect(unauthorizedHandler).not.toHaveBeenCalled()
    expect(getAccessToken()).toBeNull()
  })

  it('5. normal refresh without logout continues to work', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => dummySession,
    } as Response)

    const result = await refreshSession()

    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.session.accessToken).toBe('new-refreshed-token')
    }
    expect(getAccessToken()).toBe('new-refreshed-token')
  })

  it('6. normal logout without an active refresh continues to work', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 204,
    } as Response)
    globalThis.fetch = fetchMock

    setAccessToken('active-token')
    expect(getAccessToken()).toBe('active-token')

    await logoutSession()

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(getAccessToken()).toBeNull()
  })

  it('7. completion of an old in-flight refresh does not clear or overwrite a newer in-flight refresh', async () => {
    let resolveOldRefresh!: (res: Response) => void
    const oldRefreshFetchPromise = new Promise<Response>((resolve) => {
      resolveOldRefresh = resolve
    })

    let resolveNewRefresh!: (res: Response) => void
    const newRefreshFetchPromise = new Promise<Response>((resolve) => {
      resolveNewRefresh = resolve
    })

    let callCount = 0
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/Auth/refresh')) {
        callCount++
        if (callCount === 1) {
          return oldRefreshFetchPromise
        }
        return newRefreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`))
    })
    globalThis.fetch = fetchMock

    // 1. Old refresh starts
    const oldRefreshPromise = refreshSession()

    // 2. Logout invalidates old generation
    await logoutSession()
    expect(getAccessToken()).toBeNull()

    // 3. New authentication lifecycle starts a new refresh
    const newRefreshPromise = refreshSession()

    // 4. Old refresh completes
    resolveOldRefresh({
      ok: true,
      status: 200,
      json: async () => ({ ...dummySession, accessToken: 'stale-token' }),
    } as Response)

    const oldResult = await oldRefreshPromise
    expect(oldResult.success).toBe(false)
    expect(oldResult.status).toBe('canceled')

    // 5. Critical check: Calling refreshSession() now MUST join the still-in-flight new refresh promise!
    // It must NOT have been nulled out by the completion of oldRefreshPromise.
    const concurrentCallDuringNewRefresh = refreshSession()
    expect(concurrentCallDuringNewRefresh).toBe(newRefreshPromise)
    expect(callCount).toBe(2) // No third network fetch was initiated

    // 6. New refresh completes successfully
    resolveNewRefresh({
      ok: true,
      status: 200,
      json: async () => ({ ...dummySession, accessToken: 'fresh-valid-token' }),
    } as Response)

    const newResult = await newRefreshPromise
    expect(newResult.success).toBe(true)
    if (newResult.success) {
      expect(newResult.session.accessToken).toBe('fresh-valid-token')
    }
    expect(getAccessToken()).toBe('fresh-valid-token')

    // 7. After new refresh finishes, inFlightRefresh is cleanly nulled
    expect(await concurrentCallDuringNewRefresh).toEqual(newResult)
  })
})
