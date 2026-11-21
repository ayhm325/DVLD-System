import { getAccessToken, setAccessToken } from '../utils/authTokenStore'
import type { LoginResponse } from '../types/Auth'

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '')

const CSRF_HEADER = 'X-CSRF-Protection'

export type ApiFieldErrors = Record<string, string[]>

type ApiErrorBody = {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[] | string>
  message?: string
  error?: string
}

type UnauthorizedHandler = (message: string) => void

export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail: string
  readonly fieldErrors: ApiFieldErrors
  readonly traceId?: string

  constructor(
    status: number,
    title: string,
    detail: string,
    fieldErrors: ApiFieldErrors = {},
    traceId?: string,
  ) {
    super(detail || title || `Request failed (${status})`)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    this.fieldErrors = fieldErrors
    this.traceId = traceId
  }

  get hasFieldErrors(): boolean {
    return Object.keys(this.fieldErrors).length > 0
  }

  get fieldMessages(): string[] {
    return Object.values(this.fieldErrors).flat()
  }
}

export class UnauthorizedError extends ApiError {
  constructor(
    message = 'Your session has expired. Please log in again.',
    fieldErrors: ApiFieldErrors = {},
    traceId?: string,
  ) {
    super(401, 'Unauthorized', message, fieldErrors, traceId)
    this.name = 'UnauthorizedError'
  }
}

export class ForbiddenError extends ApiError {
  constructor(
    message = 'You do not have permission to access this page.',
    fieldErrors: ApiFieldErrors = {},
    traceId?: string,
  ) {
    super(403, 'Forbidden', message, fieldErrors, traceId)
    this.name = 'ForbiddenError'
  }
}

let unauthorizedHandler: UnauthorizedHandler | null = null

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

function normalizeFieldErrors(errors: ApiErrorBody['errors']): ApiFieldErrors {
  if (!errors) return {}

  const normalized: ApiFieldErrors = {}

  for (const [key, value] of Object.entries(errors)) {
    const messages = (Array.isArray(value) ? value : [value]).filter(
      (message): message is string =>
        typeof message === 'string' && message.trim().length > 0,
    )

    if (messages.length > 0) {
      normalized[key] = messages
    }
  }

  return normalized
}

async function readApiError(response: Response): Promise<ApiError> {
  const fallback = `Request failed (${response.status})`
  const text = await response.text()

  if (!text) {
    return new ApiError(response.status, fallback, fallback)
  }

  let body: ApiErrorBody

  try {
    body = JSON.parse(text) as ApiErrorBody
  } catch {
    return new ApiError(response.status, fallback, text.trim() || fallback)
  }

  const fieldErrors = normalizeFieldErrors(body.errors)
  const detail = typeof body.detail === 'string' ? body.detail.trim() : ''
  const title = typeof body.title === 'string' ? body.title.trim() : ''
  const legacyMessage =
    typeof body.error === 'string'
      ? body.error
      : typeof body.message === 'string'
        ? body.message
        : ''
  const fieldMessage = Object.values(fieldErrors).flat().join(' ')
  const traceId = typeof body.traceId === 'string' ? body.traceId : undefined

  const message = detail || fieldMessage || legacyMessage || title || fallback

  return new ApiError(
    response.status,
    title || fallback,
    message,
    fieldErrors,
    traceId,
  )
}

async function throwApiError(response: Response): Promise<never> {
  const apiError = await readApiError(response)

  if (response.status === 401) {
    throw new UnauthorizedError(
      apiError.message,
      apiError.fieldErrors,
      apiError.traceId,
    )
  }

  if (response.status === 403) {
    throw new ForbiddenError(
      apiError.message,
      apiError.fieldErrors,
      apiError.traceId,
    )
  }

  throw apiError
}

export type RefreshResult =
  | { success: true; session: LoginResponse }
  | { success: false; status: number | 'network' | 'canceled'; isFatal: boolean }

// ---------------------------------------------------------------------------
// Refresh single-flight & Generation coordination
//
// Every 401 in this browser context shares one refresh request. A generation
// counter tracks active session lifecycles: whenever logout or local session
// invalidation occurs, the generation is incremented. In-flight refresh
// requests that complete after invalidation are dropped immediately, preventing
// stale responses from resurrecting authenticated state or overwriting tokens.
// ---------------------------------------------------------------------------

let inFlightRefresh: Promise<RefreshResult> | null = null
let authGeneration = 0

export function getAuthGeneration(): number {
  return authGeneration
}

export function invalidateSession(): void {
  authGeneration++
  inFlightRefresh = null
  setAccessToken(null)
}

export function refreshSession(): Promise<RefreshResult> {
  if (!inFlightRefresh) {
    const expectedGeneration = authGeneration
    const promise: Promise<RefreshResult> = performRefresh(0, expectedGeneration).finally(() => {
      if (inFlightRefresh === promise) {
        inFlightRefresh = null
      }
    })
    inFlightRefresh = promise
  }

  return inFlightRefresh
}

async function performRefresh(
  attempt: number,
  expectedGeneration: number,
): Promise<RefreshResult> {
  try {
    const response = await fetch(`${API_BASE_URL}/Auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        [CSRF_HEADER]: '1',
      },
    })

    if (expectedGeneration !== authGeneration) {
      return { success: false, status: 'canceled', isFatal: false }
    }

    if (response.status === 409 && attempt === 0) {
      // A concurrent tab/request won the rotation. Its Set-Cookie may already be
      // applied; retry once with the current cookie before giving up.
      await delay(250)
      if (expectedGeneration !== authGeneration) {
        return { success: false, status: 'canceled', isFatal: false }
      }
      return performRefresh(1, expectedGeneration)
    }

    if (!response.ok) {
      if (expectedGeneration !== authGeneration) {
        return { success: false, status: 'canceled', isFatal: false }
      }

      if (response.status === 401) {
        setAccessToken(null)
        return { success: false, status: 401, isFatal: true }
      }

      // Non-fatal failures (409, 429 rate limit, 500 server error, etc.)
      return { success: false, status: response.status, isFatal: false }
    }

    if (expectedGeneration !== authGeneration) {
      return { success: false, status: 'canceled', isFatal: false }
    }

    const session = (await response.json()) as LoginResponse

    if (expectedGeneration !== authGeneration) {
      return { success: false, status: 'canceled', isFatal: false }
    }

    setAccessToken(session.accessToken)
    return { success: true, session }
  } catch {
    if (expectedGeneration !== authGeneration) {
      return { success: false, status: 'canceled', isFatal: false }
    }
    // Network / fetch error (offline, DNS failure, aborted)
    return { success: false, status: 'network', isFatal: false }
  }
}

export type LogoutResult =
  | { ok: true }
  | { ok: false; status?: number; reason: 'http-error' | 'network-error' }

export async function logoutSession(): Promise<LogoutResult> {
  const currentToken = getAccessToken()
  invalidateSession()
  try {
    const headers = buildHeaders(
      {
        headers: { [CSRF_HEADER]: '1' },
      },
      currentToken ?? undefined,
    )
    const response = await fetch(`${API_BASE_URL}/Auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers,
    })

    if (!response.ok) {
      return { ok: false, status: response.status, reason: 'http-error' }
    }

    return { ok: true }
  } catch {
    return { ok: false, reason: 'network-error' }
  } finally {
    setAccessToken(null)
  }
}

export async function logoutAllSessions(): Promise<void> {
  const currentToken = getAccessToken()
  invalidateSession()
  try {
    const headers = buildHeaders(
      {
        headers: { [CSRF_HEADER]: '1' },
      },
      currentToken ?? undefined,
    )
    await fetch(`${API_BASE_URL}/Auth/logout-all`, {
      method: 'POST',
      credentials: 'include',
      headers,
    })
  } catch {
    // The local session is cleared regardless of the server response.
  } finally {
    setAccessToken(null)
  }
}

function buildHeaders(init: RequestInit, token?: string): Headers {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')

  const effectiveToken = token ?? getAccessToken()

  if (effectiveToken) {
    headers.set('Authorization', `Bearer ${effectiveToken}`)
  }

  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  return headers
}

const DEFAULT_REQUEST_TIMEOUT_MS = 30_000

function isAuthLifecyclePath(normalizedPath: string): boolean {
  const lower = normalizedPath.toLowerCase()
  return (
    lower === '/auth/login' ||
    lower === '/auth/refresh' ||
    lower === '/auth/logout' ||
    lower === '/auth/logout-all'
  )
}

function isAbortError(err: unknown): boolean {
  return (
    (err instanceof DOMException && err.name === 'AbortError') ||
    (err instanceof Error && err.name === 'AbortError')
  )
}

function createRequestSignal(callerSignal?: AbortSignal | null): {
  signal: AbortSignal | undefined
  cleanup: () => void
} {
  if (typeof AbortController === 'undefined') {
    return { signal: callerSignal ?? undefined, cleanup: () => {} }
  }

  const controller = new AbortController()
  const timeoutId = setTimeout(() => {
    controller.abort(new DOMException('Request timed out', 'TimeoutError'))
  }, DEFAULT_REQUEST_TIMEOUT_MS)

  const onCallerAbort = () => {
    controller.abort(callerSignal?.reason)
  }

  if (callerSignal) {
    if (callerSignal.aborted) {
      clearTimeout(timeoutId)
      controller.abort(callerSignal.reason)
      return { signal: controller.signal, cleanup: () => {} }
    }
    callerSignal.addEventListener('abort', onCallerAbort, { once: true })
  }

  return {
    signal: controller.signal,
    cleanup: () => {
      clearTimeout(timeoutId)
      callerSignal?.removeEventListener('abort', onCallerAbort)
    },
  }
}

async function send(
  path: string,
  init: RequestInit,
  token?: string,
): Promise<Response> {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  const { signal, cleanup } = createRequestSignal(init.signal)

  try {
    return await fetch(`${API_BASE_URL}${normalizedPath}`, {
      ...init,
      signal,
      credentials: 'include',
      headers: buildHeaders(init, token),
    })
  } catch (err) {
    if (init.signal?.aborted || isAbortError(err)) {
      throw err instanceof Error ? err : new DOMException('The operation was aborted.', 'AbortError')
    }

    throw new ApiError(
      0,
      'Network error',
      'Unable to connect to the DVLD API. Check that the API is running and the base URL is correct.',
    )
  } finally {
    cleanup()
  }
}

async function sendWithRefresh(
  path: string,
  init: RequestInit,
  token?: string,
): Promise<Response> {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  let response = await send(normalizedPath, init, token)

  if (response.status !== 401 || isAuthLifecyclePath(normalizedPath)) {
    return response
  }

  const refreshResult = await refreshSession()

  if (init.signal?.aborted) {
    throw new DOMException('The operation was aborted.', 'AbortError')
  }

  if (refreshResult.success) {
    // Retry the original request exactly once with the new access token.
    response = await send(normalizedPath, init, refreshResult.session.accessToken)
    return response
  }

  if (refreshResult.isFatal) {
    unauthorizedHandler?.('Your session has expired. Please log in again.')
    return response
  }

  // Non-fatal error (offline/500/429): do NOT trigger unauthorizedHandler.
  // Return the original response so the caller fails with the appropriate error
  // without terminating the user's active authentication state.
  return response
}

export async function apiRequest<T>(
  path: string,
  accessToken?: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await sendWithRefresh(path, init, accessToken)

  if (!response.ok) {
    await throwApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return undefined as T
  }

  return (await response.json()) as T
}

export async function apiBlobRequest(
  path: string,
  accessToken?: string,
  init: RequestInit = {},
): Promise<Blob> {
  const response = await sendWithRefresh(path, init, accessToken)

  if (!response.ok) {
    await throwApiError(response)
  }

  return await response.blob()
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, milliseconds)
  })
}
