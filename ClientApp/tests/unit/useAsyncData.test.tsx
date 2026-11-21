import { renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useAsyncData } from '../../src/hooks/useAsyncData'

describe('useAsyncData with AbortController', () => {
  it('passes an AbortSignal and aborts it on dependency change and unmount', async () => {
    const signals: AbortSignal[] = []
    const fetcher = vi.fn((signal: AbortSignal) => {
      signals.push(signal)
      return Promise.resolve('ok')
    })

    const { rerender, unmount } = renderHook(
      ({ dep }: { dep: number }) =>
        useAsyncData((signal) => fetcher(signal), { dependencies: [dep] }),
      { initialProps: { dep: 1 } },
    )

    await waitFor(() => {
      expect(signals).toHaveLength(1)
    })
    expect(signals[0].aborted).toBe(false)

    rerender({ dep: 2 })
    expect(signals[0].aborted).toBe(true)

    await waitFor(() => {
      expect(signals).toHaveLength(2)
    })

    unmount()
    expect(signals[1].aborted).toBe(true)
  })

  it('prevents stale slower responses from overwriting newer results and ignores AbortError', async () => {
    let resolveFirst!: (val: string) => void
    const firstPromise = new Promise<string>((r) => {
      resolveFirst = r
    })

    const fetcher = vi
      .fn<(signal: AbortSignal) => Promise<string>>()
      .mockImplementationOnce(() => firstPromise)
      .mockImplementationOnce(() => Promise.resolve('fresh-data'))

    const { result, rerender } = renderHook(
      ({ page }: { page: number }) =>
        useAsyncData((signal) => fetcher(signal), { dependencies: [page] }),
      { initialProps: { page: 1 } },
    )

    rerender({ page: 2 })

    await waitFor(() => {
      expect(result.current.data).toBe('fresh-data')
      expect(result.current.loading).toBe(false)
    })

    resolveFirst('stale-data')
    await Promise.resolve()

    expect(result.current.data).toBe('fresh-data')
    expect(result.current.error).toBe('')
  })
})
