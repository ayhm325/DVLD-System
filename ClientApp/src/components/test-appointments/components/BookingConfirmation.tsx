import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import LoadingSpinner from '../../common/LoadingSpinner'
import { Button } from '../../ui/button'
import { Card, CardContent } from '../../ui/card'

export interface BookingConfirmationProps {
  application: LocalDrivingLicenseApplication | null
  currentStageName: string
  totalFees: number
  submitting: boolean
  canConfirm: boolean
  onBack: () => void
}

/**
 * Renders Card 5: Booking summary bar alongside Cancel and Confirm submit buttons.
 */
export default function BookingConfirmation({
  application,
  currentStageName,
  totalFees,
  submitting,
  canConfirm,
  onBack,
}: BookingConfirmationProps) {
  return (
    <Card className="border-[#1e3458] bg-[#0c1830] rounded-2xl shadow-md overflow-hidden">
      <CardContent className="p-6">
        <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4">
          <div className="space-y-1">
            <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 block">
              Booking Summary
            </span>
            <div className="text-xs text-slate-200">
              {application ? (
                <>
                  <strong className="text-white">{application.fullName}</strong>{' '}
                  • {currentStageName} •{' '}
                  <span className="text-cyan-300 font-mono font-semibold">
                    {totalFees.toFixed(2)} JOD
                  </span>
                </>
              ) : (
                'Select application to proceed'
              )}
            </div>
          </div>

          <div className="flex items-center gap-3 justify-end">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onBack}
              disabled={submitting}
              className="h-10 px-5 rounded-xl border-slate-700 hover:bg-slate-800 text-slate-300 cursor-pointer"
            >
              Cancel
            </Button>

            <Button
              type="submit"
              disabled={!canConfirm}
              className="h-10 bg-gradient-to-r from-blue-600 to-cyan-500 hover:from-blue-500 hover:to-cyan-400 text-white font-bold text-xs px-6 rounded-xl shadow-[0_0_15px_rgba(34,211,238,0.35)] cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {submitting ? (
                <div className="flex items-center gap-2">
                  <LoadingSpinner size="sm" />
                  <span>Reserving Slot…</span>
                </div>
              ) : (
                <span>Confirm & Schedule Appointment</span>
              )}
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
