import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AppErrorBoundary from '../../src/components/common/AppErrorBoundary'
import { AuthProvider } from '../../src/context/AuthContext'
import { useAuth } from '../../src/context/useAuth'
import { logoutSession } from '../../src/services/apiClient'
import { getAccessToken, setAccessToken } from '../../src/utils/authTokenStore'

function LogoutProbe() {
  const { isAuthenticated, authMessage, logout } = useAuth()
  return (
    <div>
      <span data-testid="auth-state">{isAuthenticated ? 'in' : 'out'}</span>
      <span data-testid="auth-message">{authMessage ?? ''}</span>
      <button type="button" onClick={() => void logout()}>
        Sign Out
      </button>
    </div>
  )
}

function ThrowingChild({ shouldThrow }: { shouldThrow: boolean }) {
  if (shouldThrow) {
    throw new Error('Synthetic render failure with sensitive StackTraceSecret')
  }
  return <div>Healthy child content</div>
}

describe('Phase 1 Reliability: Logout & Error Boundaries', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    setAccessToken(null)
  })

  afterEach(() => {
    globalThis.fetch = originalFetch
    vi.restoreAllMocks()
    setAccessToken(null)
  })

  it('logoutSession returns ok:true on 204 and clears local access token', async () => {
    setAccessToken('jwt-active-1')
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    globalThis.fetch = fetchMock as typeof fetch

    const result = await logoutSession()

    expect(result).toEqual({ ok: true })
    expect(getAccessToken()).toBeNull()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it.each([401, 409, 500])(
    'logoutSession returns ok:false with status %i on failed server revocation and still clears token',
    async (status) => {
      setAccessToken('jwt-active-2')
      globalThis.fetch = vi.fn().mockResolvedValue(new Response(null, { status })) as typeof fetch

      const result = await logoutSession()

      expect(result).toEqual({ ok: false, status, reason: 'http-error' })
      expect(getAccessToken()).toBeNull()
    },
  )

  it('logoutSession returns ok:false network-error when fetch throws and still clears token', async () => {
    setAccessToken('jwt-active-3')
    globalThis.fetch = vi.fn().mockRejectedValue(new TypeError('Network failure')) as typeof fetch

    const result = await logoutSession()

    expect(result).toEqual({ ok: false, reason: 'network-error' })
    expect(getAccessToken()).toBeNull()
  })

  it('AuthProvider.logout warns the user when server revocation fails while clearing local session immediately', async () => {
    globalThis.fetch = vi.fn().mockImplementation((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/Auth/refresh')) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              accessToken: 'jwt-session-1',
              expiresAtUtc: new Date(Date.now() + 15 * 60_000).toISOString(),
              userId: 1,
              userName: 'staff1',
              personId: 10,
              fullName: 'Staff User',
              role: 'Staff',
            }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          ),
        )
      }
      if (url.includes('/Auth/logout')) {
        return Promise.resolve(new Response(null, { status: 500 }))
      }
      return Promise.resolve(new Response(null, { status: 404 }))
    }) as typeof fetch

    render(
      <AuthProvider>
        <LogoutProbe />
      </AuthProvider>,
    )

    await waitFor(() => {
      expect(screen.getByTestId('auth-state').textContent).toBe('in')
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Sign Out' }))
    })

    await waitFor(() => {
      expect(screen.getByTestId('auth-state').textContent).toBe('out')
      expect(screen.getByTestId('auth-message').textContent).toContain(
        'could not confirm session revocation',
      )
    })
  })

  it('AppErrorBoundary catches render errors, hides error details, and supports Try Again and Back to Dashboard', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const onBack = vi.fn()
    let shouldThrow = true

    function DynamicChild() {
      return <ThrowingChild shouldThrow={shouldThrow} />
    }

    const { rerender } = render(
      <AppErrorBoundary scope="route" resetKey="People" onBackToDashboard={onBack}>
        <DynamicChild />
      </AppErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Unable to display this page')).toBeInTheDocument()
    expect(screen.queryByText(/StackTraceSecret/)).not.toBeInTheDocument()

    shouldThrow = false
    fireEvent.click(screen.getByRole('button', { name: /Try Again/i }))
    expect(screen.getByText('Healthy child content')).toBeInTheDocument()

    shouldThrow = true
    rerender(
      <AppErrorBoundary scope="route" resetKey="Users" onBackToDashboard={onBack}>
        <DynamicChild />
      </AppErrorBoundary>,
    )
    expect(screen.getByRole('alert')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Back to Dashboard/i }))
    expect(onBack).toHaveBeenCalledTimes(1)
    expect(consoleSpy).toHaveBeenCalled()
  })
})
