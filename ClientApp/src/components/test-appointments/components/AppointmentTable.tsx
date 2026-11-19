import { useCallback, useMemo } from 'react'
import {
  Award,
  CalendarClock,
  Car,
  ChevronDown,
  ChevronRight,
  Clock,
  Eye,
  FileSignature,
  PlusCircle,
  XCircle,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TestAppointment, TestResponse } from '../../../types/TestAppointment'
import { formatJordanAppointmentDateTime } from '../../../utils/jordanTime'
import { isAppointmentActive, resolveAppointmentLifecycle } from '../../../utils/status'
import DataTable, {
  type DataTableColumn,
  type DataTableRowAction,
} from '../../common/DataTable'
import EmptyState from '../../common/EmptyState'
import StatusBadge from '../../common/StatusBadge'
import { Button } from '../../ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '../../ui/card'
import { canTakeOrCancelAppointment } from '../utils/appointmentSorting'
import AppointmentActions from './AppointmentActions'
import AppointmentTestDetails from './AppointmentTestDetails'

const TEST_TYPE_META: Record<
  number,
  {
    name: string
    shortName: string
    icon: typeof Eye
    color: string
    bgColor: string
    borderColor: string
    badgeBg: string
    badgeText: string
    badgeBorder: string
  }
> = {
  1: {
    name: 'Vision Test',
    shortName: 'Vision',
    icon: Eye,
    color: 'text-cyan-400',
    bgColor: 'bg-cyan-500/10',
    borderColor: 'border-cyan-500/30',
    badgeBg: 'bg-cyan-950/50',
    badgeText: 'text-cyan-300',
    badgeBorder: 'border-cyan-500/30',
  },
  2: {
    name: 'Written (Theory) Test',
    shortName: 'Written',
    icon: FileSignature,
    color: 'text-indigo-400',
    bgColor: 'bg-indigo-500/10',
    borderColor: 'border-indigo-500/30',
    badgeBg: 'bg-indigo-950/50',
    badgeText: 'text-indigo-300',
    badgeBorder: 'border-indigo-500/30',
  },
  3: {
    name: 'Practical (Street) Test',
    shortName: 'Street',
    icon: Car,
    color: 'text-emerald-400',
    bgColor: 'bg-emerald-500/10',
    borderColor: 'border-emerald-500/30',
    badgeBg: 'bg-emerald-950/50',
    badgeText: 'text-emerald-300',
    badgeBorder: 'border-emerald-500/30',
  },
}

export interface AppointmentTableProps {
  appointments: TestAppointment[]
  expandedAppointmentId: number | null
  testDetailsMap: Record<number, TestResponse | null>
  loadingTestId: number | null
  onToggleTestDetails: (appointmentId: number) => void
  onCloseTestDetails: () => void
  onTakeExamination: (appointment: TestAppointment) => void
  onRequestCancel: (appointment: TestAppointment) => void
  onShowDetails: (appointment: TestAppointment) => void
  onScheduleInitialTest: () => void
}

/**
 * Renders the Unified Test Appointment Timeline table, context menu,
 * empty-state action, and expandable examiner observations drawer.
 */
export default function AppointmentTable({
  appointments,
  expandedAppointmentId,
  testDetailsMap,
  loadingTestId,
  onToggleTestDetails,
  onCloseTestDetails,
  onTakeExamination,
  onRequestCancel,
  onShowDetails,
  onScheduleInitialTest,
}: AppointmentTableProps) {
  // Table Columns
  const columns = useMemo<DataTableColumn<TestAppointment>[]>(
    () => [
      {
        key: 'testAppointmentId',
        header: 'ID',
        sortable: true,
        sortValue: (r) => r.testAppointmentId,
        render: (r) => (
          <span className="font-mono text-xs font-bold text-cyan-400">
            #{r.testAppointmentId}
          </span>
        ),
      },
      {
        key: 'testTypeName',
        header: 'Examination Stage',
        sortable: true,
        sortValue: (r) => r.testTypeId,
        render: (r) => {
          const meta = TEST_TYPE_META[r.testTypeId] || {
            name: r.testTypeName || `Stage #${r.testTypeId}`,
            shortName: r.testTypeName || 'Exam',
            icon: Award,
            color: 'text-slate-300',
            badgeBg: 'bg-slate-900/60',
            badgeText: 'text-slate-300',
            badgeBorder: 'border-slate-700/60',
          }
          const Icon = meta.icon
          return (
            <div className="flex items-center gap-2">
              <span
                className={cn(
                  'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg border text-xs font-semibold shadow-xs',
                  meta.badgeBg,
                  meta.badgeText,
                  meta.badgeBorder,
                )}
              >
                <Icon className="size-3.5 shrink-0" />
                <span>{meta.name}</span>
              </span>
              {r.retakeTestApplicationId ? (
                <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded-sm bg-amber-950/60 text-amber-300 border border-amber-500/30 font-medium">
                  Retake
                </span>
              ) : null}
            </div>
          )
        },
      },
      {
        key: 'appointmentDate',
        header: 'Scheduled Date & Facility Time',
        sortable: true,
        sortValue: (r) => new Date(r.appointmentDate).getTime(),
        render: (r) => {
          const isActive = isAppointmentActive(r)
          return (
            <div className="flex items-center gap-2 text-xs">
              <Clock
                className={cn(
                  'size-3.5 shrink-0',
                  isActive ? 'text-cyan-400' : 'text-slate-500',
                )}
              />
              <span
                className={cn(
                  'font-mono',
                  isActive ? 'text-white font-medium' : 'text-slate-300',
                )}
              >
                {formatJordanAppointmentDateTime(r.appointmentDate)}
              </span>
            </div>
          )
        },
      },
      {
        key: 'paidFees',
        header: 'Applicable Fee',
        sortable: true,
        sortValue: (r) => r.paidFees,
        render: (r) => (
          <span className="font-mono text-xs font-semibold text-emerald-400">
            {r.paidFees.toFixed(2)} JOD
          </span>
        ),
      },
      {
        key: 'status',
        header: 'Lifecycle State',
        sortable: true,
        sortValue: (r) => resolveAppointmentLifecycle(r).state,
        render: (r) => {
          const lifecycle = resolveAppointmentLifecycle(r)
          return (
            <StatusBadge
              variant={lifecycle.statusVariant}
              label={
                lifecycle.resultLabel !== 'Not Taken'
                  ? `${lifecycle.statusLabel} (${lifecycle.resultLabel})`
                  : lifecycle.statusLabel
              }
            />
          )
        },
      },
      {
        key: 'details',
        header: 'Observations & Notes',
        align: 'center',
        render: (r) => {
          const isExpanded = expandedAppointmentId === r.testAppointmentId
          const isLoadingNotes = loadingTestId === r.testAppointmentId
          const lifecycle = resolveAppointmentLifecycle(r)
          const isCompleted = lifecycle.state.startsWith('Completed')

          if (!isCompleted && lifecycle.state !== 'NoShow') {
            return (
              <span className="text-slate-500 text-[11px] italic font-sans">
                Pending Examination
              </span>
            )
          }

          return (
            <button
              type="button"
              onClick={() => onToggleTestDetails(r.testAppointmentId)}
              className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium text-slate-300 hover:text-white bg-slate-900/60 hover:bg-slate-800/80 border border-slate-700/60 transition-all cursor-pointer"
            >
              {isLoadingNotes ? (
                <span className="text-[11px] text-cyan-400 animate-pulse">
                  Loading...
                </span>
              ) : (
                <>
                  <span className="text-[11px]">
                    {isExpanded ? 'Hide Notes' : 'View Notes'}
                  </span>
                  {isExpanded ? (
                    <ChevronDown className="size-3 text-slate-400" />
                  ) : (
                    <ChevronRight className="size-3 text-slate-400" />
                  )}
                </>
              )}
            </button>
          )
        },
      },
      {
        key: 'actions',
        header: 'Actions',
        align: 'right',
        render: (r) => (
          <AppointmentActions
            appointment={r}
            onTakeExamination={onTakeExamination}
            onRequestCancel={onRequestCancel}
            onShowDetails={onShowDetails}
          />
        ),
      },
    ],
    [
      expandedAppointmentId,
      loadingTestId,
      onToggleTestDetails,
      onTakeExamination,
      onRequestCancel,
      onShowDetails,
    ],
  )

  // Row Context Menu
  const rowContextMenu = useCallback(
    (apt: TestAppointment): DataTableRowAction<TestAppointment>[] => {
      const actions: DataTableRowAction<TestAppointment>[] = [
        {
          key: 'details',
          label: 'View Appointment Details',
          icon: <Eye className="size-4" />,
          onClick: () => onShowDetails(apt),
        },
      ]

      if (canTakeOrCancelAppointment(apt)) {
        actions.push({
          key: 'take-test',
          label: 'Take Examination',
          icon: <Award className="size-4 text-emerald-400" />,
          onClick: () => onTakeExamination(apt),
        })
        actions.push({
          key: 'cancel',
          label: 'Cancel Appointment',
          icon: <XCircle className="size-4 text-rose-400" />,
          danger: true,
          onClick: () => onRequestCancel(apt),
        })
      }

      return actions
    },
    [onShowDetails, onTakeExamination, onRequestCancel],
  )

  return (
    <Card className="border-[#1b2f52] bg-[#0c192e] text-white shadow-xl rounded-2xl overflow-hidden">
      <CardHeader className="border-b border-[#1b2f52]/60 p-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <div>
            <CardTitle className="text-sm font-bold text-white flex items-center gap-2">
              <CalendarClock className="size-4 text-cyan-400" />
              <span>Unified Test Appointment Timeline</span>
            </CardTitle>
            <CardDescription className="text-xs text-slate-400 mt-0.5">
              Chronological record of all driving test appointments for this candidate across Vision, Written, and Practical stages ({appointments.length} total records).
            </CardDescription>
          </div>
        </div>
      </CardHeader>

      {appointments.length === 0 ? (
        <div className="p-8">
          <EmptyState
            icon={<CalendarClock className="size-10 text-slate-400" />}
            message="No Test Appointments Recorded"
            description="No test appointments have been booked yet for this Local Driving License Application. You can schedule the candidate's initial Vision Test appointment directly."
          >
            <Button
              type="button"
              size="sm"
              onClick={onScheduleInitialTest}
              className="bg-cyan-600 hover:bg-cyan-500 text-white font-semibold text-xs gap-1.5 cursor-pointer rounded-xl shadow-md mt-2"
            >
              <PlusCircle className="size-4" />
              <span>Schedule Initial Test</span>
            </Button>
          </EmptyState>
        </div>
      ) : (
        <div className="p-4 space-y-3">
          <DataTable<TestAppointment>
            data={appointments}
            columns={columns}
            getRowKey={(r: TestAppointment) => r.testAppointmentId}
            rowContextMenu={rowContextMenu}
            emptyMessage="No appointments found."
          />

          {/* Inline Expanded Observation Drawer */}
          {expandedAppointmentId !== null && (
            <AppointmentTestDetails
              appointmentId={expandedAppointmentId}
              testDetails={testDetailsMap[expandedAppointmentId]}
              onClose={onCloseTestDetails}
            />
          )}
        </div>
      )}
    </Card>
  )
}
