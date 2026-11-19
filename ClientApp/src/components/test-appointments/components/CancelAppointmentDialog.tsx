import type { TestAppointment } from '../../../types/TestAppointment'
import ConfirmModal from '../../common/ConfirmModal'

export interface CancelAppointmentDialogProps {
  appointment: TestAppointment | null
  cancelling: boolean
  onConfirm: () => void | Promise<void>
  onCancel: () => void
}

/**
 * Confirmation modal guarding irreversible soft-cancellation of a scheduled appointment.
 */
export default function CancelAppointmentDialog({
  appointment,
  cancelling,
  onConfirm,
  onCancel,
}: CancelAppointmentDialogProps) {
  if (!appointment) {
    return null
  }

  return (
    <ConfirmModal
      isOpen={Boolean(appointment)}
      title="Cancel Scheduled Appointment"
      message={`Are you sure you want to cancel Appointment #${appointment.testAppointmentId} for ${appointment.testTypeName}? Once cancelled, the slot will be locked and terminal.`}
      confirmText={cancelling ? 'Cancelling...' : 'Confirm Cancellation'}
      cancelText="Keep Appointment"
      confirmVariant="danger"
      loading={cancelling}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  )
}
