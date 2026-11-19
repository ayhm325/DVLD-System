import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import type {
  AppointmentAvailabilityResponse,
  ScheduleTestPreparation,
  TestAppointment,
} from '../../../types/TestAppointment'
import { isAppointmentActive } from '../../../utils/status'
import { STAGE_CONFIG, type StageItemConfig } from '../stageConfig'

export interface AppointmentEligibility {
  currentStageConfig: StageItemConfig
  isNewStatus: boolean
  passedCount: number
  isPrerequisiteMet: boolean
  isStageAlreadyPassed: boolean
  activeAppointment: TestAppointment | undefined
  hasActiveAppointment: boolean
  isEligibleToSchedule: boolean
}

/**
 * Checks whether a local driving license application is in the 'New' lifecycle state.
 */
export function isApplicationNewStatus(
  application:
    | Pick<LocalDrivingLicenseApplication, 'statusText' | 'applicationStatus'>
    | null
    | undefined,
): boolean {
  return (
    (
      application?.statusText ||
      application?.applicationStatus ||
      ''
    ).toLowerCase() === 'new'
  )
}

/**
 * Evaluates all frontend stage scheduling rules:
 * - New application status
 * - Sequential 3-stage prerequisite progression (Vision -> Written -> Street)
 * - Prevention of rescheduling an already-passed stage
 * - Duplicate active booking detection for the selected test stage
 */
export function evaluateAppointmentEligibility({
  application,
  existingAppointments,
  selectedTestTypeId,
}: {
  application: LocalDrivingLicenseApplication | null
  existingAppointments: readonly TestAppointment[]
  selectedTestTypeId: 1 | 2 | 3
}): AppointmentEligibility {
  const currentStageConfig = STAGE_CONFIG[selectedTestTypeId]
  const isNewStatus = isApplicationNewStatus(application)
  const passedCount = application?.passedTest ?? 0
  const isPrerequisiteMet = passedCount >= currentStageConfig.requiredPasses
  const isStageAlreadyPassed =
    (selectedTestTypeId === 1 && passedCount >= 1) ||
    (selectedTestTypeId === 2 && passedCount >= 2) ||
    (selectedTestTypeId === 3 && passedCount >= 3)

  const activeAppointment = existingAppointments
    .filter((a) => a.testTypeId === selectedTestTypeId)
    .find(isAppointmentActive)

  const hasActiveAppointment = Boolean(activeAppointment)

  const isEligibleToSchedule =
    Boolean(application) &&
    isNewStatus &&
    isPrerequisiteMet &&
    !isStageAlreadyPassed &&
    !hasActiveAppointment

  return {
    currentStageConfig,
    isNewStatus,
    passedCount,
    isPrerequisiteMet,
    isStageAlreadyPassed,
    activeAppointment,
    hasActiveAppointment,
    isEligibleToSchedule,
  }
}

/**
 * Computes combined standard stage fee + retake application fee when applicable.
 */
export function calculateTotalBookingFees(
  schedulePrep:
    | Pick<ScheduleTestPreparation, 'fees' | 'retakerFees'>
    | null
    | undefined,
): number {
  return (schedulePrep?.fees ?? 0) + (schedulePrep?.retakerFees ?? 0)
}

/**
 * Filters candidate local applications by applicant name, national number, app ID, or license class.
 */
export function filterCandidateApplications(
  candidates: readonly LocalDrivingLicenseApplication[],
  searchQuery: string,
): LocalDrivingLicenseApplication[] {
  const query = searchQuery.trim().toLowerCase()
  if (!query) return [...candidates]
  return candidates.filter((app) => {
    return (
      app.fullName?.toLowerCase().includes(query) ||
      app.nationalNo?.toLowerCase().includes(query) ||
      String(app.localDrivingLicenseApplicationId).includes(query) ||
      app.licenseClassName?.toLowerCase().includes(query)
    )
  })
}

/**
 * Reconciles selected date and time slot against newly fetched availability windows,
 * preserving prior choices when still available and otherwise selecting the earliest slot.
 */
export function resolveNextAvailabilitySelection(
  availability: AppointmentAvailabilityResponse | null,
  prevDate: string,
  prevSlot: string,
): { date: string; slot: string } {
  if (!availability || availability.availableDays.length === 0) {
    return { date: '', slot: '' }
  }

  const dateExists = availability.availableDays.some((d) => d.date === prevDate)
  const nextDate = dateExists ? prevDate : availability.availableDays[0].date
  const chosenDay = availability.availableDays.find((d) => d.date === nextDate)
  const slotExists = chosenDay?.slots.some((s) => s.startTime === prevSlot)
  const nextSlot = slotExists ? prevSlot : chosenDay?.slots[0]?.startTime ?? ''

  return { date: nextDate, slot: nextSlot }
}
