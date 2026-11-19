import {
  CalendarCheck,
  Car,
  ClipboardList,
  FileCheck,
  Globe,
  IdCard,
  RefreshCw,
  ShieldAlert,
  Users,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { DashboardStatistics } from "../../../types/Dashboard";
import type { LocalDrivingLicenseApplicationSummary } from "../../../types/LocalDrivingLicenseApplication";
import ErrorAlert from "../../common/ErrorAlert";
import LoadingSpinner from "../../common/LoadingSpinner";
import StatusBadge from "../../common/StatusBadge";
import {
  formatDashboardStatValue,
  type DashboardLoadStatus,
  type DashboardMetrics,
} from "../utils/dashboardMetrics";
import UnifiedKpiCard from "./UnifiedKpiCard";

export interface DashboardOperationalStatsProps {
  statistics: DashboardStatistics | null;
  localSummary: LocalDrivingLicenseApplicationSummary | null;
  metrics: DashboardMetrics;
  status: DashboardLoadStatus;
  onRefresh: () => void;
}

/**
 * Operational Statistics Section (Unified 8-Card Executive Telemetry Matrix).
 */
export default function DashboardOperationalStats({
  statistics,
  localSummary,
  metrics,
  status,
  onRefresh,
}: DashboardOperationalStatsProps) {
  const { loading, summaryLoading, error } = status;
  const { openPendingLocalApps, detainedLicenses } = metrics;
  const formatStatValue = (value: number | undefined) =>
    formatDashboardStatValue(value, loading, error);

  return (
    <section
      className="space-y-4"
      aria-labelledby="dashboard-operational-stats-heading"
    >
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800/80 pb-3">
        <div className="flex flex-wrap items-center gap-2.5">
          <h2
            id="dashboard-operational-stats-heading"
            className="text-lg sm:text-xl font-bold text-white tracking-tight"
          >
            Operational Statistics
          </h2>
          <StatusBadge
            variant={error ? "danger" : loading ? "warning" : "info"}
            label={
              error
                ? "Sync Error"
                : loading
                  ? "Refreshing"
                  : "Database Synchronized"
            }
          />
        </div>

        <div className="flex items-center gap-2.5">
          {(loading || summaryLoading) && (
            <Badge
              variant="secondary"
              className="gap-1.5 py-1 text-xs font-normal bg-blue-500/10 text-blue-300 border-blue-500/25"
            >
              <LoadingSpinner size="sm" message="" className="p-0" />
              <span>Updating...</span>
            </Badge>
          )}
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onRefresh}
            disabled={loading || summaryLoading}
            specularProps={{
              radius: 12,
              autoAnimate: true,
              followMouse: true,
              intensity: 1,
              idleIntensity: 0.45,
              glow: 0.95,
            }}
            className="h-9 gap-2 text-xs font-semibold text-white rounded-xl cursor-pointer"
          >
            <RefreshCw
              className={cn(
                "size-3.5 text-blue-400",
                (loading || summaryLoading) && "animate-spin",
              )}
            />
            <span>Refresh Counters</span>
          </Button>
        </div>
      </div>

      {/* Error Alert */}
      {error && (
        <ErrorAlert
          variant="error"
          title="Statistics Loading Error"
          message={error}
          onRetry={onRefresh}
          retryLabel="Retry Statistics"
        />
      )}

      {/* Unified 8-Card Enterprise KPI Matrix (2 rows x 4 columns on desktop) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 sm:gap-5">
        {/* 1. Total People */}
        <UnifiedKpiCard
          id="kpi-total-people"
          label="Total People"
          value={formatStatValue(statistics?.totalPeople)}
          description="Registered citizens and residents in the civil directory"
          icon={<Users className="size-5" />}
          variant="blue"
          iconToneClass="bg-blue-500/10 border-blue-500/25 text-blue-400"
          topAccentClass="bg-linear-to-r from-blue-500 via-cyan-400 to-transparent"
          badge={<StatusBadge variant="info" label="Directory" />}
          loading={loading}
        />

        {/* 2. Total Drivers */}
        <UnifiedKpiCard
          id="kpi-total-drivers"
          label="Total Drivers"
          value={formatStatValue(statistics?.totalDrivers)}
          description="Certified individuals holding an official driver record"
          icon={<Car className="size-5" />}
          variant="emerald"
          iconToneClass="bg-emerald-500/10 border-emerald-500/25 text-emerald-400"
          topAccentClass="bg-linear-to-r from-emerald-500 via-teal-400 to-transparent"
          badge={<StatusBadge variant="success" label="Certified" />}
          loading={loading}
        />

        {/* 3. Active Licenses */}
        <UnifiedKpiCard
          id="kpi-active-licenses"
          label="Active Licenses"
          value={formatStatValue(statistics?.activeLicenses)}
          description="Valid local driving permits currently in good standing"
          icon={<IdCard className="size-5" />}
          variant="emerald"
          iconToneClass="bg-emerald-500/10 border-emerald-500/25 text-emerald-400"
          topAccentClass="bg-linear-to-r from-emerald-500 via-cyan-400 to-transparent"
          badge={<StatusBadge variant="success" label="Active" />}
          loading={loading}
        />

        {/* 4. International Permits (IDP) */}
        <UnifiedKpiCard
          id="kpi-international-licenses"
          label="International Licenses"
          value={formatStatValue(statistics?.internationalLicenses)}
          description="Active cross-border permits issued on Class 3 licenses"
          icon={<Globe className="size-5" />}
          variant="cyan"
          iconToneClass="bg-cyan-500/10 border-cyan-500/25 text-cyan-400"
          topAccentClass="bg-linear-to-r from-cyan-400 via-blue-500 to-transparent"
          badge={<StatusBadge variant="info" label="IDP" />}
          loading={loading}
        />

        {/* 5. Local Driving License Applications */}
        <UnifiedKpiCard
          id="kpi-local-applications"
          label="Local Applications"
          value={formatStatValue(
            localSummary?.totalApplications ??
              statistics?.localDrivingLicenseApplications,
          )}
          description="Local driving license applications filed across all classes"
          icon={<FileCheck className="size-5" />}
          variant="blue"
          iconToneClass="bg-blue-500/10 border-blue-500/25 text-blue-400"
          topAccentClass="bg-linear-to-r from-blue-500 via-indigo-400 to-transparent"
          badge={<StatusBadge variant="info" label="Filings" />}
          loading={loading}
        />

        {/* 6. Pending Applications */}
        <UnifiedKpiCard
          id="kpi-pending-applications"
          label="Pending Applications"
          value={
            summaryLoading && loading
              ? "..."
              : formatStatValue(openPendingLocalApps)
          }
          description="Open local applications with New status awaiting completion"
          icon={<ClipboardList className="size-5" />}
          variant="amber"
          iconToneClass="bg-amber-500/10 border-amber-500/25 text-amber-400"
          topAccentClass="bg-linear-to-r from-amber-500 via-yellow-400 to-transparent"
          badge={<StatusBadge variant="warning" label="Pending" />}
          loading={loading || summaryLoading}
        />

        {/* 7. Upcoming Test Appointments */}
        <UnifiedKpiCard
          id="kpi-upcoming-tests"
          label="Upcoming Tests"
          value={formatStatValue(statistics?.upcomingTests)}
          description="Unlocked examination appointments scheduled across all stages"
          icon={<CalendarCheck className="size-5" />}
          variant="cyan"
          iconToneClass="bg-cyan-500/10 border-cyan-500/25 text-cyan-400"
          topAccentClass="bg-linear-to-r from-cyan-400 via-teal-400 to-transparent"
          badge={<StatusBadge variant="info" label="Scheduled" />}
          loading={loading}
        />

        {/* 8. Detained Licenses */}
        <UnifiedKpiCard
          id="kpi-detained-licenses"
          label="Detained Licenses"
          value={formatStatValue(statistics?.detainedLicenses)}
          description="Issued local licenses currently under administrative hold"
          icon={<ShieldAlert className="size-5" />}
          variant={detainedLicenses > 0 ? "amber" : "slate"}
          iconToneClass="bg-rose-500/10 border-rose-500/25 text-rose-400"
          topAccentClass="bg-linear-to-r from-rose-500 via-amber-400 to-transparent"
          badge={
            <StatusBadge
              variant={detainedLicenses > 0 ? "warning" : "success"}
              label={detainedLicenses > 0 ? "Held" : "Clear"}
            />
          }
          loading={loading}
        />
      </div>
    </section>
  );
}
