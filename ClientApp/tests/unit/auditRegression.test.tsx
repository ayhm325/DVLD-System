import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  apiRequest,
  invalidateSession,
  logoutAllSessions,
  setUnauthorizedHandler,
} from '../../src/services/apiClient'
import { setAccessToken } from '../../src/utils/authTokenStore'
import { useAsyncData } from '../../src/hooks/useAsyncData'
import { useTableFilters } from '../../src/hooks/useTableFilters'

describe('Frontend Audit Regression Tests', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    invalidateSession()
    setUnauthorizedHandler(null)
  })

  it('does not trigger /Auth/refresh or unauthorizedHandler when POST /Auth/login returns 401', async () => {
    const unauthorizedSpy = vi.fn()
    setUnauthorizedHandler(unauthorizedSpy)

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ message: 'Invalid credentials' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(
      apiRequest('/Auth/login', undefined, {
        method: 'POST',
        body: JSON.stringify({ userName: 'ayhm', password: 'wrong' }),
      }),
    ).rejects.toThrow()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(unauthorizedSpy).not.toHaveBeenCalled()
  })

  it('logoutAllSessions does not trigger /Auth/refresh on 401 response', async () => {
    setAccessToken('expired-token')

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(null, { status: 401 }),
    )

    await logoutAllSessions()

    expect(fetchSpy).toHaveBeenCalledTimes(1)
    expect(String(fetchSpy.mock.calls[0]?.[0])).toContain('/Auth/logout-all')
  })

  it('preserves AbortError instead of masking it as Network error when caller signal aborts', async () => {
    const controller = new AbortController()
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      (_input, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => {
            reject(new DOMException('The operation was aborted.', 'AbortError'))
          })
        }),
    )

    const promise = apiRequest('/People', 'token', { signal: controller.signal })
    controller.abort()

    await expect(promise).rejects.toMatchObject({ name: 'AbortError' })
  })

  it('useAsyncData.refresh waits until the refetch completes and clears loading when disabled', async () => {
    let callCount = 0
    let resolveSecond: ((value: string) => void) | null = null

    const fetcher = vi.fn(() => {
      callCount += 1
      if (callCount === 1) {
        return Promise.resolve('initial')
      }
      return new Promise<string>((resolve) => {
        resolveSecond = resolve
      })
    })

    const { result, rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) =>
        useAsyncData(fetcher, { enabled, initialData: '' }),
      { initialProps: { enabled: true } },
    )

    await waitFor(() => {
      expect(result.current.data).toBe('initial')
      expect(result.current.loading).toBe(false)
    })

    let refreshResolved = false
    let refreshPromise: Promise<void> | undefined
    act(() => {
      refreshPromise = result.current.refresh().then(() => {
        refreshResolved = true
      })
    })

    await waitFor(() => {
      expect(callCount).toBe(2)
    })
    expect(refreshResolved).toBe(false)

    await act(async () => {
      resolveSecond?.('updated')
      await refreshPromise
    })

    expect(refreshResolved).toBe(true)
    expect(result.current.data).toBe('updated')

    rerender({ enabled: false })
    expect(result.current.loading).toBe(false)
  })

  it('useTableFilters keeps resetFilters stable across renders when initialFilters is omitted', () => {
    const data = [{ id: 1, name: 'Ahmad' }]
    const filterItem = (item: { id: number; name: string }, query: string) =>
      item.name.toLowerCase().includes(query)

    const { result, rerender } = renderHook(() =>
      useTableFilters({ data, filterItem }),
    )

    const initialReset = result.current.resetFilters
    rerender()
    expect(result.current.resetFilters).toBe(initialReset)
  })
})
