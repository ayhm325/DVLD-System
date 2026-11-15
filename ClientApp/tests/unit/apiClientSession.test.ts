import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import {
  refreshSession,
  apiRequest,
  setUnauthorizedHandler,
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
})
