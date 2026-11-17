import { Suspense } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { NavigationProvider } from '../../src/context/NavigationContext'
import { useNavigation } from '../../src/context/useNavigation'
import AppointmentRoutes from '../../src/components/layout/router/AppointmentRoutes'

vi.mock('../../src/components/local-applications/LocalApplicationsPage', () => ({
  default: ({
    onManageTestAppointments,
  }: {
    onManageTestAppointments?: (localAppId: number) => void
  }) => (
    <div data-testid="page-local-applications">
      <button
        type="button"
        onClick={() => onManageTestAppointments?.(42)}
      >
        Open Manage 42
      </button>
    </div>
  ),
}))

vi.mock('../../src/components/test-appointments/TestAppointmentsPage', () => ({
  default: ({
    onShowAppointmentDetails,
    onTakeExamination,
  }: {
    onShowAppointmentDetails?: (appointmentId: number) => void
    onTakeExamination?: (appointmentId: number, returnPage?: string) => void
  }) => (
    <div data-testid="page-test-appointments">
      <button type="button" onClick={() => onShowAppointmentDetails?.(701)}>
        Open Details 701
      </button>
      <button
        type="button"
        onClick={() => onTakeExamination?.(701, 'Test Appointments')}
      >
        Take Exam 701
      </button>
    </div>
  ),
}))

vi.mock('../../src/components/test-appointments/ManageTestAppointmentsPage', () => ({
  default: ({
    localAppId,
    onBack,
    onShowAppointmentDetails,
    onTakeExamination,
  }: {
    localAppId: number | null
    onBack: () => void
    onShowAppointmentDetails?: (appointmentId: number, returnPage?: string) => void
    onTakeExamination?: (appointmentId: number, returnPage?: string) => void
  }) => (
    <div data-testid="page-manage-test-appointments">
      <span data-testid="manage-local-app-id">
        {localAppId ? String(localAppId) : 'Application ID Not Specified'}
      </span>
      <button type="button" onClick={onBack}>
        Back from Manage
      </button>
      <button
        type="button"
        onClick={() => onTakeExamination?.(701, 'Manage Test Appointments')}
      >
        Manage Take Exam 701
      </button>
      <button
        type="button"
        onClick={() =>
          onShowAppointmentDetails?.(701, 'Manage Test Appointments')
        }
      >
        Manage Details 701
      </button>
    </div>
  ),
}))

vi.mock('../../src/components/test-appointments/AppointmentDetailsPage', () => ({
  default: ({
    appointmentId,
    onBack,
    onTakeExamination,
  }: {
    appointmentId: number | null
    onBack: () => void
    onTakeExamination?: (appointmentId: number, returnPage?: string) => void
  }) => (
    <div data-testid="page-appointment-details">
      <span data-testid="details-appointment-id">
        {appointmentId ? String(appointmentId) : 'Missing Appointment ID'}
      </span>
      <button type="button" onClick={onBack}>
        Back from Details
      </button>
      <button
        type="button"
        onClick={() =>
          appointmentId && onTakeExamination?.(appointmentId, 'Appointment Details')
        }
      >
        Details Take Exam
      </button>
    </div>
  ),
}))

vi.mock('../../src/components/test-appointments/TakeExaminationPage', () => ({
  default: ({
    appointmentId,
    onBack,
    returnPage,
  }: {
    appointmentId: number | null
    onBack: () => void
    returnPage?: string
  }) => (
    <div data-testid="page-take-examination">
      <span data-testid="exam-appointment-id">
        {appointmentId ? String(appointmentId) : 'Missing Appointment ID'}
      </span>
      <span data-testid="exam-return-page">{returnPage}</span>
      <button type="button" onClick={onBack}>
        Back from Exam
      </button>
    </div>
  ),
}))

vi.mock('../../src/components/test-appointments/ScheduleAppointmentPage', () => ({
  default: ({ onBack }: { onBack: () => void }) => (
    <div data-testid="page-schedule-appointment">
      <button type="button" onClick={onBack}>
        Back from Schedule
      </button>
    </div>
  ),
}))

function TestHarness() {
  const { activePage } = useNavigation()
  return (
    <div>
      <span data-testid="active-page">{activePage}</span>
      <Suspense fallback={<div>Loading...</div>}>
        <AppointmentRoutes
          accessToken="test-token"
          activePage={activePage}
          userRole="Staff"
        />
      </Suspense>
    </div>
  )
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(
    <NavigationProvider>
      <TestHarness />
    </NavigationProvider>,
  )
}

describe('Test Appointments Navigation and Back Context', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/test-appointments')
  })

  it('returns to Test Appointments from Take Examination', async () => {
    renderAt('/test-appointments')

    fireEvent.click(await screen.findByText('Take Exam 701'))
    expect(await screen.findByTestId('page-take-examination')).toBeInTheDocument()
    expect(screen.getByTestId('exam-appointment-id')).toHaveTextContent('701')
    expect(screen.getByTestId('exam-return-page')).toHaveTextContent('Test Appointments')

    fireEvent.click(screen.getByText('Back from Exam'))
    expect(await screen.findByTestId('page-test-appointments')).toBeInTheDocument()
    expect(screen.getByTestId('active-page')).toHaveTextContent('Test Appointments')
  })

  it('returns to Test Appointments from Appointment Details', async () => {
    renderAt('/test-appointments')

    fireEvent.click(await screen.findByText('Open Details 701'))
    expect(await screen.findByTestId('page-appointment-details')).toBeInTheDocument()
    expect(screen.getByTestId('details-appointment-id')).toHaveTextContent('701')

    fireEvent.click(screen.getByText('Back from Details'))
    expect(await screen.findByTestId('page-test-appointments')).toBeInTheDocument()
  })

  it('preserves Appointment Details context through Take Examination and Back', async () => {
    renderAt('/test-appointments')

    fireEvent.click(await screen.findByText('Open Details 701'))
    expect(await screen.findByTestId('page-appointment-details')).toBeInTheDocument()

    fireEvent.click(screen.getByText('Details Take Exam'))
    expect(await screen.findByTestId('page-take-examination')).toBeInTheDocument()
    expect(screen.getByTestId('exam-return-page')).toHaveTextContent('Appointment Details')

    fireEvent.click(screen.getByText('Back from Exam'))
    expect(await screen.findByTestId('page-appointment-details')).toBeInTheDocument()
    expect(screen.getByTestId('details-appointment-id')).toHaveTextContent('701')

    fireEvent.click(screen.getByText('Back from Details'))
    expect(await screen.findByTestId('page-test-appointments')).toBeInTheDocument()
  })

  it('preserves localAppId when returning from Take Examination to Manage Test Appointments', async () => {
    renderAt('/local-applications')

    fireEvent.click(await screen.findByText('Open Manage 42'))
    expect(await screen.findByTestId('page-manage-test-appointments')).toBeInTheDocument()
    expect(screen.getByTestId('manage-local-app-id')).toHaveTextContent('42')

    fireEvent.click(screen.getByText('Manage Take Exam 701'))
    expect(await screen.findByTestId('page-take-examination')).toBeInTheDocument()
    expect(screen.getByTestId('exam-return-page')).toHaveTextContent('Manage Test Appointments')

    fireEvent.click(screen.getByText('Back from Exam'))
    expect(await screen.findByTestId('page-manage-test-appointments')).toBeInTheDocument()
    expect(screen.getByTestId('manage-local-app-id')).toHaveTextContent('42')
  })

  it('preserves localAppId when returning from Appointment Details to Manage Test Appointments', async () => {
    renderAt('/local-applications')

    fireEvent.click(await screen.findByText('Open Manage 42'))
    expect(await screen.findByTestId('page-manage-test-appointments')).toBeInTheDocument()

    fireEvent.click(screen.getByText('Manage Details 701'))
    expect(await screen.findByTestId('page-appointment-details')).toBeInTheDocument()
    expect(screen.getByTestId('details-appointment-id')).toHaveTextContent('701')

    fireEvent.click(screen.getByText('Back from Details'))
    expect(await screen.findByTestId('page-manage-test-appointments')).toBeInTheDocument()
    expect(screen.getByTestId('manage-local-app-id')).toHaveTextContent('42')
  })

  it('falls back safely to Test Appointments when Take Examination is opened directly without context', async () => {
    renderAt('/test-appointments/take-exam?appointmentId=701')

    expect(await screen.findByTestId('page-take-examination')).toBeInTheDocument()
    expect(screen.getByTestId('exam-appointment-id')).toHaveTextContent('701')
    expect(screen.getByTestId('exam-return-page')).toHaveTextContent('Test Appointments')

    fireEvent.click(screen.getByText('Back from Exam'))
    expect(await screen.findByTestId('page-test-appointments')).toBeInTheDocument()
    expect(screen.getByTestId('active-page')).toHaveTextContent('Test Appointments')
  })
})
