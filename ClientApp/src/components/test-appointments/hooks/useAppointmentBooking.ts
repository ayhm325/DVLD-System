import { useCallback, useMemo, useRef, useState } from 'react'
import { useAsyncData } from '../../../hooks'
import { getLocalDrivingLicenseApplicationById } from '../../../services/localDrivingLicenseApplicationService'
import {
  getAppointmentAvailability,
  getAppointmentsByLocalAppId,
  getSchedulePreparation,
  scheduleTestAppointment,
} from '../../../services/testAppointmentService'
import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import type {
  AppointmentAvailabilityResponse,
  ScheduleTestPreparation,
  TestAppointment,
} from '../../../types/TestAppointment'
import {
  formatAppointmentSubmission,
  getJordanDateString,
} from '../../../utils/jordanTime'
import type { BookedAppointmentDetails } from '../AppointmentReceiptCard'
import {
  calculateTotalBookingFees,
  evaluateAppointmentEligibility,
  resolveNextAvailabilitySelection,
  type AppointmentEligibility,
} from '../utils/appointmentEligibility'

interface BookingContextData {
  schedulePrep: ScheduleTestPreparation | null
  existingAppointments: TestAppointment[]
  availability: AppointmentAvailabilityResponse | null
  availabilityError: string | null
}

const INITIAL_BOOKING_CONTEXT: BookingContextData = {
  schedulePrep: null,
  existingAppointments: [],
  availability: null,
  availabilityError: null,
}

export interface UseAppointmentBookingParams {
  accessToken: string
  initialLocalAppId?: number | null
  initialTestTypeId?: 1 | 2 | 3
}

export interface UseAppointmentBookingResult {
  selectedAppId: number | null
  selectedTestTypeId: 1 | 2 | 3
  application: LocalDrivingLicenseApplication | null
  loadingApp: boolean
  appError: string
  refreshApp: () => Promise<void>
  schedulePrep: ScheduleTestPreparation | null
  availability: AppointmentAvailabilityResponse | null
  availabilityError: string | null
  loadingContext: boolean
  refreshBookingContext: () => Promise<void>
  selectedDate: string
  selectedSlot: string
  setSelectedDate: (date: string) => void
  setSelectedSlot: (slot: string) => void
  eligibility: AppointmentEligibility
  totalFees: number
  submitting: boolean
  submitError: string | null
  submitSuccess: boolean
  bookedDetails: BookedAppointmentDetails | null
  selectInitialApplication: (appId: number) => void
  selectApplication: (appId: number | null) => void
  selectStage: (stageId: 1 | 2 | 3) => void
  confirmSchedule: (e: React.FormEvent) => Promise<void>
  dismissSubmitError: () => void
  scheduleAnother: () => void
}

/**
 * Coordinates booking state for a selected application & test stage:
 * loads application details, scheduling preparation, existing appointments,
 * and Jordan facility availability windows, computes eligibility and total fees,
 * and handles atomic appointment submission + receipt state.
 */
export function useAppointmentBooking({
  accessToken,
  initialLocalAppId,
  initialTestTypeId = 1,
}: UseAppointmentBookingParams): UseAppointmentBookingResult {
  const [selectedAppId, setSelectedAppId] = useState<number | null>(
    initialLocalAppId ?? null,
  )
  const [selectedTestTypeId, setSelectedTestTypeId] = useState<1 | 2 | 3>(
    initialTestTypeId ?? 1,
  )

  const [selectedDate, setSelectedDateState] = useState<string>('')
  const [selectedSlot, setSelectedSlotState] = useState<string>('')
  const selectedDateRef = useRef(selectedDate)
  const selectedSlotRef = useRef(selectedSlot)

  const setSelectedDate = useCallback((date: string) => {
    selectedDateRef.current = date
    setSelectedDateState(date)
  }, [])

  const setSelectedSlot = useCallback((slot: string) => {
    selectedSlotRef.current = slot
    setSelectedSlotState(slot)
  }, [])

  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [submitSuccess, setSubmitSuccess] = useState(false)
  const [bookedDetails, setBookedDetails] =
    useState<BookedAppointmentDetails | null>(null)

  const selectInitialApplication = useCallback((appId: number) => {
    setSelectedAppId(appId)
  }, [])

  const selectApplication = useCallback(
    (appId: number | null) => {
      setSelectedAppId(appId)
      setSelectedDate('')
      setSelectedSlot('')
      setSubmitSuccess(false)
      setSubmitError(null)
    },
    [setSelectedDate, setSelectedSlot],
  )

  const selectStage = useCallback(
    (stageId: 1 | 2 | 3) => {
      setSelectedTestTypeId(stageId)
      setSelectedDate('')
      setSelectedSlot('')
      setSubmitSuccess(false)
      setSubmitError(null)
    },
    [setSelectedDate, setSelectedSlot],
  )

  // Selected Application Record
  const appFetcher = useCallback(async (): Promise<
    LocalDrivingLicenseApplication | null
  > => {
    if (selectedAppId === null) return null
    return getLocalDrivingLicenseApplicationById(accessToken, selectedAppId)
  }, [accessToken, selectedAppId])

  const {
    data: application = null,
    loading: loadingApp,
    error: appError,
    refresh: refreshApp,
  } = useAsyncData<LocalDrivingLicenseApplication | null>(appFetcher, {
    enabled: Boolean(accessToken && selectedAppId !== null),
    initialData: null,
    dependencies: [selectedAppId],
    defaultErrorMessage: `Failed to load application #${selectedAppId ?? ''}.`,
  })

  // Preparation, Existing Appointments & Availability for Stage
  const bookingContextFetcher =
    useCallback(async (): Promise<BookingContextData> => {
      if (selectedAppId === null) {
        return INITIAL_BOOKING_CONTEXT
      }

      const startDate = getJordanDateString(new Date(), 2)
      const endDate = getJordanDateString(new Date(), 30)

      const [prepRes, apptsRes, availRes] = await Promise.allSettled([
        getSchedulePreparation(accessToken, selectedAppId, selectedTestTypeId),
        getAppointmentsByLocalAppId(accessToken, selectedAppId),
        getAppointmentAvailability(
          accessToken,
          selectedAppId,
          selectedTestTypeId,
          startDate,
          endDate,
        ),
      ])

      const prep = prepRes.status === 'fulfilled' ? prepRes.value : null
      const existing =
        apptsRes.status === 'fulfilled' ? apptsRes.value ?? [] : []

      let availData: AppointmentAvailabilityResponse | null = null
      let availError: string | null = null

      if (availRes.status === 'fulfilled') {
        availData = availRes.value
      } else {
        availError =
          availRes.reason instanceof Error
            ? availRes.reason.message
            : 'Could not load appointment availability from DVLD server.'
      }

      return {
        schedulePrep: prep,
        existingAppointments: existing,
        availability: availData,
        availabilityError: availError,
      }
    }, [accessToken, selectedAppId, selectedTestTypeId])

  const {
    data: bookingContext = INITIAL_BOOKING_CONTEXT,
    loading: loadingContext,
    refresh: refreshBookingContext,
  } = useAsyncData<BookingContextData>(bookingContextFetcher, {
    enabled: Boolean(accessToken && selectedAppId !== null),
    initialData: INITIAL_BOOKING_CONTEXT,
    dependencies: [selectedAppId, selectedTestTypeId],
    defaultErrorMessage:
      'Could not load appointment availability from DVLD server.',
    onSuccess: (data) => {
      const next = resolveNextAvailabilitySelection(
        data.availability,
        selectedDateRef.current,
        selectedSlotRef.current,
      )
      setSelectedDate(next.date)
      setSelectedSlot(next.slot)
    },
  })

  const schedulePrep = bookingContext.schedulePrep
  const existingAppointments = bookingContext.existingAppointments
  const availability = bookingContext.availability
  const availabilityError = bookingContext.availabilityError

  const eligibility = useMemo(
    () =>
      evaluateAppointmentEligibility({
        application,
        existingAppointments,
        selectedTestTypeId,
      }),
    [application, existingAppointments, selectedTestTypeId],
  )

  const totalFees = useMemo(
    () => calculateTotalBookingFees(schedulePrep),
    [schedulePrep],
  )

  const confirmSchedule = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault()
      if (
        !selectedAppId ||
        !selectedDate ||
        !selectedSlot ||
        !eligibility.isEligibleToSchedule
      ) {
        return
      }

      setSubmitting(true)
      setSubmitError(null)

      try {
        const submissionDateTime = formatAppointmentSubmission(
          `${selectedDate}T${selectedSlot}`,
        )
        await scheduleTestAppointment(accessToken, {
          localDrivingLicenseApplicationId: selectedAppId,
          testTypeId: selectedTestTypeId,
          appointmentDate: submissionDateTime,
        })

        setBookedDetails({
          date: selectedDate,
          time: selectedSlot,
          fee: totalFees,
          testName: eligibility.currentStageConfig.name,
          appId: selectedAppId,
          candidateName: application?.fullName || 'Candidate',
        })
        setSubmitSuccess(true)

        void refreshBookingContext()
      } catch (err) {
        setSubmitError(
          err instanceof Error
            ? err.message
            : 'Failed to schedule examination appointment. The requested slot may have just been reserved.',
        )
        void refreshBookingContext()
      } finally {
        setSubmitting(false)
      }
    },
    [
      accessToken,
      application,
      eligibility,
      refreshBookingContext,
      selectedAppId,
      selectedDate,
      selectedSlot,
      selectedTestTypeId,
      totalFees,
    ],
  )

  const dismissSubmitError = useCallback(() => {
    setSubmitError(null)
  }, [])

  const scheduleAnother = useCallback(() => {
    setSubmitSuccess(false)
    setBookedDetails(null)
    void refreshBookingContext()
  }, [refreshBookingContext])

  return {
    selectedAppId,
    selectedTestTypeId,
    application,
    loadingApp,
    appError,
    refreshApp,
    schedulePrep,
    availability,
    availabilityError,
    loadingContext,
    refreshBookingContext,
    selectedDate,
    selectedSlot,
    setSelectedDate,
    setSelectedSlot,
    eligibility,
    totalFees,
    submitting,
    submitError,
    submitSuccess,
    bookedDetails,
    selectInitialApplication,
    selectApplication,
    selectStage,
    confirmSchedule,
    dismissSubmitError,
    scheduleAnother,
  }
}
