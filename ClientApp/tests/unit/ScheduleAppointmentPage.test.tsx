import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import ScheduleAppointmentPage from '../../src/components/test-appointments/ScheduleAppointmentPage'
import {
  calculateTotalBookingFees,
  evaluateAppointmentEligibility,
  filterCandidateApplications,
  isApplicationNewStatus,
  resolveNextAvailabilitySelection,
} from '../../src/components/test-appointments/utils/appointmentEligibility'
import * as localAppService from '../../src/services/localDrivingLicenseApplicationService'
import * as appointmentService from '../../src/services/testAppointmentService'
import type { LocalDrivingLicenseApplication } from '../../src/types/LocalDrivingLicenseApplication'
import type { TestAppointment } from '../../src/types/TestAppointment'

function makeLocalApp(
  overrides: Partial<LocalDrivingLicenseApplication> = {},
): LocalDrivingLicenseApplication {
  return {
    localDrivingLicenseApplicationId: 42,
    licenseClassId: 3,
    licenseClassName: 'Class 3 - Ordinary driving license',
    nationalNo: 'N4242',
    fullName: 'Layla Hassan',
    applicationDate: '2026-11-01T10:00:00',
    passedTest: 1,
    applicationStatus: 'New',
    statusText: 'New',
    applicationFees: 15,
    licenseClassFees: 20,
    hasLicense: false,
    applicantPersonId: 800,
    ...overrides,
  }
}

function makeAppointment(
  overrides: Partial<TestAppointment> = {},
): TestAppointment {
  return {
    testAppointmentId: 701,
    testTypeId: 2,
    testTypeName: 'Written (Theory) Test',
    localDrivingLicenseApplicationId: 42,
    appointmentDate: '2026-11-25T10:00:00+03:00',
    paidFees: 20,
    createdByUserId: 1,
    createdByUserName: 'staff1',
    isLocked: false,
    status: 'Scheduled',
    testResult: -1,
    testResultText: 'Not Taken',
    retakeTestApplicationId: null,
    ...overrides,
  }
}

describe('appointmentEligibility utilities', () => {
  it('evaluates sequential stage prerequisites, passed stage lock, New status, and duplicate active bookings', () => {
    const appAtStage1 = makeLocalApp({ passedTest: 1, statusText: 'New' })

    // Stage 1 (Vision) already passed when passedTest === 1
    const stage1 = evaluateAppointmentEligibility({
      application: appAtStage1,
      existingAppointments: [],
      selectedTestTypeId: 1,
    })
    expect(stage1.isStageAlreadyPassed).toBe(true)
    expect(stage1.isEligibleToSchedule).toBe(false)

    // Stage 2 (Written) is eligible when no active appointment exists for stage 2
    const stage2Eligible = evaluateAppointmentEligibility({
      application: appAtStage1,
      existingAppointments: [
        makeAppointment({ testTypeId: 2, status: 'Cancelled', isLocked: true }),
      ],
      selectedTestTypeId: 2,
    })
    expect(stage2Eligible.isPrerequisiteMet).toBe(true)
    expect(stage2Eligible.hasActiveAppointment).toBe(false)
    expect(stage2Eligible.isEligibleToSchedule).toBe(true)

    // Stage 2 blocked when an unlocked Scheduled appointment exists for stage 2
    const stage2Duplicate = evaluateAppointmentEligibility({
      application: appAtStage1,
      existingAppointments: [
        makeAppointment({ testTypeId: 2, status: 'Scheduled', isLocked: false }),
      ],
      selectedTestTypeId: 2,
    })
    expect(stage2Duplicate.hasActiveAppointment).toBe(true)
    expect(stage2Duplicate.isEligibleToSchedule).toBe(false)

    // Stage 3 (Street) blocked until Written is passed (passedTest < 2)
    const stage3 = evaluateAppointmentEligibility({
      application: appAtStage1,
      existingAppointments: [],
      selectedTestTypeId: 3,
    })
    expect(stage3.isPrerequisiteMet).toBe(false)
    expect(stage3.isEligibleToSchedule).toBe(false)

    // Cancelled / Completed applications cannot schedule
    const completedApp = makeLocalApp({
      passedTest: 1,
      applicationStatus: 'Cancelled',
      statusText: 'Cancelled',
    })
    expect(isApplicationNewStatus(completedApp)).toBe(false)
    expect(
      evaluateAppointmentEligibility({
        application: completedApp,
        existingAppointments: [],
        selectedTestTypeId: 2,
      }).isEligibleToSchedule,
    ).toBe(false)
  })

  it('calculates base + retake fees, filters candidates, and reconciles availability selections', () => {
    expect(calculateTotalBookingFees(null)).toBe(0)
    expect(calculateTotalBookingFees({ fees: 20, retakerFees: 5 })).toBe(25)

    const apps = [
      makeLocalApp({
        localDrivingLicenseApplicationId: 10,
        fullName: 'Omar Zaid',
        nationalNo: 'N111',
      }),
      makeLocalApp({
        localDrivingLicenseApplicationId: 20,
        fullName: 'Rana Sami',
        nationalNo: 'N222',
      }),
    ]
    expect(filterCandidateApplications(apps, '')).toHaveLength(2)
    expect(filterCandidateApplications(apps, 'n222')).toHaveLength(1)
    expect(filterCandidateApplications(apps, '10')[0].fullName).toBe('Omar Zaid')

    const availability = {
      localAppId: 42,
      testTypeId: 2,
      availableDays: [
        {
          date: '2026-11-25',
          slots: [
            { startTime: '09:20', endTime: '10:00' },
            { startTime: '10:00', endTime: '10:40' },
          ],
        },
        {
          date: '2026-11-26',
          slots: [{ startTime: '11:00', endTime: '11:40' }],
        },
      ],
    }

    expect(
      resolveNextAvailabilitySelection(availability, '2026-11-26', '11:00'),
    ).toEqual({ date: '2026-11-26', slot: '11:00' })
    expect(
      resolveNextAvailabilitySelection(availability, '2026-11-30', '15:00'),
    ).toEqual({ date: '2026-11-25', slot: '09:20' })
    expect(resolveNextAvailabilitySelection(null, '2026-11-25', '09:20')).toEqual(
      { date: '', slot: '' },
    )
  })
})

describe('ScheduleAppointmentPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('supports Scenario B candidate auto-selection, search, pagination, and direct ID lookup', async () => {
    const completedApp = makeLocalApp({
      localDrivingLicenseApplicationId: 11,
      fullName: 'Completed User',
      nationalNo: 'N11',
      applicationStatus: 'Completed',
      statusText: 'Completed',
      passedTest: 3,
    })
    const newApp = makeLocalApp({
      localDrivingLicenseApplicationId: 22,
      fullName: 'Layla Hassan',
      nationalNo: 'N22',
      passedTest: 0,
    })
    const pagedApp = makeLocalApp({
      localDrivingLicenseApplicationId: 33,
      fullName: 'Extra Candidate',
      nationalNo: 'N33',
      passedTest: 0,
    })
    const lookupApp = makeLocalApp({
      localDrivingLicenseApplicationId: 99,
      fullName: 'Direct Lookup Candidate',
      nationalNo: 'N99',
      passedTest: 0,
    })

    const getAppsSpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplications')
      .mockResolvedValueOnce({
        items: [completedApp, newApp],
        pageNumber: 1,
        pageSize: 50,
        totalCount: 60,
        totalPages: 2,
        hasPreviousPage: false,
        hasNextPage: true,
      })
      .mockResolvedValueOnce({
        items: [pagedApp],
        pageNumber: 2,
        pageSize: 50,
        totalCount: 60,
        totalPages: 2,
        hasPreviousPage: true,
        hasNextPage: false,
      })

    const getByIdSpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplicationById')
      .mockImplementation(async (_token, id) => {
        if (id === 22) return newApp
        if (id === 99) return lookupApp
        throw new Error('Not found')
      })

    vi.spyOn(appointmentService, 'getSchedulePreparation').mockResolvedValue({
      appointmentId: 0,
      retakeTestApplicationId: null,
      localDrivingLicenseApplicationId: 22,
      licenseClassName: 'Class 3',
      fullName: 'Layla Hassan',
      trial: 1,
      date: '2026-11-25T00:00:00',
      fees: 10,
      testTypeId: 1,
      retakerFees: 5,
      testId: 0,
      result: false,
      notes: null,
    })
    vi.spyOn(appointmentService, 'getAppointmentsByLocalAppId').mockResolvedValue(
      [],
    )
    vi.spyOn(
      appointmentService,
      'getAppointmentAvailability',
    ).mockResolvedValue({
      localAppId: 22,
      testTypeId: 1,
      availableDays: [
        {
          date: '2026-11-25',
          slots: [{ startTime: '09:00', endTime: '09:30' }],
        },
      ],
    })

    render(
      <ScheduleAppointmentPage
        accessToken="staff-token"
        returnPage="Test Appointments"
        onBack={vi.fn()}
      />,
    )

    // Automatically selects first New candidate (#22) and renders Test Appointments back text
    expect(
      screen.getByRole('button', { name: /back to test appointments/i }),
    ).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByText('Loaded Applications (2)')).toBeInTheDocument()
    })

    // Retake total fees displayed (10 + 5 = 15.00 JOD)
    expect((await screen.findAllByText('15.00 JOD')).length).toBeGreaterThan(0)

    // Filter candidate dropdown via search box
    fireEvent.change(screen.getByLabelText(/search candidate/i), {
      target: { value: 'Layla' },
    })
    expect(screen.getByText('Loaded Applications (1)')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText(/search candidate/i), {
      target: { value: '' },
    })

    // Pagination "Load More Applications" appends page 2
    fireEvent.click(
      screen.getByRole('button', { name: /load more applications/i }),
    )
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenCalledWith('staff-token', 2, 50)
      expect(screen.getByText('Loaded Applications (3)')).toBeInTheDocument()
    })

    // Direct lookup loads app #99
    fireEvent.change(screen.getByPlaceholderText('App ID #'), {
      target: { value: '99' },
    })
    fireEvent.click(screen.getByRole('button', { name: /^lookup$/i }))
    await waitFor(() => {
      expect(getByIdSpy).toHaveBeenCalledWith('staff-token', 99)
      expect(screen.getAllByText('Direct Lookup Candidate').length).toBeGreaterThan(0)
    })
  })

  it('disables confirmation when an active booking already exists for the stage and surfaces submission errors cleanly', async () => {
    const appAtStage1 = makeLocalApp({ passedTest: 1 })
    vi.spyOn(
      localAppService,
      'getLocalDrivingLicenseApplicationById',
    ).mockResolvedValue(appAtStage1)
    vi.spyOn(appointmentService, 'getSchedulePreparation').mockResolvedValue({
      appointmentId: 0,
      retakeTestApplicationId: null,
      localDrivingLicenseApplicationId: 42,
      licenseClassName: 'Class 3',
      fullName: 'Layla Hassan',
      trial: 1,
      date: '2026-11-25T00:00:00',
      fees: 20,
      testTypeId: 2,
      retakerFees: 0,
      testId: 0,
      result: false,
      notes: null,
    })
    vi.spyOn(appointmentService, 'getAppointmentsByLocalAppId')
      .mockResolvedValueOnce([
        makeAppointment({ testTypeId: 2, status: 'Scheduled', isLocked: false }),
      ])
      .mockResolvedValue([])
    vi.spyOn(
      appointmentService,
      'getAppointmentAvailability',
    ).mockResolvedValue({
      localAppId: 42,
      testTypeId: 2,
      availableDays: [
        {
          date: '2026-11-25',
          slots: [{ startTime: '09:20', endTime: '10:00' }],
        },
      ],
    })
    vi.spyOn(appointmentService, 'scheduleTestAppointment').mockRejectedValueOnce(
      new Error('Slot was just reserved by another officer.'),
    )

    const { rerender } = render(
      <ScheduleAppointmentPage
        accessToken="staff-token"
        initialLocalAppId={42}
        initialTestTypeId={2}
        onBack={vi.fn()}
      />,
    )

    const confirmBtn = await screen.findByRole('button', {
      name: /confirm & schedule appointment/i,
    })
    await waitFor(() => {
      expect(confirmBtn).toBeDisabled()
    })

    // Re-render without active appointment and simulate submission error
    rerender(
      <ScheduleAppointmentPage
        key="no-active"
        accessToken="staff-token"
        initialLocalAppId={42}
        initialTestTypeId={2}
        onBack={vi.fn()}
      />,
    )

    const enabledBtn = await screen.findByRole('button', {
      name: /confirm & schedule appointment/i,
    })
    await waitFor(() => {
      expect(enabledBtn).not.toBeDisabled()
    })

    fireEvent.click(enabledBtn)
    await waitFor(() => {
      expect(
        screen.getByText('Slot was just reserved by another officer.'),
      ).toBeInTheDocument()
    })
  })
})
