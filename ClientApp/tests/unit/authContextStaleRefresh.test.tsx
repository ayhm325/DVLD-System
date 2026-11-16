import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { AuthProvider } from '../../src/context/AuthContext'
import { useAuth } from '../../src/context/useAuth'
import type { LoginResponse } from '../../src/types/Auth'
import { getAccessToken, setAccessToken } from '../../src/utils/authTokenStore'

const dummySession: LoginResponse = {
  accessToken: 'stale-token-after-logout',
  expiresAtUtc: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
  userId: 42,
  userName: 'testuser',
  personId: 99,
  fullName: 'Test User',
  role: 'Staff',
}

function TestConsumer() {
  const { isAuthenticated, accessToken, logout } = useAuth()
  return (
    <div>
      <span data-testid="auth-status">{isAuthenticated ? 'AUTHENTICATED' : 'ANONYMOUS'}</span>
      <span data-testid="token-value">{accessToken ?? 'NONE'}</span>
      <button onClick={() => logout('Manual test logout')}>Logout</button>
    </div>
  )
}

describe('AuthContext Stale Refresh Protection', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    setAccessToken(null)
  })

  afterEach(() => {
    globalThis.fetch = originalFetch
    vi.restoreAllMocks()
  })

  it('proves that a stale delayed refresh result cannot restore React authenticated state after logout', async () => {
    let resolveRefreshFetch!: (value: Response) => void
    const refreshFetchPromise = new Promise<Response>((resolve) => {
      resolveRefreshFetch = resolve
    })

    const fetchMock = vi.fn().mockImplementation((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/Auth/refresh')) {
        return refreshFetchPromise
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve({ ok: true, status: 204 } as Response)
      }
      return Promise.reject(new Error(`Unexpected fetch to ${url}`))
    })
    globalThis.fetch = fetchMock

    // 1. Render AuthProvider with consumer. Mount triggers session restoration refresh.
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('auth-status').textContent).toBe('ANONYMOUS')
    expect(screen.getByTestId('token-value').textContent).toBe('NONE')

    // 2. User initiates logout while the initial refresh is still pending on network
    const logoutBtn = screen.getByRole('button', { name: /logout/i })
    await act(async () => {
      fireEvent.click(logoutBtn)
    })

    // 3. Delayed refresh response arrives with HTTP 200 and a valid token payload
    await act(async () => {
      resolveRefreshFetch({
        ok: true,
        status: 200,
        json: async () => dummySession,
      } as Response)
    })

    // 4. Invariant assertion: The React UI must remain strictly unauthenticated
    expect(screen.getByTestId('auth-status').textContent).toBe('ANONYMOUS')
    expect(screen.getByTestId('token-value').textContent).toBe('NONE')
    expect(getAccessToken()).toBeNull()
  })
})
