import type { TestAppointment } from '../../../types/TestAppointment'
import { isAppointmentActive, resolveAppointmentLifecycle } from '../../../utils/status'
import type { StatCardVariant } from '../../common/statCardVariants'

export interface LatestOutcomeSummary {
  value: string
  variant: StatCardVariant
  description: string
}

/**
 * Sorts test appointments chronologically (newest date first), deterministically
 * tie-breaking equal or invalid dates by descending testAppointmentId.
 */
export function sortAppointmentsChronologically(
  rawAppointments: readonly TestAppointment[],
): TestAppointment[] {
  return [...rawAppointments].sort((a, b) => {
    const timeA = new Date(a.appointmentDate).getTime()
    const timeB = new Date(b.appointmentDate).getTime()
    if (!Number.isNaN(timeA) && !Number.isNaN(timeB) && timeA !== timeB) {
      return timeB - timeA // Descending: newest first
    }
    return b.testAppointmentId - a.testAppointmentId
  })
}

/**
 * Returns the active (unlocked Scheduled/Open) appointment, if any.
 */
export function findActiveAppointment(
  sortedAppointments: readonly TestAppointment[],
): TestAppointment | null {
  return sortedAppointments.find(isAppointmentActive) ?? null
}

/**
 * Resolves the next sequential examination stage (1 = Vision, 2 = Written, 3 = Street)
 * based on the application's passed test count.
 */
export function resolveNextTestStage(
  passedTest: number | null | undefined,
): 1 | 2 | 3 {
  if (passedTest === 0 || passedTest === null || passedTest === undefined) {
    return 1
  }
  if (passedTest === 1) {
    return 2
  }
  return 3
}

/**
 * Shared action eligibility rule determining whether an appointment can be taken
 * for examination or cancelled.
 */
export function canTakeOrCancelAppointment(
  appointment: Pick<TestAppointment, 'isLocked' | 'status'>,
): boolean {
  return (
    !appointment.isLocked &&
    (appointment.status === 'Scheduled' || appointment.status === 'Open')
  )
}

/**
 * Derives the Latest Outcome summary StatCard value, semantic variant, and caption
 * from chronologically sorted appointments (calling lifecycle resolution once).
 */
export function deriveLatestOutcomeSummary(
  sortedAppointments: readonly TestAppointment[],
): LatestOutcomeSummary {
  if (sortedAppointments.length === 0) {
    return {
      value: 'No Records',
      variant: 'default',
      description: 'Awaiting initial booking',
    }
  }

  const latest = sortedAppointments[0]
  const lifecycle = resolveAppointmentLifecycle(latest)
  const value =
    lifecycle.resultLabel !== 'Not Taken'
      ? lifecycle.resultLabel
      : lifecycle.statusLabel

  const variant: StatCardVariant =
    lifecycle.resultLabel === 'Passed'
      ? 'emerald'
      : lifecycle.resultLabel === 'Failed'
        ? 'rose'
        : 'default'

  return {
    value,
    variant,
    description: `${latest.testTypeName} (Trial)`,
  }
}
