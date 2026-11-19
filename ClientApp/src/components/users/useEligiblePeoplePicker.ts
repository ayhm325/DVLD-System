import { useCallback, useEffect, useState } from 'react'
import { getPeopleAvailableForUser } from '../../services/peopleService'
import { useAsyncData, useDebounce, usePagination } from '../../hooks'

interface UseEligiblePeoplePickerOptions {
  isOpen: boolean
  accessToken: string
}

export function useEligiblePeoplePicker({
  isOpen,
  accessToken,
}: UseEligiblePeoplePickerOptions) {
  const [personQuery, setPersonQuery] = useState('')
  const debouncedQuery = useDebounce(personQuery, 300)
  const [serverMeta, setServerMeta] = useState<{ totalCount?: number; totalPages?: number }>({})

  const pagination = usePagination({
    pageSize: 10,
    initialPage: 1,
    totalCount: serverMeta.totalCount,
    totalPages: serverMeta.totalPages,
  })

  const { resetPage } = pagination

  // Reset page when search or pageSize changes
  useEffect(() => {
    resetPage()
  }, [debouncedQuery, pagination.pageSize, resetPage])

  const fetchCandidates = useCallback(async () => {
    return getPeopleAvailableForUser(
      accessToken,
      pagination.page,
      pagination.pageSize,
      debouncedQuery,
      'all',
    )
  }, [accessToken, pagination.page, pagination.pageSize, debouncedQuery])

  const {
    data: candidateResult,
    loading: loadingCandidates,
    error: candidateError,
    refresh: refreshCandidates,
  } = useAsyncData(fetchCandidates, {
    enabled: isOpen && Boolean(accessToken),
    dependencies: [pagination.page, pagination.pageSize, debouncedQuery],
    defaultErrorMessage: 'Failed to load eligible people.',
    onSuccess: (data) => {
      setServerMeta({ totalCount: data.totalCount, totalPages: data.totalPages })
    },
  })

  const candidates = candidateResult?.items ?? []
  const totalCount = candidateResult?.totalCount ?? serverMeta.totalCount ?? 0
  const totalPages = candidateResult?.totalPages ?? serverMeta.totalPages ?? 1

  const resetPicker = useCallback(() => {
    setPersonQuery('')
    setServerMeta({})
  }, [])

  return {
    personQuery,
    setPersonQuery,
    debouncedQuery,
    candidates,
    loadingCandidates,
    candidateError,
    refreshCandidates,
    totalCount,
    totalPages,
    pagination,
    resetPicker,
  }
}
