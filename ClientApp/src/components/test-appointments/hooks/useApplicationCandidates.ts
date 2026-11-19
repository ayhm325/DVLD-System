import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAsyncData } from '../../../hooks'
import {
  getLocalDrivingLicenseApplicationById,
  getLocalDrivingLicenseApplications,
} from '../../../services/localDrivingLicenseApplicationService'
import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import {
  filterCandidateApplications,
  isApplicationNewStatus,
} from '../utils/appointmentEligibility'

export interface UseApplicationCandidatesParams {
  accessToken: string
  initialLocalAppId?: number | null
  selectedAppId: number | null
  onSelectInitialApp: (appId: number) => void
  onSelectDirectApp: (appId: number) => void
}

export interface UseApplicationCandidatesResult {
  candidateSearchQuery: string
  setCandidateSearchQuery: (query: string) => void
  filteredCandidates: LocalDrivingLicenseApplication[]
  hasMoreCandidates: boolean
  loadingMoreCandidates: boolean
  loadMoreCandidates: () => Promise<void>
  directLookupId: string
  setDirectLookupId: (id: string) => void
  directLookupError: string | null
  directLookupLoading: boolean
  submitDirectLookup: (e: React.FormEvent) => Promise<void>
}

const EMPTY_CANDIDATES: LocalDrivingLicenseApplication[] = []

/**
 * Manages Scenario B candidate application loading, search filtering,
 * paginated "Load More" fetching, and direct Application ID lookup.
 */
export function useApplicationCandidates({
  accessToken,
  initialLocalAppId,
  selectedAppId,
  onSelectInitialApp,
  onSelectDirectApp,
}: UseApplicationCandidatesParams): UseApplicationCandidatesResult {
  const [candidateSearchQuery, setCandidateSearchQuery] = useState('')
  const [candidatePage, setCandidatePage] = useState(1)
  const [hasMoreCandidates, setHasMoreCandidates] = useState(false)
  const [loadingMoreCandidates, setLoadingMoreCandidates] = useState(false)
  const [directLookupId, setDirectLookupId] = useState('')
  const [directLookupError, setDirectLookupError] = useState<string | null>(null)
  const [directLookupLoading, setDirectLookupLoading] = useState(false)
  const [loadedCandidates, setLoadedCandidates] = useState<
    LocalDrivingLicenseApplication[]
  >(EMPTY_CANDIDATES)

  const selectedAppIdRef = useRef(selectedAppId)
  useEffect(() => {
    selectedAppIdRef.current = selectedAppId
  }, [selectedAppId])

  const candidateFetcher = useCallback(async (): Promise<
    LocalDrivingLicenseApplication[]
  > => {
    if (initialLocalAppId !== undefined && initialLocalAppId !== null) {
      const app = await getLocalDrivingLicenseApplicationById(
        accessToken,
        initialLocalAppId,
      )
      return app ? [app] : []
    }

    const res = await getLocalDrivingLicenseApplications(accessToken, 1, 50)
    setHasMoreCandidates(1 * 50 < res.totalCount)
    return res.items
  }, [accessToken, initialLocalAppId])

  useAsyncData<LocalDrivingLicenseApplication[]>(candidateFetcher, {
    enabled: Boolean(accessToken),
    initialData: EMPTY_CANDIDATES,
    dependencies: [accessToken, initialLocalAppId],
    defaultErrorMessage: 'Could not fetch candidate application records.',
    onSuccess: (data) => {
      setLoadedCandidates(data)
      if (initialLocalAppId) {
        onSelectInitialApp(initialLocalAppId)
      } else if (data.length > 0 && selectedAppIdRef.current === null) {
        const firstNew = data.find(isApplicationNewStatus)
        if (firstNew) {
          onSelectInitialApp(firstNew.localDrivingLicenseApplicationId)
        }
      }
    },
  })

  const loadMoreCandidates = useCallback(async () => {
    if (loadingMoreCandidates || !hasMoreCandidates) return
    setLoadingMoreCandidates(true)
    try {
      const nextPage = candidatePage + 1
      const res = await getLocalDrivingLicenseApplications(
        accessToken,
        nextPage,
        50,
      )
      setLoadedCandidates((prev) => [...prev, ...res.items])
      setCandidatePage(nextPage)
      setHasMoreCandidates(nextPage * 50 < res.totalCount)
    } catch {
      // Non-critical background pagination failure
    } finally {
      setLoadingMoreCandidates(false)
    }
  }, [accessToken, candidatePage, hasMoreCandidates, loadingMoreCandidates])

  const submitDirectLookup = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault()
      const id = Number(directLookupId.trim())
      if (!id || isNaN(id)) return

      setDirectLookupLoading(true)
      setDirectLookupError(null)

      try {
        const app = await getLocalDrivingLicenseApplicationById(accessToken, id)
        if (app) {
          setLoadedCandidates((prev) => {
            const exists = prev.some(
              (x) =>
                x.localDrivingLicenseApplicationId ===
                app.localDrivingLicenseApplicationId,
            )
            return exists ? prev : [app, ...prev]
          })
          onSelectDirectApp(app.localDrivingLicenseApplicationId)
        } else {
          setDirectLookupError(`Application #${id} was not found.`)
        }
      } catch (err) {
        setDirectLookupError(
          err instanceof Error
            ? err.message
            : `Failed to load application #${id}.`,
        )
      } finally {
        setDirectLookupLoading(false)
      }
    },
    [accessToken, directLookupId, onSelectDirectApp],
  )

  const filteredCandidates = useMemo(
    () => filterCandidateApplications(loadedCandidates, candidateSearchQuery),
    [loadedCandidates, candidateSearchQuery],
  )

  return {
    candidateSearchQuery,
    setCandidateSearchQuery,
    filteredCandidates,
    hasMoreCandidates,
    loadingMoreCandidates,
    loadMoreCandidates,
    directLookupId,
    setDirectLookupId,
    directLookupError,
    directLookupLoading,
    submitDirectLookup,
  }
}
