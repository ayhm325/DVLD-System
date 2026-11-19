import { useCallback, useMemo, useState } from 'react'
import { useAsyncData } from '../../../hooks'
import { getLocalDrivingLicenseApplicationById } from '../../../services/localDrivingLicenseApplicationService'
import {
  cancelTestAppointment,
  getAppointmentsByLocalAppId,
  getTestByAppointmentId,
} from '../../../services/testAppointmentService'
import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import type { TestAppointment, TestResponse } from '../../../types/TestAppointment'
import {
  deriveLatestOutcomeSummary,
  findActiveAppointment,
  resolveNextTestStage,
  sortAppointmentsChronologically,
  type LatestOutcomeSummary,
} from '../utils/appointmentSorting'

interface ApplicationAppointmentsData {
  application: LocalDrivingLicenseApplication | null
  appointments: TestAppointment[]
}

export interface ActionFeedback {
  type: 'success' | 'error'
  message: string
}

export interface UseManageTestAppointmentsResult {
  application: LocalDrivingLicenseApplication | null
  appointments: TestAppointment[]
  activeAppointment: TestAppointment | null
  nextStage: 1 | 2 | 3
  allStagesPassed: boolean
  latestOutcome: LatestOutcomeSummary
  loading: boolean
  error: string
  actionFeedback: ActionFeedback | null
  expandedAppointmentId: number | null
  testDetailsMap: Record<number, TestResponse | null>
  loadingTestId: number | null
  appointmentToCancel: TestAppointment | null
  cancelling: boolean
  refresh: () => Promise<void>
  dismissError: () => void
  dismissFeedback: () => void
  toggleTestDetails: (appointmentId: number) => void
  closeTestDetails: () => void
  requestCancel: (appointment: TestAppointment) => void
  dismissCancel: () => void
  confirmCancel: () => Promise<void>
}

const INITIAL_APPOINTMENTS_DATA: ApplicationAppointmentsData = {
  application: null,
  appointments: [],
}

/**
 * Coordinates data fetching, chronological sorting, derived exam stage status,
 * lazy cached observation lookup, and appointment cancellation workflow for a single application.
 */
export function useManageTestAppointments(
  accessToken: string,
  localAppId: number | null,
): UseManageTestAppointmentsResult {
  const [actionFeedback, setActionFeedback] = useState<ActionFeedback | null>(null)

  // Expandable test details (examiner observations)
  const [expandedAppointmentId, setExpandedAppointmentId] = useState<number | null>(null)
  const [testDetailsMap, setTestDetailsMap] = useState<Record<number, TestResponse | null>>({})
  const [loadingTestId, setLoadingTestId] = useState<number | null>(null)

  // Cancellation modal
  const [appointmentToCancel, setAppointmentToCancel] = useState<TestAppointment | null>(null)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)

  // Data fetcher for application context and ALL test appointments
  const fetcher = useCallback(async (): Promise<ApplicationAppointmentsData> => {
    if (!localAppId || localAppId <= 0) {
      throw new Error('A valid local driving license application ID is required.')
    }

    const [appData, allAppts] = await Promise.all([
      getLocalDrivingLicenseApplicationById(accessToken, localAppId),
      getAppointmentsByLocalAppId(accessToken, localAppId),
    ])

    return {
      application: appData,
      appointments: allAppts,
    }
  }, [accessToken, localAppId])

  const {
    data,
    loading,
    error: loadError,
    setError: setLoadError,
    refresh: loadData,
  } = useAsyncData<ApplicationAppointmentsData>(fetcher, {
    enabled: Boolean(localAppId && localAppId > 0),
    initialData: INITIAL_APPOINTMENTS_DATA,
    defaultErrorMessage: `Failed to retrieve appointment records for application #${localAppId ?? ''}.`,
  })

  const application = data.application
  const rawAppointments = data.appointments
  const error = cancelError || loadError

  const appointments = useMemo(
    () => sortAppointmentsChronologically(rawAppointments),
    [rawAppointments],
  )

  const activeAppointment = useMemo(
    () => findActiveAppointment(appointments),
    [appointments],
  )

  const nextStage = useMemo(
    () => resolveNextTestStage(application?.passedTest),
    [application?.passedTest],
  )

  const allStagesPassed = Boolean(application && application.passedTest >= 3)

  const latestOutcome = useMemo(
    () => deriveLatestOutcomeSummary(appointments),
    [appointments],
  )

  const toggleTestDetails = useCallback(
    (appointmentId: number) => {
      if (expandedAppointmentId === appointmentId) {
        setExpandedAppointmentId(null)
        return
      }

      setExpandedAppointmentId(appointmentId)

      if (testDetailsMap[appointmentId] === undefined) {
        setLoadingTestId(appointmentId)
        getTestByAppointmentId(accessToken, appointmentId)
          .then((tests: TestResponse[]) => {
            setTestDetailsMap((prevMap) => ({
              ...prevMap,
              [appointmentId]: tests && tests.length > 0 ? tests[0] : null,
            }))
          })
          .catch(() => {
            setTestDetailsMap((prevMap) => ({
              ...prevMap,
              [appointmentId]: null,
            }))
          })
          .finally(() => {
            setLoadingTestId((current) =>
              current === appointmentId ? null : current,
            )
          })
      }
    },
    [accessToken, expandedAppointmentId, testDetailsMap],
  )

  const closeTestDetails = useCallback(() => {
    setExpandedAppointmentId(null)
  }, [])

  const requestCancel = useCallback((appointment: TestAppointment) => {
    setCancelError(null)
    setAppointmentToCancel(appointment)
  }, [])

  const dismissCancel = useCallback(() => {
    setAppointmentToCancel(null)
    setCancelError(null)
  }, [])

  const confirmCancel = useCallback(async () => {
    if (!appointmentToCancel) return

    setCancelling(true)
    setCancelError(null)

    try {
      await cancelTestAppointment(accessToken, appointmentToCancel.testAppointmentId)
      setActionFeedback({
        type: 'success',
        message: `Appointment #${appointmentToCancel.testAppointmentId} for ${appointmentToCancel.testTypeName} has been successfully cancelled.`,
      })
      setAppointmentToCancel(null)
      await loadData()
    } catch (err) {
      setCancelError(
        err instanceof Error ? err.message : 'Failed to cancel appointment.',
      )
    } finally {
      setCancelling(false)
    }
  }, [accessToken, appointmentToCancel, loadData])

  const dismissError = useCallback(() => {
    setCancelError(null)
    setLoadError('')
  }, [setLoadError])

  const dismissFeedback = useCallback(() => {
    setActionFeedback(null)
  }, [])

  return {
    application,
    appointments,
    activeAppointment,
    nextStage,
    allStagesPassed,
    latestOutcome,
    loading,
    error,
    actionFeedback,
    expandedAppointmentId,
    testDetailsMap,
    loadingTestId,
    appointmentToCancel,
    cancelling,
    refresh: loadData,
    dismissError,
    dismissFeedback,
    toggleTestDetails,
    closeTestDetails,
    requestCancel,
    dismissCancel,
    confirmCancel,
  }
}
