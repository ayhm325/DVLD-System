import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import type { AuthSession, AuthUser, LoginResponse } from '../types/Auth'
import {
  getAuthGeneration,
  invalidateSession,
  logoutAllSessions,
  logoutSession,
  refreshSession,
  setUnauthorizedHandler,
} from '../services/apiClient'
import { setAccessToken } from '../utils/authTokenStore'
import { purgeLegacyTokenStorage } from '../utils/sessionStorage'
import { AuthContext, type AuthContextValue } from './authContextInstance'

type AuthChannelMessage =
  | { type: 'login' }
  | { type: 'logout'; reason?: string }
  | { type: 'token-refreshed'; expiresAtUtc: string }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(null)
  const [isLoading, setIsLoading] = useState<boolean>(true)
  const [authMessage, setAuthMessage] = useState<string | null>(null)
  const channelRef = useRef<BroadcastChannel | null>(null)

  const applySession = useCallback((response: LoginResponse, expectedGeneration?: number) => {
    if (expectedGeneration !== undefined && expectedGeneration !== getAuthGeneration()) {
      return
    }

    const user: AuthUser = {
      userId: response.userId,
      userName: response.userName,
      personId: response.personId,
      fullName: response.fullName,
      role: response.role,
    }

    const nextSession: AuthSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user,
    }

    setAccessToken(nextSession.accessToken)
    setSession(nextSession)
    setAuthMessage(null)
  }, [])

  const clearLocalState = useCallback((message: string | null) => {
    invalidateSession()
    setAccessToken(null)
    setSession(null)
    setAuthMessage(message)
  }, [])

  const login = useCallback(
    (response: LoginResponse) => {
      invalidateSession()
      applySession(response)
      channelRef.current?.postMessage({ type: 'login' } satisfies AuthChannelMessage)
    },
    [applySession],
  )

  const logout = useCallback(
    async (reason?: string): Promise<void> => {
      // Initiate server revocation while the current access token is still readable,
      // then clear local state immediately and warn if server revocation fails.
      const logoutPromise = logoutSession()
      clearLocalState(reason ?? 'You have successfully signed out.')
      channelRef.current?.postMessage({
        type: 'logout',
        reason,
      } satisfies AuthChannelMessage)

      const result = await logoutPromise
      if (!result.ok) {
        setAuthMessage(
          'Signed out locally, but the server could not confirm session revocation. Please close the browser or try signing out again.',
        )
      }
    },
    [clearLocalState],
  )

  const logoutAll = useCallback(() => {
    void logoutAllSessions().finally(() => {
      clearLocalState('All sessions have been revoked. Please log in again.')
      channelRef.current?.postMessage({ type: 'logout' } satisfies AuthChannelMessage)
    })
  }, [clearLocalState])

  // Cross-tab coordination: one tab logging out clears every tab; a login in one
  // tab lets the others pick up the shared refresh cookie; token refresh updates timers.
  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') {
      return
    }

    const channel = new BroadcastChannel('dvld-auth')

    channel.onmessage = (event: MessageEvent<AuthChannelMessage>) => {
      const message = event.data

      if (message?.type === 'logout') {
        clearLocalState(
          message.reason ?? 'You have been signed out in another tab.',
        )
      }

      if (message?.type === 'login') {
        const generation = getAuthGeneration()
        void refreshSession().then((result) => {
          if (result.success) {
            applySession(result.session, generation)
          }
        })
      }

      if (message?.type === 'token-refreshed' && message.expiresAtUtc) {
        setSession((previous) => {
          if (!previous) return null
          return {
            ...previous,
            expiresAtUtc: message.expiresAtUtc,
          }
        })
      }
    }

    channelRef.current = channel

    return () => {
      channel.close()
      channelRef.current = null
    }
  }, [applySession, clearLocalState])

  // Session restoration: the access token is never persisted; the HttpOnly cookie
  // is exchanged for a fresh access token on every page load.
  useEffect(() => {
    let active = true

    purgeLegacyTokenStorage()

    const generation = getAuthGeneration()
    void refreshSession()
      .then((result) => {
        if (active && result.success) {
          applySession(result.session, generation)
        }
      })
      .finally(() => {
        if (active) {
          setIsLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [applySession])

  useEffect(() => {
    setUnauthorizedHandler((message: string) => {
      clearLocalState(message || 'Your session has expired. Please log in again.')
      channelRef.current?.postMessage({ type: 'logout' } satisfies AuthChannelMessage)
    })

    return () => {
      setUnauthorizedHandler(null)
    }
  }, [clearLocalState])

  // Silent renewal shortly before the access token expires.
  useEffect(() => {
    if (!session) {
      return
    }

    let retryTimerId: number | null = null
    let retryAttempt = 0
    let active = true

    const expiresAt = new Date(session.expiresAtUtc).getTime()

    if (!Number.isFinite(expiresAt)) {
      return
    }

    const delayMs = Math.max(
      15_000,
      Math.min(expiresAt - Date.now() - 60_000, 2_147_483_647),
    )

    const getRetryDelay = (attempt: number): number => {
      // Bounded exponential backoff: 15s -> 30s -> 60s (capped at 60s)
      if (attempt <= 0) return 15_000
      if (attempt === 1) return 30_000
      return 60_000
    }

    const executeRenewal = () => {
      const generation = getAuthGeneration()
      void refreshSession().then((result) => {
        if (!active) return

        if (result.success) {
          retryAttempt = 0
          applySession(result.session, generation)
          channelRef.current?.postMessage({
            type: 'token-refreshed',
            expiresAtUtc: result.session.expiresAtUtc,
          } satisfies AuthChannelMessage)
        } else if (result.isFatal) {
          retryAttempt = 0
          clearLocalState('Your session has expired. Please log in again.')
        } else {
          // Transient failure (network/500/429): retry with bounded backoff
          const delay = getRetryDelay(retryAttempt)
          retryAttempt++
          retryTimerId = window.setTimeout(executeRenewal, delay)
        }
      })
    }

    const timerId = window.setTimeout(executeRenewal, delayMs)

    return () => {
      active = false
      window.clearTimeout(timerId)
      if (retryTimerId !== null) {
        window.clearTimeout(retryTimerId)
      }
    }
  }, [session, applySession, clearLocalState])

  // When a tab resumes from background/sleep, check if the token is close to expiry or already expired.
  useEffect(() => {
    if (!session) {
      return
    }

    let active = true

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        const expiresAt = new Date(session.expiresAtUtc).getTime()
        if (Number.isFinite(expiresAt) && expiresAt - Date.now() <= 90_000) {
          const generation = getAuthGeneration()
          void refreshSession().then((result) => {
            if (!active) return

            if (result.success) {
              applySession(result.session, generation)
              channelRef.current?.postMessage({
                type: 'token-refreshed',
                expiresAtUtc: result.session.expiresAtUtc,
              } satisfies AuthChannelMessage)
            } else if (result.isFatal) {
              clearLocalState('Your session has expired. Please log in again.')
            }
          })
        }
      }
    }

    document.addEventListener('visibilitychange', handleVisibilityChange)
    return () => {
      active = false
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [session, applySession, clearLocalState])

  const clearAuthMessage = useCallback(() => {
    setAuthMessage(null)
  }, [])

  const updateUser = useCallback((updatedUser: Partial<AuthUser>) => {
    setSession((previous) => {
      if (!previous) return null

      return {
        ...previous,
        user: {
          ...previous.user,
          ...updatedUser,
        },
      }
    })
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      accessToken: session?.accessToken ?? null,
      user: session?.user ?? null,
      isAuthenticated: session !== null && session.accessToken.length > 0,
      isLoading,
      authMessage,
      login,
      logout,
      logoutAll,
      clearAuthMessage,
      updateUser,
    }),
    [
      session,
      isLoading,
      authMessage,
      login,
      logout,
      logoutAll,
      clearAuthMessage,
      updateUser,
    ],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
