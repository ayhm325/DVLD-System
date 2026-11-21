import { useCallback, useEffect, useRef, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'

export interface UseAsyncDataOptions<T> {
  /** If false, the fetcher will not execute automatically on mount or dep changes. Defaults to true. */
  enabled?: boolean
  /** Initial data before the first successful fetch. */
  initialData?: T
  /** External dependencies that trigger a refetch when changed. */
  dependencies?: readonly unknown[]
  /** Default fallback error message if the thrown error is not an Error instance. */
  defaultErrorMessage?: string
  /** Callback triggered after a successful fetch. */
  onSuccess?: (data: T) => void
  /** Callback triggered after a failed fetch. */
  onError?: (error: string) => void
}

export interface UseAsyncDataResult<T> {
  data: T
  loading: boolean
  error: string
  refresh: () => Promise<void>
  setData: Dispatch<SetStateAction<T>>
  setError: Dispatch<SetStateAction<string>>
}

function isAbortError(err: unknown): boolean {
  return (
    (err instanceof DOMException && err.name === 'AbortError') ||
    (err instanceof Error && err.name === 'AbortError')
  )
}

/**
 * Reusable asynchronous data loading hook with AbortController cancellation,
 * stale-response protection, and manual refresh/retry capabilities.
 */
export function useAsyncData<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  options: UseAsyncDataOptions<T> & { initialData: T },
): UseAsyncDataResult<T>

export function useAsyncData<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  options?: UseAsyncDataOptions<T>,
): UseAsyncDataResult<T | undefined>

export function useAsyncData<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  options: UseAsyncDataOptions<T> = {},
): UseAsyncDataResult<T | undefined> {
  const {
    enabled = true,
    initialData,
    dependencies = [],
    defaultErrorMessage = 'Failed to load data.',
    onSuccess,
    onError,
  } = options

  const [data, setData] = useState<T | undefined>(initialData)
  const [loading, setLoading] = useState<boolean>(
    Boolean(enabled && initialData === undefined),
  )
  const [error, setError] = useState<string>('')
  const [refreshIndex, setRefreshIndex] = useState<number>(0)

  const fetcherRef = useRef(fetcher)
  const onSuccessRef = useRef(onSuccess)
  const onErrorRef = useRef(onError)

  useEffect(() => {
    fetcherRef.current = fetcher
    onSuccessRef.current = onSuccess
    onErrorRef.current = onError
  })

  useEffect(() => {
    if (!enabled) {
      return
    }

    const controller = new AbortController()
    let cancelled = false

    async function load() {
      setLoading(true)
      setError('')

      try {
        const result = await fetcherRef.current(controller.signal)
        if (!cancelled && !controller.signal.aborted) {
          setData(result)
          onSuccessRef.current?.(result)
        }
      } catch (err) {
        if (!cancelled && !controller.signal.aborted && !isAbortError(err)) {
          const message =
            err instanceof Error ? err.message : defaultErrorMessage
          setError(message)
          onErrorRef.current?.(message)
        }
      } finally {
        if (!cancelled && !controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      cancelled = true
      controller.abort()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, refreshIndex, defaultErrorMessage, ...dependencies])

  const refresh = useCallback(async () => {
    setRefreshIndex((prev) => prev + 1)
  }, [])

  return {
    data,
    loading,
    error,
    refresh,
    setData,
    setError,
  }
}
