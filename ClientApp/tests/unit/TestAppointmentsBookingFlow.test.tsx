import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import TestAppointmentsPage from '../../src/components/test-appointments/TestAppointmentsPage'
import ScheduleAppointmentPage from '../../src/components/test-appointments/ScheduleAppointmentPage'
import LocalApplicationsPage from '../../src/components/local-applications/LocalApplicationsPage'
import * as testAppointmentService from '../../src/services/testAppointmentService'
import * as localAppService from '../../src/services/localDrivingLicenseApplicationService'
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
    testTypeName: 'Written Test',
    localDrivingLicenseApplicationId: 42,
    applicantFullName: 'Layla Hassan',
    nationalNo: 'N4242',
    licenseClassName: 'Class 3 - Ordinary driving license',
    appointmentDate: '2026-11-25T10:00:00',
    appointmentDateFormatted: '2026-11-25 10:00 AM',
    paidFees: 20,
    createdByUserId: 1,
    createdByUserName: 'staff1',
    isLocked: false,
    status: 'Scheduled',
    testResult: 'Pending',
    testResultText: 'Pending',
    retakeTestApplicationId: null,
    ...overrides,
  }
}

describe('Application-Driven Test Appointment Booking', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    })
  })

  it('removes global Book Test Appointment button from TestAppointmentsPage while preserving management view', async () => {
    vi.spyOn(testAppointmentService, 'getAllAppointments').mockResolvedValue([
      makeAppointment(),
    ])

    render(<TestAppointmentsPage accessToken="staff-token" />)

    await screen.findAllByText('Layla Hassan')

    expect(screen.queryByRole('button', { name: /book test appointment/i })).toBeNull()
    expect(screen.getByText('Test Appointments Directory')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /all appointments/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /today/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /history/i })).toBeInTheDocument()
  })

  it('navigates from a specific Local Application to ScheduleAppointmentPage with its ID and next test stage', async () => {
    const appAtStage1 = makeLocalApp({
      localDrivingLicenseApplicationId: 42,
      passedTest: 1,
    })

    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplications').mockResolvedValue({
      items: [appAtStage1],
      pageNumber: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    })
    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplicationsSummary').mockResolvedValue({
      totalApplications: 1,
      inProgressCount: 1,
      readyForIssuanceCount: 0,
    })

    const onScheduleSpy = vi.fn()

    render(
      <LocalApplicationsPage
        accessToken="staff-token"
        onScheduleAppointment={onScheduleSpy}
      />,
    )

    const row = (await screen.findByText('Layla Hassan')).closest('tr')!
    fireEvent.contextMenu(row, { button: 2, clientX: 100, clientY: 100 })
    const scheduleSubmenuTrigger = await screen.findByRole('menuitem', { name: /schedule tests/i })
    fireEvent.click(scheduleSubmenuTrigger)

    const writtenOption = await screen.findByRole('menuitem', { name: /schedule written test/i })
    fireEvent.click(writtenOption)

    expect(onScheduleSpy).toHaveBeenCalledWith(42, 2, 'Local Applications')
  })

  it('loads preparation, availability, and submits booking when ScheduleAppointmentPage receives initialLocalAppId and initialTestTypeId', async () => {
    const appAtStage1 = makeLocalApp({
      localDrivingLicenseApplicationId: 42,
      passedTest: 1,
    })

    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplicationById').mockResolvedValue(appAtStage1)
    vi.spyOn(testAppointmentService, 'getSchedulePreparation').mockResolvedValue({
      appointmentId: 0,
      retakeTestApplicationId: null,
      localDrivingLicenseApplicationId: 42,
      licenseClassName: 'Class 3 - Ordinary driving license',
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
    vi.spyOn(testAppointmentService, 'getAppointmentsByLocalAppId').mockResolvedValue([])
    vi.spyOn(testAppointmentService, 'getAppointmentAvailability').mockResolvedValue({
      localAppId: 42,
      testTypeId: 2,
      availableDays: [
        {
          date: '2026-11-25',
          slots: [{ startTime: '09:20', endTime: '10:00' }],
        },
      ],
    })
    const scheduleSpy = vi
      .spyOn(testAppointmentService, 'scheduleTestAppointment')
      .mockResolvedValue(undefined)

    render(
      <ScheduleAppointmentPage
        accessToken="staff-token"
        initialLocalAppId={42}
        initialTestTypeId={2}
        returnPage="Local Applications"
        onBack={vi.fn()}
      />,
    )

    await screen.findAllByText('Layla Hassan')
    expect(screen.queryByLabelText(/loaded applications/i)).toBeNull()

    const confirmBtn = await screen.findByRole('button', {
      name: /confirm & schedule appointment/i,
    })
    await waitFor(() => {
      expect(confirmBtn).not.toBeDisabled()
    })

    fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(scheduleSpy).toHaveBeenCalledTimes(1)
    })
    expect(scheduleSpy).toHaveBeenCalledWith('staff-token', {
      localDrivingLicenseApplicationId: 42,
      testTypeId: 2,
      appointmentDate: '2026-11-25T09:20:00',
    })
  })
})
