import { useCallback } from 'react'
import {
  AlertCircle,
  ArrowLeft,
  Award,
  CalendarClock,
  CalendarDays,
  CheckCircle2,
  Clock,
  PlusCircle,
  ShieldCheck,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TestAppointment } from '../../types/TestAppointment'
import EmptyState from '../common/EmptyState'
import ErrorAlert from '../common/ErrorAlert'
import LoadingSpinner from '../common/LoadingSpinner'
import PageHero from '../common/PageHero'
import StatCard from '../common/StatCard'
import StatsGrid from '../common/StatsGrid'
import StatusBadge from '../common/StatusBadge'
import { Button } from '../ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '../ui/card'
import AppointmentTable from './components/AppointmentTable'
import CancelAppointmentDialog from './components/CancelAppointmentDialog'
import { useManageTestAppointments } from './hooks/useManageTestAppointments'
import { STAGE_CONFIG } from './stageConfig'

export interface ManageTestAppointmentsPageProps {
  accessToken: string
  localAppId: number | null
  onBack: () => void
  onNavigate?: (page: string) => void
  onScheduleAppointment?: (
    localAppId: number,
    testTypeId: 1 | 2 | 3,
    returnPage?: string,
  ) => void
  onShowAppointmentDetails?: (appointmentId: number, returnPage?: string) => void
  onTakeExamination?: (appointmentId: number, returnPage?: string) => void
}

export default function ManageTestAppointmentsPage({
  accessToken,
  localAppId,
  onBack,
  onNavigate,
  onScheduleAppointment,
  onShowAppointmentDetails,
  onTakeExamination,
}: ManageTestAppointmentsPageProps) {
  const {
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
    refresh,
    dismissError,
    dismissFeedback,
    toggleTestDetails,
    closeTestDetails,
    requestCancel,
    dismissCancel,
    confirmCancel,
  } = useManageTestAppointments(accessToken, localAppId)

  const handleScheduleNextTest = useCallback(() => {
    if (!application) return
    if (onScheduleAppointment) {
      onScheduleAppointment(
        application.localDrivingLicenseApplicationId,
        nextStage,
        'Manage Test Appointments',
      )
    } else {
      onNavigate?.('Schedule Appointment')
    }
  }, [application, nextStage, onScheduleAppointment, onNavigate])

  const handleTakeExamination = useCallback(
    (appointment: TestAppointment) => {
      if (onTakeExamination) {
        onTakeExamination(appointment.testAppointmentId, 'Manage Test Appointments')
      } else {
        onNavigate?.('Take Examination')
      }
    },
    [onTakeExamination, onNavigate],
  )

  const handleShowAppointmentDetails = useCallback(
    (appointment: TestAppointment) => {
      if (onShowAppointmentDetails) {
        onShowAppointmentDetails(
          appointment.testAppointmentId,
          'Manage Test Appointments',
        )
      } else {
        onNavigate?.('Appointment Details')
      }
    },
    [onShowAppointmentDetails, onNavigate],
  )

  const nextStageConfig = STAGE_CONFIG[nextStage]

  // Early return for missing context
  if (!localAppId || localAppId <= 0) {
    return (
      <section className="page-content space-y-6">
        <PageHero
          pageId="Manage Test Appointments"
          title="Test Appointments"
          highlight="History"
          description="Complete chronological examination history and appointment management across Vision, Written, and Practical (Street) tests."
        />

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onBack}
            className="gap-2 text-slate-300 hover:text-white border-[#263e68] bg-[#0e1a33] hover:bg-[#16274a] rounded-xl cursor-pointer self-start"
          >
            <ArrowLeft className="size-4" />
            <span>Back to Local Applications</span>
          </Button>
        </div>

        <Card className="border-[#1b2f52] bg-[#0c192e] text-white p-12 text-center">
          <EmptyState
            icon={<AlertCircle className="size-10 text-amber-400" />}
            message="Application ID Not Specified"
            description="A valid local driving license application ID is required to manage test appointments. Please return to Local Applications and select an application."
          />
        </Card>
      </section>
    )
  }

  return (
    <section className="page-content space-y-6">
      {/* 1. Informational PageHero (Immediately below Topbar, zero interactive controls inside) */}
      <PageHero
        pageId="Manage Test Appointments"
        title="Test Appointments"
        highlight="History"
        description="Complete chronological examination history and appointment management across Vision, Written, and Practical (Street) tests."
      />

      {/* 2. Page Actions Toolbar (Below PageHero) */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onBack}
          className="gap-2 text-slate-300 hover:text-white border-[#263e68] bg-[#0e1a33] hover:bg-[#16274a] rounded-xl cursor-pointer self-start"
        >
          <ArrowLeft className="size-4" />
          <span>Back to Local Applications</span>
        </Button>

        {application && (
          <Button
            type="button"
            size="sm"
            onClick={handleScheduleNextTest}
            disabled={allStagesPassed || Boolean(activeAppointment)}
            title={
              allStagesPassed
                ? 'Candidate has already passed all three driving tests.'
                : activeAppointment
                  ? `An appointment is already active for ${activeAppointment.testTypeName} (#${activeAppointment.testAppointmentId}).`
                  : `Schedule the candidate's next test stage: ${nextStageConfig?.name ?? 'Next Test'}`
            }
            className={cn(
              'font-semibold text-xs gap-1.5 shadow-sm rounded-xl cursor-pointer transition-all',
              allStagesPassed || Boolean(activeAppointment)
                ? 'bg-slate-800 text-slate-400 border border-slate-700/60 cursor-not-allowed opacity-75'
                : 'bg-cyan-600 hover:bg-cyan-500 text-white shadow-cyan-900/30',
            )}
          >
            <PlusCircle className="size-4" />
            <span>
              {allStagesPassed
                ? 'All Stages Completed'
                : activeAppointment
                  ? `Active Appointment (#${activeAppointment.testAppointmentId})`
                  : `Schedule ${nextStageConfig?.shortName ?? 'Next'} Test`}
            </span>
          </Button>
        )}
      </div>

      {/* Action feedback */}
      {actionFeedback && (
        <div
          className={cn(
            'p-4 rounded-xl border flex items-center justify-between gap-3 text-xs',
            actionFeedback.type === 'success'
              ? 'bg-emerald-950/40 border-emerald-500/40 text-emerald-200'
              : 'bg-rose-950/40 border-rose-500/40 text-rose-200',
          )}
        >
          <div className="flex items-center gap-2.5">
            {actionFeedback.type === 'success' ? (
              <CheckCircle2 className="size-4 text-emerald-400 shrink-0" />
            ) : (
              <AlertCircle className="size-4 text-rose-400 shrink-0" />
            )}
            <span>{actionFeedback.message}</span>
          </div>
          <button
            type="button"
            onClick={dismissFeedback}
            className="text-slate-400 hover:text-white cursor-pointer"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Error Alert */}
      {error && (
        <ErrorAlert
          title="Could Not Load Appointment Records"
          message={error}
          onDismiss={dismissError}
          onRetry={refresh}
        />
      )}

      {loading && !application ? (
        <div className="flex items-center justify-center min-h-[300px] w-full">
          <LoadingSpinner size="lg" />
        </div>
      ) : (
        <>
          {/* Section B: Applicant & Application Context Card */}
          {application && (
            <Card className="border-[#1b2f52] bg-[#0c192e] text-white shadow-xl rounded-2xl overflow-hidden">
              <CardHeader className="border-b border-[#1b2f52]/60 p-5 pb-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="flex size-10 items-center justify-center rounded-xl border border-cyan-500/30 bg-cyan-500/10 text-cyan-400 shadow-xs shrink-0">
                      <CalendarClock className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-bold flex items-center gap-2">
                        <span>{application.fullName}</span>
                        <span className="text-slate-400 font-normal text-xs font-mono">
                          ({application.nationalNo})
                        </span>
                      </CardTitle>
                      <CardDescription className="text-xs text-slate-400 mt-0.5">
                        Local Application #{application.localDrivingLicenseApplicationId} ·{' '}
                        {application.licenseClassName || `Class ${application.licenseClassId}`}
                      </CardDescription>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold border border-indigo-500/30 bg-indigo-950/40 text-indigo-300">
                      <ShieldCheck className="size-3.5 text-indigo-400" />
                      <span>{application.passedTest} / 3 Stages Passed</span>
                    </span>

                    <StatusBadge
                      variant={
                        application.applicationStatus === 'Completed'
                          ? 'success'
                          : application.applicationStatus === 'Cancelled'
                            ? 'danger'
                            : 'warning'
                      }
                      label={application.applicationStatus || 'Active'}
                    />
                  </div>
                </div>
              </CardHeader>

              {/* Grid details */}
              <div className="p-5 grid grid-cols-2 sm:grid-cols-4 gap-4 text-xs">
                <div>
                  <span className="text-slate-400 block text-[11px]">
                    Application Date
                  </span>
                  <span className="text-slate-200 font-mono font-medium mt-0.5 block">
                    {application.applicationDate?.split('T')[0] || '—'}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">
                    License Class
                  </span>
                  <span
                    className="text-white font-medium mt-0.5 block truncate"
                    title={application.licenseClassName ?? undefined}
                  >
                    {application.licenseClassName}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">
                    Application Tariff
                  </span>
                  <span className="text-slate-200 font-mono font-medium mt-0.5 block">
                    {application.applicationFees.toFixed(2)} JOD
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">
                    Current Stage Status
                  </span>
                  <span
                    className={cn(
                      'font-semibold mt-0.5 block',
                      allStagesPassed ? 'text-emerald-400' : 'text-cyan-300',
                    )}
                  >
                    {allStagesPassed
                      ? 'Ready for License Issuance'
                      : `Next: ${nextStageConfig?.name ?? 'Examination'}`}
                  </span>
                </div>
              </div>
            </Card>
          )}

          {/* Section C: Complete History Summary Metrics */}
          <StatsGrid columns={4}>
            <StatCard
              label="Total Appointments"
              value={appointments.length}
              icon={<CalendarDays className="size-5" />}
              variant="default"
              description="Chronological records booked"
            />
            <StatCard
              label="Examination Progress"
              value={`${application?.passedTest ?? 0} of 3`}
              icon={<Award className="size-5" />}
              variant="blue"
              description={
                allStagesPassed
                  ? 'All 3 examination stages passed'
                  : `Next requirement: ${nextStageConfig?.shortName ?? 'Test'}`
              }
            />
            <StatCard
              label="Active Appointment"
              value={
                activeAppointment
                  ? `#${activeAppointment.testAppointmentId}`
                  : 'None'
              }
              icon={<Clock className="size-5" />}
              variant={activeAppointment ? 'amber' : 'slate'}
              description={
                activeAppointment
                  ? `${activeAppointment.testTypeName} slot booked`
                  : 'Ready to schedule next stage'
              }
            />
            <StatCard
              label="Latest Outcome"
              value={latestOutcome.value}
              icon={<CheckCircle2 className="size-5" />}
              variant={latestOutcome.variant}
              description={latestOutcome.description}
            />
          </StatsGrid>

          {/* Section D: Recorded Appointments Table */}
          <AppointmentTable
            appointments={appointments}
            expandedAppointmentId={expandedAppointmentId}
            testDetailsMap={testDetailsMap}
            loadingTestId={loadingTestId}
            onToggleTestDetails={toggleTestDetails}
            onCloseTestDetails={closeTestDetails}
            onTakeExamination={handleTakeExamination}
            onRequestCancel={requestCancel}
            onShowDetails={handleShowAppointmentDetails}
            onScheduleInitialTest={handleScheduleNextTest}
          />
        </>
      )}

      {/* Appointment Cancellation Modal */}
      <CancelAppointmentDialog
        appointment={appointmentToCancel}
        cancelling={cancelling}
        onConfirm={confirmCancel}
        onCancel={dismissCancel}
      />
    </section>
  )
}
