import { FileText } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TestResponse } from '../../../types/TestAppointment'

export interface AppointmentTestDetailsProps {
  appointmentId: number
  testDetails: TestResponse | null | undefined
  onClose: () => void
}

/**
 * Inline expanded panel displaying official test result observations and examiner remarks.
 */
export default function AppointmentTestDetails({
  appointmentId,
  testDetails,
  onClose,
}: AppointmentTestDetailsProps) {
  return (
    <div className="p-4 rounded-xl border border-slate-700/60 bg-slate-950/70 space-y-2 animate-in fade-in duration-200">
      <div className="flex items-center justify-between text-xs border-b border-slate-800 pb-2">
        <span className="font-semibold text-slate-200 flex items-center gap-2">
          <FileText className="size-4 text-cyan-400" />
          <span>
            Official Test Record Observations for Appointment #{appointmentId}
          </span>
        </span>
        <button
          type="button"
          onClick={onClose}
          className="text-slate-400 hover:text-white cursor-pointer text-xs"
        >
          Close
        </button>
      </div>

      {testDetails ? (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs pt-1">
          <div>
            <span className="text-slate-400 block text-[11px]">
              Official Outcome
            </span>
            <span
              className={cn(
                'font-bold mt-0.5 inline-block',
                testDetails.testResult ? 'text-emerald-400' : 'text-rose-400',
              )}
            >
              {testDetails.testResult ? 'PASSED' : 'FAILED'}
            </span>
          </div>
          <div>
            <span className="text-slate-400 block text-[11px]">Examiner</span>
            <span className="text-slate-200 font-medium mt-0.5 block">
              {testDetails.createdByUserName ||
                `Staff User #${testDetails.createdByUserId}`}
            </span>
          </div>
          <div className="sm:col-span-3">
            <span className="text-slate-400 block text-[11px]">
              Examiner Remarks
            </span>
            <p className="text-slate-200 mt-1 bg-slate-900/60 p-2.5 rounded-lg border border-slate-800 italic leading-relaxed">
              {testDetails.notes?.trim() ||
                'No specific observations or remarks were recorded for this test examination.'}
            </p>
          </div>
        </div>
      ) : (
        <p className="text-xs text-slate-400 italic py-2">
          No official test result record was found for this appointment.
        </p>
      )}
    </div>
  )
}
