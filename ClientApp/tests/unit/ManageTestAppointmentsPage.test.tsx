import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import ManageTestAppointmentsPage from '../../src/components/test-appointments/ManageTestAppointmentsPage'
import {
  canTakeOrCancelAppointment,
  deriveLatestOutcomeSummary,
  findActiveAppointment,
  resolveNextTestStage,
  sortAppointmentsChronologically,
} from '../../src/components/test-appointments/utils/appointmentSorting'
import * as localAppService from '../../src/services/localDrivingLicenseApplicationService'
import * as appointmentService from '../../src/services/testAppointmentService'
import type { LocalDrivingLicenseApplication } from '../../src/types/LocalDrivingLicenseApplication'
import type { TestAppointment, TestResponse } from '../../src/types/TestAppointment'

function makeApplication(
  overrides: Partial<LocalDrivingLicenseApplication> = {},
): LocalDrivingLicenseApplication {
  return {
    localDrivingLicenseApplicationId: 42,
    licenseClassId: 3,
    licenseClassName: 'Class 3 - Ordinary driving license',
    nationalNo: 'N10042',
    fullName: 'Layla Ahmad',
    applicationDate: '2026-10-15T10:00:00Z',
    passedTest: 1,
    applicationStatus: 'New',
    statusText: 'New',
    applicationFees: 15,
    licenseClassFees: 20,
    hasLicense: false,
    applicantPersonId: 12,
    ...overrides,
  }
}

function makeAppointment(
  overrides: Partial<TestAppointment> = {},
): TestAppointment {
  return {
    testAppointmentId: 101,
    testTypeId: 1,
    testTypeName: 'Vision Test',
    localDrivingLicenseApplicationId: 42,
    appointmentDate: '2026-11-01T09:00:00+03:00',
    paidFees: 10,
    createdByUserId: 1,
    createdByUserName: 'staff',
    isLocked: false,
    retakeTestApplicationId: null,
    testResult: -1,
    testResultText: 'Not Taken',
    status: 'Scheduled',
    ...overrides,
  }
}

describe('appointmentSorting utilities', () => {
  it('sorts appointments chronologically descending and tie-breaks deterministically by ID', () => {
    const older = makeAppointment({
      testAppointmentId: 10,
      appointmentDate: '2026-11-01T08:00:00+03:00',
    })
    const sameDateLowId = makeAppointment({
      testAppointmentId: 20,
      appointmentDate: '2026-11-05T10:00:00+03:00',
    })
    const sameDateHighId = makeAppointment({
      testAppointmentId: 25,
      appointmentDate: '2026-11-05T10:00:00+03:00',
    })

    const sorted = sortAppointmentsChronologically([
      older,
      sameDateLowId,
      sameDateHighId,
    ])
    expect(sorted.map((a) => a.testAppointmentId)).toEqual([25, 20, 10])
  })

  it('resolves next stage, active appointment, action eligibility, and latest outcome summary', () => {
    expect(resolveNextTestStage(undefined)).toBe(1)
    expect(resolveNextTestStage(0)).toBe(1)
    expect(resolveNextTestStage(1)).toBe(2)
    expect(resolveNextTestStage(2)).toBe(3)
    expect(resolveNextTestStage(3)).toBe(3)

    expect(
      canTakeOrCancelAppointment({ isLocked: false, status: 'Scheduled' }),
    ).toBe(true)
    expect(
      canTakeOrCancelAppointment({ isLocked: false, status: 'Open' }),
    ).toBe(true)
    expect(
      canTakeOrCancelAppointment({ isLocked: true, status: 'Scheduled' }),
    ).toBe(false)
    expect(
      canTakeOrCancelAppointment({ isLocked: false, status: 'Completed' }),
    ).toBe(false)

    const emptyOutcome = deriveLatestOutcomeSummary([])
    expect(emptyOutcome.value).toBe('No Records')
    expect(emptyOutcome.variant).toBe('default')

    const passedLatest = makeAppointment({
      testAppointmentId: 300,
      testTypeName: 'Vision Test',
      isLocked: true,
      status: 'Completed',
      testResult: 1,
      testResultText: 'Pass',
    })
    const scheduled = makeAppointment({
      testAppointmentId: 301,
      status: 'Scheduled',
      isLocked: false,
    })

    expect(findActiveAppointment([passedLatest, scheduled])?.testAppointmentId).toBe(
      301,
    )

    const passedSummary = deriveLatestOutcomeSummary([passedLatest])
    expect(passedSummary.value).toBe('Passed')
    expect(passedSummary.variant).toBe('emerald')
    expect(passedSummary.description).toBe('Vision Test (Trial)')

    const failedLatest = makeAppointment({
      testTypeName: 'Written (Theory) Test',
      isLocked: true,
      status: 'Completed',
      testResult: 0,
      testResultText: 'Fail',
    })
    const failedSummary = deriveLatestOutcomeSummary([failedLatest])
    expect(failedSummary.value).toBe('Failed')
    expect(failedSummary.variant).toBe('rose')
  })
})

describe('ManageTestAppointmentsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('renders empty context message when localAppId is missing or invalid and wires Back button', () => {
    const onBack = vi.fn()
    render(
      <ManageTestAppointmentsPage
        accessToken="staff-token"
        localAppId={null}
        onBack={onBack}
      />,
    )

    expect(screen.getByText('Application ID Not Specified')).toBeInTheDocument()
    fireEvent.click(
      screen.getByRole('button', { name: /back to local applications/i }),
    )
    expect(onBack).toHaveBeenCalledTimes(1)
  })

  it('renders application context, sorted timeline, lazy cached test observations, and row navigation callbacks', async () => {
    const passedVision = makeAppointment({
      testAppointmentId: 201,
      testTypeId: 1,
      testTypeName: 'Vision Test',
      appointmentDate: '2026-11-01T09:00:00+03:00',
      isLocked: true,
      status: 'Completed',
      testResult: 1,
      testResultText: 'Pass',
    })
    const activeWritten = makeAppointment({
      testAppointmentId: 202,
      testTypeId: 2,
      testTypeName: 'Written (Theory) Test',
      appointmentDate: '2026-11-10T11:00:00+03:00',
      isLocked: false,
      status: 'Scheduled',
      testResult: -1,
      testResultText: 'Not Taken',
    })
    const testRecord: TestResponse = {
      testId: 901,
      testAppointmentId: 201,
      testResult: true,
      notes: 'Clear 6/6 vision score verified.',
      createdByUserId: 7,
      createdByUserName: 'examiner1',
      testTypeName: 'Vision Test',
      appointmentDate: '2026-11-01T09:00:00+03:00',
    }

    vi.spyOn(
      localAppService,
      'getLocalDrivingLicenseApplicationById',
    ).mockResolvedValue(makeApplication({ passedTest: 1 }))
    vi.spyOn(
      appointmentService,
      'getAppointmentsByLocalAppId',
    ).mockResolvedValue([passedVision, activeWritten])
    const getTestSpy = vi
      .spyOn(appointmentService, 'getTestByAppointmentId')
      .mockResolvedValue([testRecord])

    const onTakeExamination = vi.fn()
    const onShowAppointmentDetails = vi.fn()

    render(
      <ManageTestAppointmentsPage
        accessToken="staff-token"
        localAppId={42}
        onBack={vi.fn()}
        onTakeExamination={onTakeExamination}
        onShowAppointmentDetails={onShowAppointmentDetails}
      />,
    )

    await waitFor(() => {
      expect(screen.getByText('Layla Ahmad')).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: /active appointment \(#202\)/i }),
      ).toBeDisabled()
    })

    // Row 202 (newest) should appear before row 201
    const rows = screen.getAllByRole('row')
    expect(rows[1]).toHaveTextContent('#202')
    expect(rows[2]).toHaveTextContent('#201')

    // Navigation callbacks preserve return page 'Manage Test Appointments'
    fireEvent.click(screen.getByRole('button', { name: /take exam/i }))
    expect(onTakeExamination).toHaveBeenCalledWith(
      202,
      'Manage Test Appointments',
    )

    const activeRow = rows[1]
    fireEvent.click(within(activeRow).getByRole('button', { name: /details/i }))
    expect(onShowAppointmentDetails).toHaveBeenCalledWith(
      202,
      'Manage Test Appointments',
    )

    // Expand observations lazily and verify caching across Close + View Notes
    fireEvent.click(screen.getByRole('button', { name: /view notes/i }))
    await waitFor(() => {
      expect(
        screen.getByText('Clear 6/6 vision score verified.'),
      ).toBeInTheDocument()
    })
    expect(getTestSpy).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))
    expect(
      screen.queryByText('Clear 6/6 vision score verified.'),
    ).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /view notes/i }))
    await waitFor(() => {
      expect(
        screen.getByText('Clear 6/6 vision score verified.'),
      ).toBeInTheDocument()
    })
    expect(getTestSpy).toHaveBeenCalledTimes(1)
  })

  it('cancels an active appointment through the confirmation dialog and refreshes records', async () => {
    const activeWritten = makeAppointment({
      testAppointmentId: 202,
      testTypeId: 2,
      testTypeName: 'Written (Theory) Test',
      status: 'Scheduled',
      isLocked: false,
    })
    const cancelledWritten = makeAppointment({
      ...activeWritten,
      status: 'Cancelled',
      isLocked: true,
    })

    vi.spyOn(
      localAppService,
      'getLocalDrivingLicenseApplicationById',
    ).mockResolvedValue(makeApplication({ passedTest: 1 }))
    const getAppointmentsSpy = vi
      .spyOn(appointmentService, 'getAppointmentsByLocalAppId')
      .mockResolvedValueOnce([activeWritten])
      .mockResolvedValueOnce([cancelledWritten])
    const cancelSpy = vi
      .spyOn(appointmentService, 'cancelTestAppointment')
      .mockResolvedValue(undefined)

    render(
      <ManageTestAppointmentsPage
        accessToken="staff-token"
        localAppId={42}
        onBack={vi.fn()}
      />,
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /^cancel$/i })).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.getByText('Cancel Scheduled Appointment')).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: /confirm cancellation/i }),
    )

    await waitFor(() => {
      expect(cancelSpy).toHaveBeenCalledWith('staff-token', 202)
      expect(
        screen.getByText(/Appointment #202 for Written \(Theory\) Test has been successfully cancelled\./),
      ).toBeInTheDocument()
    })
    expect(getAppointmentsSpy).toHaveBeenCalledTimes(2)
  })

  it('navigates to Schedule Appointment with next stage or disables when all 3 stages are passed', async () => {
    const onScheduleAppointment = vi.fn()
    vi.spyOn(
      localAppService,
      'getLocalDrivingLicenseApplicationById',
    ).mockResolvedValueOnce(makeApplication({ passedTest: 1 }))
    vi.spyOn(
      appointmentService,
      'getAppointmentsByLocalAppId',
    ).mockResolvedValueOnce([])

    const view = render(
      <ManageTestAppointmentsPage
        accessToken="staff-token"
        localAppId={42}
        onBack={vi.fn()}
        onScheduleAppointment={onScheduleAppointment}
      />,
    )

    const scheduleBtn = await screen.findByRole('button', {
      name: /schedule written test/i,
    })
    fireEvent.click(scheduleBtn)
    expect(onScheduleAppointment).toHaveBeenCalledWith(
      42,
      2,
      'Manage Test Appointments',
    )

    view.unmount()

    vi.spyOn(
      localAppService,
      'getLocalDrivingLicenseApplicationById',
    ).mockResolvedValueOnce(makeApplication({ passedTest: 3 }))
    vi.spyOn(
      appointmentService,
      'getAppointmentsByLocalAppId',
    ).mockResolvedValueOnce([])

    render(
      <ManageTestAppointmentsPage
        accessToken="staff-token"
        localAppId={42}
        onBack={vi.fn()}
        onScheduleAppointment={onScheduleAppointment}
      />,
    )

    const completedBtn = await screen.findByRole('button', {
      name: /all stages completed/i,
    })
    expect(completedBtn).toBeDisabled()
  })
})
