import {
  CalendarDays,
  Clock,
  CheckCircle2,
  AlertTriangle,
  Calendar,
  XCircle,
  AlertCircle,
} from 'lucide-react'
import StatsGrid from '../common/StatsGrid'
import StatCard from '../common/StatCard'
import type { TabType, TabStats } from './appointmentStats'

export interface AppointmentSummaryProps {
  activeTab: TabType
  currentStats: TabStats
  loading: boolean
  error: boolean
  todayJordanStr: string
  statusFilter: string
  onKpiClick: (status: string) => void
}

export default function AppointmentSummary({
  activeTab,
  currentStats,
  loading,
  error,
  todayJordanStr,
  statusFilter,
  onKpiClick,
}: AppointmentSummaryProps) {
  const formatStat = (value: number) => (loading ? '...' : error ? '—' : value)

  return (
    <StatsGrid columns={5} className="lg:grid-cols-5">
      {activeTab === 'history' ? (
        <>
          <StatCard
            label="Historical Total"
            value={formatStat(currentStats.total)}
            icon={<Clock className="size-5" />}
            variant="blue"
            description="Concluded records"
            active={statusFilter === 'all'}
            onClick={() => onKpiClick('all')}
          />
          <StatCard
            label="Passed Tests"
            value={formatStat(currentStats.passed)}
            icon={<CheckCircle2 className="size-5" />}
            variant="emerald"
            description="Successful evaluations"
            active={statusFilter === 'passed'}
            onClick={() => onKpiClick('passed')}
          />
          <StatCard
            label="Failed Tests"
            value={formatStat(currentStats.failed)}
            icon={<XCircle className="size-5" />}
            variant="rose"
            description="Unsuccessful evaluations"
            active={statusFilter === 'failed'}
            onClick={() => onKpiClick('failed')}
          />
          <StatCard
            label="No-Show Tests"
            value={formatStat(currentStats.noShow)}
            icon={<AlertCircle className="size-5" />}
            variant="amber"
            description="Applicant did not attend"
            active={statusFilter === 'noshow'}
            onClick={() => onKpiClick('noshow')}
          />
          <StatCard
            label="Cancelled Records"
            value={formatStat(currentStats.cancelled)}
            icon={<AlertTriangle className="size-5" />}
            variant="slate"
            description="Soft-cancelled prior to test"
            active={statusFilter === 'cancelled'}
            onClick={() => onKpiClick('cancelled')}
          />
        </>
      ) : activeTab === 'today' ? (
        <>
          <StatCard
            label="Today's Total"
            value={formatStat(currentStats.total)}
            icon={<Calendar className="size-5" />}
            variant="blue"
            description={`Jordan facility: ${todayJordanStr}`}
            active={statusFilter === 'all'}
            onClick={() => onKpiClick('all')}
          />
          <StatCard
            label="Scheduled Today"
            value={formatStat(currentStats.scheduled)}
            icon={<Clock className="size-5" />}
            variant="purple"
            description="Awaiting examination today"
            active={statusFilter === 'scheduled'}
            onClick={() => onKpiClick('scheduled')}
          />
          <StatCard
            label="Completed Today"
            value={formatStat(currentStats.completed)}
            icon={<CheckCircle2 className="size-5" />}
            variant="emerald"
            description={`${currentStats.passed} Passed • ${currentStats.failed} Failed`}
            active={statusFilter === 'completed'}
            onClick={() => onKpiClick('completed')}
          />
          <StatCard
            label="No-Show Today"
            value={formatStat(currentStats.noShow)}
            icon={<AlertCircle className="size-5" />}
            variant="amber"
            description="Missed slot today"
            active={statusFilter === 'noshow'}
            onClick={() => onKpiClick('noshow')}
          />
          <StatCard
            label="Cancelled Today"
            value={formatStat(currentStats.cancelled)}
            icon={<AlertTriangle className="size-5" />}
            variant="rose"
            description="Cancelled today"
            active={statusFilter === 'cancelled'}
            onClick={() => onKpiClick('cancelled')}
          />
        </>
      ) : (
        <>
          <StatCard
            label="Total Appointments"
            value={formatStat(currentStats.total)}
            icon={<CalendarDays className="size-5" />}
            variant="blue"
            description="All recorded appointments"
            active={statusFilter === 'all'}
            onClick={() => onKpiClick('all')}
          />
          <StatCard
            label="Scheduled"
            value={formatStat(currentStats.scheduled)}
            icon={<Clock className="size-5" />}
            variant="purple"
            description="Awaiting examination"
            active={statusFilter === 'scheduled'}
            onClick={() => onKpiClick('scheduled')}
          />
          <StatCard
            label="Completed"
            value={formatStat(currentStats.completed)}
            icon={<CheckCircle2 className="size-5" />}
            variant="emerald"
            description={`${currentStats.passed} Passed • ${currentStats.failed} Failed`}
            active={statusFilter === 'completed'}
            onClick={() => onKpiClick('completed')}
          />
          <StatCard
            label="No-Show"
            value={formatStat(currentStats.noShow)}
            icon={<AlertCircle className="size-5" />}
            variant="amber"
            description="Applicant did not attend"
            active={statusFilter === 'noshow'}
            onClick={() => onKpiClick('noshow')}
          />
          <StatCard
            label="Cancelled"
            value={formatStat(currentStats.cancelled)}
            icon={<AlertTriangle className="size-5" />}
            variant="rose"
            description="Cancelled appointments"
            active={statusFilter === 'cancelled'}
            onClick={() => onKpiClick('cancelled')}
          />
        </>
      )}
    </StatsGrid>
  )
}
