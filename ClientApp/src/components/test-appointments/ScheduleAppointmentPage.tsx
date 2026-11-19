import { ArrowLeft, Clock } from 'lucide-react'
import ErrorAlert from '../common/ErrorAlert'
import LoadingSpinner from '../common/LoadingSpinner'
import PageHero from '../common/PageHero'
import { Button } from '../ui/button'
import AppointmentFeeSummary from './AppointmentFeeSummary'
import AppointmentReceiptCard from './AppointmentReceiptCard'
import AppointmentSlotPicker from './AppointmentSlotPicker'
import AppointmentStageStepper from './AppointmentStageStepper'
import ApplicationCandidatePicker from './components/ApplicationCandidatePicker'
import BookingConfirmation from './components/BookingConfirmation'
import { useApplicationCandidates } from './hooks/useApplicationCandidates'
import { useAppointmentBooking } from './hooks/useAppointmentBooking'

export { STAGE_CONFIG } from './stageConfig'

export interface ScheduleAppointmentPageProps {
  accessToken: string
  initialLocalAppId?: number | null
  initialTestTypeId?: 1 | 2 | 3
  returnPage?: string
  onBack: () => void
  onNavigate?: (page: string) => void
  onShowAppointmentDetails?: (appointmentId: number) => void
}

export default function ScheduleAppointmentPage({
  accessToken,
  initialLocalAppId,
  initialTestTypeId = 1,
  returnPage = 'Local Applications',
  onBack,
  onNavigate,
  onShowAppointmentDetails,
}: ScheduleAppointmentPageProps) {
  const {
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
  } = useAppointmentBooking({
    accessToken,
    initialLocalAppId,
    initialTestTypeId,
  })

  const {
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
  } = useApplicationCandidates({
    accessToken,
    initialLocalAppId,
    selectedAppId,
    onSelectInitialApp: selectInitialApplication,
    onSelectDirectApp: selectApplication,
  })

  const canConfirmBooking =
    !submitting &&
    eligibility.isEligibleToSchedule &&
    Boolean(selectedDate) &&
    Boolean(selectedSlot) &&
    !loadingContext &&
    !(availability && availability.availableDays.length === 0)

  return (
    <section className="page-content space-y-6">
      {/* 1. Strictly Informational PageHero */}
      <PageHero
        pageId="Schedule Appointment"
        title="Schedule Test"
        highlight="Appointment"
        description="Reserve an official driving test slot in accordance with Jordan Licensing Department regulations and facility working hours."
      />

      {/* 2. Top Action and Navigation Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onBack}
          className="gap-2 text-slate-300 hover:text-white border-[#263e68] bg-[#0e1a33] hover:bg-[#16274a] rounded-xl cursor-pointer"
        >
          <ArrowLeft className="size-4" />
          <span>
            {returnPage === 'Test Appointments'
              ? 'Back to Test Appointments'
              : 'Back to Local Applications'}
          </span>
        </Button>

        <div className="flex items-center gap-2 text-xs text-slate-400 font-mono bg-[#0c1830] border border-[#1e3458] px-3 py-1.5 rounded-xl">
          <Clock className="size-3.5 text-cyan-400" />
          <span>Jordan Facility Time: Asia/Amman (+03:00)</span>
        </div>
      </div>

      {/* 3. Global Feedback Alerts */}
      {appError && (
        <ErrorAlert
          message={appError}
          onRetry={() => {
            void refreshApp()
          }}
          retryLabel="Retry Loading Application"
        />
      )}

      {submitError && (
        <ErrorAlert
          message={submitError}
          onRetry={dismissSubmitError}
          retryLabel="Dismiss"
        />
      )}

      {submitSuccess && bookedDetails && (
        <AppointmentReceiptCard
          bookedDetails={bookedDetails}
          returnPage={returnPage}
          onBack={onBack}
          onScheduleAnother={scheduleAnother}
          onNavigate={onNavigate}
        />
      )}

      {/* 4. Loading Application Spinner */}
      {loadingApp ? (
        <div className="flex items-center justify-center min-h-[300px]">
          <LoadingSpinner
            size="lg"
            message="Loading candidate application details…"
          />
        </div>
      ) : (
        /* 5. Main 2-Column Responsive Layout */
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          {/* Left Column: Application Context, Examination Stage & Fee Breakdown */}
          <div className="lg:col-span-5 space-y-6">
            <ApplicationCandidatePicker
              application={application}
              isNewStatus={eligibility.isNewStatus}
              showCandidateSelector={initialLocalAppId === undefined}
              selectedAppId={selectedAppId}
              candidateSearchQuery={candidateSearchQuery}
              onSearchQueryChange={setCandidateSearchQuery}
              filteredCandidates={filteredCandidates}
              onSelectApplication={selectApplication}
              hasMoreCandidates={hasMoreCandidates}
              loadingMoreCandidates={loadingMoreCandidates}
              onLoadMoreCandidates={loadMoreCandidates}
              directLookupId={directLookupId}
              onDirectLookupIdChange={setDirectLookupId}
              directLookupLoading={directLookupLoading}
              directLookupError={directLookupError}
              onDirectLookupSubmit={submitDirectLookup}
            />

            <AppointmentStageStepper
              selectedTestTypeId={selectedTestTypeId}
              onSelectStage={selectStage}
              passedCount={eligibility.passedCount}
              trialNumber={schedulePrep?.trial}
              application={application}
              isNewStatus={eligibility.isNewStatus}
              isStageAlreadyPassed={eligibility.isStageAlreadyPassed}
              isPrerequisiteMet={eligibility.isPrerequisiteMet}
              hasActiveAppointment={eligibility.hasActiveAppointment}
              activeAppointment={eligibility.activeAppointment}
              onShowAppointmentDetails={onShowAppointmentDetails}
            />

            <AppointmentFeeSummary
              loadingPrep={loadingContext}
              hasApplication={Boolean(application)}
              standardFee={schedulePrep?.fees}
              retakeFee={schedulePrep?.retakerFees}
              trialNumber={schedulePrep?.trial}
              totalFees={totalFees}
            />
          </div>

          {/* Right Column: Date & Slot Availability Picker and Booking Confirmation */}
          <div className="lg:col-span-7 space-y-6">
            <form
              onSubmit={(e) => void confirmSchedule(e)}
              className="space-y-6"
            >
              <AppointmentSlotPicker
                application={application}
                isEligibleToSchedule={eligibility.isEligibleToSchedule}
                isNewStatus={eligibility.isNewStatus}
                isStageAlreadyPassed={eligibility.isStageAlreadyPassed}
                isPrerequisiteMet={eligibility.isPrerequisiteMet}
                hasActiveAppointment={eligibility.hasActiveAppointment}
                activeAppointment={eligibility.activeAppointment}
                currentStageName={eligibility.currentStageConfig.name}
                availability={availability}
                loadingAvailability={loadingContext}
                availabilityError={availabilityError}
                selectedDate={selectedDate}
                selectedSlot={selectedSlot}
                onSelectDate={setSelectedDate}
                onSelectSlot={setSelectedSlot}
                onRetryAvailability={() => void refreshBookingContext()}
              />

              <BookingConfirmation
                application={application}
                currentStageName={eligibility.currentStageConfig.name}
                totalFees={totalFees}
                submitting={submitting}
                canConfirm={canConfirmBooking}
                onBack={onBack}
              />
            </form>
          </div>
        </div>
      )}
    </section>
  )
}
