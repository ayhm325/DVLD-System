import { Award, Eye, XCircle } from 'lucide-react'
import type { TestAppointment } from '../../../types/TestAppointment'
import { Button } from '../../ui/button'
import { canTakeOrCancelAppointment } from '../utils/appointmentSorting'

export interface AppointmentActionsProps {
  appointment: TestAppointment
  onTakeExamination: (appointment: TestAppointment) => void
  onRequestCancel: (appointment: TestAppointment) => void
  onShowDetails: (appointment: TestAppointment) => void
}

/**
 * Renders row-level action buttons (Take Exam, Cancel, Details) for an appointment record.
 */
export default function AppointmentActions({
  appointment,
  onTakeExamination,
  onRequestCancel,
  onShowDetails,
}: AppointmentActionsProps) {
  const canAct = canTakeOrCancelAppointment(appointment)

  return (
    <div className="flex items-center justify-end gap-1.5">
      {canAct && (
        <Button
          type="button"
          variant="success"
          size="sm"
          onClick={() => onTakeExamination(appointment)}
          className="h-7 px-2.5 text-xs"
          title="Conduct and record examination outcome"
        >
          <Award className="size-3.5" />
          <span>Take Exam</span>
        </Button>
      )}

      {canAct && (
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={() => onRequestCancel(appointment)}
          className="h-7 px-2.5 text-xs"
          title="Cancel scheduled appointment slot"
        >
          <XCircle className="size-3.5" />
          <span>Cancel</span>
        </Button>
      )}

      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => onShowDetails(appointment)}
        className="h-7 px-2.5 text-xs"
        title="View comprehensive audit details"
      >
        <Eye className="size-3.5" />
        <span>Details</span>
      </Button>
    </div>
  )
}
