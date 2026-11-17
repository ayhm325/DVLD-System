import { useCallback } from "react";
import {
  Users,
  Car,
  IdCard,
  ClipboardList,
  RefreshCw,
  CalendarCheck,
  ShieldAlert,
  Globe,
  FileCheck,
  CheckCircle2,
  Clock,
  Activity,
  Award,
  Layers,
} from "lucide-react";
import PageHero from "../common/PageHero";
import StatusBadge from "../common/StatusBadge";
import ErrorAlert from "../common/ErrorAlert";
import LoadingSpinner from "../common/LoadingSpinner";
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter,
  SpecularCard,
} from "@/components/ui/card";
import type { SpecularCardVariant } from "@/components/ui/SpecularCard";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { STAGE_CONFIG } from "../test-appointments/stageConfig";
import { useDashboardStatistics } from "../../hooks/useDashboardStatistics";
import { useAsyncData } from "../../hooks/useAsyncData";
import { getLocalDrivingLicenseApplicationsSummary } from "../../services/localDrivingLicenseApplicationService";
import { getLicenseClasses } from "../../services/licenseClassService";
import { getTestTypes } from "../../services/testTypeService";
import { getApplicationTypes } from "../../services/applicationTypeService";
import { formatCurrency, formatNumber } from "../../utils/formatters";
import type { Person } from "../../types/Person";
import type { ReactNode } from "react";

export type DashboardPageProps = {
  accessToken: string;
  statisticsRefreshKey?: number;
  onAddPerson?: () => void;
  onShowDetails?: (person: Person) => void;
  onEditPerson?: (person: Person) => void;
};

/**
 * Purposeful SVG Illustration: Official Application Dossier & Verification Stamp
 */
function ApplicationDossierIllustration() {
  return (
    <svg
      viewBox="0 0 120 76"
      className="h-16 w-24 shrink-0 select-none text-blue-400"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <rect
        x="8"
        y="8"
        width="64"
        height="60"
        rx="8"
        fill="#0b1528"
        stroke="#1e3a6d"
        strokeWidth="1.5"
      />
      <rect x="18" y="18" width="28" height="5" rx="2.5" fill="#3b82f6" fillOpacity="0.8" />
      <rect x="18" y="28" width="44" height="3.5" rx="1.75" fill="#475569" />
      <rect x="18" y="36" width="38" height="3.5" rx="1.75" fill="#475569" />
      <rect x="18" y="44" width="30" height="3.5" rx="1.75" fill="#334155" />
      <rect
        x="18"
        y="53"
        width="20"
        height="6"
        rx="3"
        fill="#10b981"
        fillOpacity="0.2"
        stroke="#10b981"
        strokeWidth="1"
      />
      <circle
        cx="84"
        cy="44"
        r="20"
        fill="#0f1e38"
        stroke="#3b82f6"
        strokeWidth="1.5"
      />
      <circle
        cx="84"
        cy="44"
        r="14"
        stroke="#60a5fa"
        strokeWidth="1"
        strokeDasharray="3 2"
      />
      <path
        d="M78 44.5L82 48.5L91 39.5"
        stroke="#34d399"
        strokeWidth="2.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * Purposeful SVG Illustration: Official Driving License Card & Biometric Chip
 */
function LicenseCardIllustration() {
  return (
    <svg
      viewBox="0 0 120 76"
      className="h-16 w-24 shrink-0 select-none"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <rect
        x="6"
        y="10"
        width="108"
        height="56"
        rx="9"
        fill="#0b1528"
        stroke="#1e3a6d"
        strokeWidth="1.5"
      />
      <path
        d="M7 19C7 14.5817 10.5817 11 15 11H105C109.418 11 113 14.5817 113 19V23H7V19Z"
        fill="#162a4f"
      />
      <rect x="14" y="15" width="36" height="4" rx="2" fill="#60a5fa" />
      <rect
        x="15"
        y="30"
        width="22"
        height="26"
        rx="4"
        fill="#132342"
        stroke="#3b82f6"
        strokeWidth="1.2"
      />
      <circle cx="26" cy="39" r="4.5" fill="#60a5fa" fillOpacity="0.85" />
      <path
        d="M19 52C19 48.5 22 46.5 26 46.5C30 46.5 33 48.5 33 52"
        stroke="#60a5fa"
        strokeWidth="1.5"
        strokeLinecap="round"
      />
      <rect x="44" y="31" width="36" height="4.5" rx="2.25" fill="#e2e8f0" />
      <rect x="44" y="40" width="28" height="3.5" rx="1.75" fill="#64748b" />
      <rect x="44" y="48" width="46" height="3.5" rx="1.75" fill="#475569" />
      <rect
        x="90"
        y="30"
        width="14"
        height="11"
        rx="2.5"
        fill="#f59e0b"
        fillOpacity="0.2"
        stroke="#fbbf24"
        strokeWidth="1.2"
      />
    </svg>
  );
}

/**
 * Purposeful SVG Illustration: Examination Track & Qualification Checkpoints
 */
function ExaminationTrackIllustration() {
  return (
    <svg
      viewBox="0 0 120 68"
      className="h-14 w-24 shrink-0 select-none"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <path
        d="M12 50C28 50 32 22 58 22C84 22 88 46 108 46"
        stroke="#1e3a6d"
        strokeWidth="8"
        strokeLinecap="round"
      />
      <path
        d="M12 50C28 50 32 22 58 22C84 22 88 46 108 46"
        stroke="#38bdf8"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeDasharray="4 4"
      />
      <circle cx="16" cy="50" r="6" fill="#0b1528" stroke="#38bdf8" strokeWidth="2" />
      <circle cx="58" cy="22" r="6" fill="#0b1528" stroke="#818cf8" strokeWidth="2" />
      <circle cx="104" cy="46" r="6" fill="#0b1528" stroke="#10b981" strokeWidth="2" />
      <circle cx="104" cy="46" r="2.5" fill="#10b981" />
    </svg>
  );
}

/**
 * Purposeful SVG Illustration: International Permit & Route Grid
 */
function InternationalPermitIllustration() {
  return (
    <svg
      viewBox="0 0 120 68"
      className="h-14 w-24 shrink-0 select-none"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <circle
        cx="40"
        cy="34"
        r="22"
        fill="#0b1528"
        stroke="#1e3a6d"
        strokeWidth="1.5"
      />
      <ellipse
        cx="40"
        cy="34"
        rx="10"
        ry="22"
        stroke="#3b82f6"
        strokeWidth="1.2"
        strokeOpacity="0.7"
      />
      <line x1="18" y1="34" x2="62" y2="34" stroke="#3b82f6" strokeWidth="1.2" strokeOpacity="0.7" />
      <rect
        x="68"
        y="14"
        width="40"
        height="40"
        rx="6"
        fill="#0f1e38"
        stroke="#38bdf8"
        strokeWidth="1.4"
      />
      <rect x="76" y="23" width="24" height="4" rx="2" fill="#38bdf8" />
      <rect x="76" y="32" width="18" height="3" rx="1.5" fill="#64748b" />
      <rect x="76" y="39" width="22" height="3" rx="1.5" fill="#475569" />
    </svg>
  );
}

/**
 * Purposeful SVG Illustration: Citizen & Driver Registry Composition
 */
function RegistryCompositionIllustration() {
  return (
    <svg
      viewBox="0 0 120 68"
      className="h-14 w-24 shrink-0 select-none"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <rect
        x="10"
        y="12"
        width="44"
        height="44"
        rx="8"
        fill="#0b1528"
        stroke="#1e3a6d"
        strokeWidth="1.5"
      />
      <circle cx="32" cy="28" r="6" fill="#60a5fa" />
      <path
        d="M21 46C21 41 25.5 38 32 38C38.5 38 43 41 43 46"
        stroke="#60a5fa"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <rect
        x="64"
        y="12"
        width="46"
        height="44"
        rx="8"
        fill="#0b1528"
        stroke="#059669"
        strokeWidth="1.5"
      />
      <circle cx="87" cy="28" r="6" fill="#34d399" />
      <path
        d="M76 46C76 41 80.5 38 87 38C93.5 38 98 41 98 46"
        stroke="#34d399"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}

interface UnifiedKpiCardProps {
  id: string;
  label: string;
  value: string;
  description: string;
  icon: ReactNode;
  variant: SpecularCardVariant;
  iconToneClass: string;
  topAccentClass: string;
  badge: ReactNode;
  loading?: boolean;
}

/**
 * Unified Dark Enterprise KPI Card built on SpecularCard.
 * Matches the exact surface, border, and orbital border shimmer of the analytical cards below.
 */
function UnifiedKpiCard({
  label,
  value,
  description,
  icon,
  variant,
  iconToneClass,
  topAccentClass,
  badge,
  loading = false,
}: UnifiedKpiCardProps) {
  return (
    <SpecularCard
      variant={variant}
      radius={16}
      className="flex flex-col justify-between border border-slate-800/90 bg-[#0e182a]/95 p-5 sm:p-6 shadow-lg hover:-translate-y-0.5"
    >
      {/* Subtle top semantic accent line */}
      <div
        className={cn(
          "pointer-events-none absolute inset-x-0 top-0 h-[2px] opacity-80",
          topAccentClass,
        )}
        aria-hidden="true"
      />

      {/* Top Row: Icon Box + Title + Status Badge */}
      <div className="relative z-10 flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className={cn(
              "flex size-11 shrink-0 items-center justify-center rounded-xl border transition-transform duration-300 group-hover:scale-105",
              iconToneClass,
            )}
            aria-hidden="true"
          >
            {icon}
          </div>
          <h3 className="text-xs sm:text-sm font-bold uppercase tracking-wider text-slate-100 leading-snug">
            {label}
          </h3>
        </div>
        <div className="shrink-0">{badge}</div>
      </div>

      {/* Main Counter + Clear Definition */}
      <div className="relative z-10 mt-5">
        {loading ? (
          <div className="h-10 w-28 rounded-lg bg-slate-800/80 animate-pulse border border-slate-700/60" />
        ) : (
          <strong className="block text-3xl sm:text-4xl font-extrabold text-white tracking-tight tabular-nums leading-none">
            {value}
          </strong>
        )}
        <p className="mt-2.5 text-xs sm:text-[13px] text-slate-300/90 leading-relaxed">
          {description}
        </p>
      </div>
    </SpecularCard>
  );
}

export default function DashboardPage({
  accessToken,
  statisticsRefreshKey = 0,
}: DashboardPageProps) {
  const { statistics, loading, error, retry } = useDashboardStatistics(
    accessToken,
    statisticsRefreshKey,
  );

  // Fetch supplementary real database summaries & catalogs from existing services
  const supplementaryFetcher = useCallback(async () => {
    const [localSummary, licenseClasses, testTypes, applicationTypes] =
      await Promise.all([
        getLocalDrivingLicenseApplicationsSummary(accessToken).catch(
          () => null,
        ),
        getLicenseClasses(accessToken).catch(() => []),
        getTestTypes(accessToken).catch(() => []),
        getApplicationTypes(accessToken).catch(() => []),
      ]);
    return {
      localSummary,
      licenseClasses: Array.isArray(licenseClasses) ? licenseClasses : [],
      testTypes: Array.isArray(testTypes) ? testTypes : [],
      applicationTypes: Array.isArray(applicationTypes) ? applicationTypes : [],
    };
  }, [accessToken]);

  const {
    data: supplementary,
    loading: summaryLoading,
    refresh: refreshSupplementary,
  } = useAsyncData(supplementaryFetcher, {
    enabled: Boolean(accessToken),
    initialData: {
      localSummary: null,
      licenseClasses: [],
      testTypes: [],
      applicationTypes: [],
    },
    dependencies: [statisticsRefreshKey],
  });

  const localSummary = supplementary?.localSummary ?? null;
  const licenseClasses = supplementary?.licenseClasses ?? [];
  const testTypes = supplementary?.testTypes ?? [];
  const applicationTypes = supplementary?.applicationTypes ?? [];

  const handleRefreshAll = useCallback(() => {
    retry();
    void refreshSupplementary();
  }, [retry, refreshSupplementary]);

  const hasData = !loading && !error && statistics !== null;
  const hasSummary =
    !summaryLoading && localSummary !== null && localSummary !== undefined;

  const formatStatValue = (value: number | undefined) => {
    if (loading) return "...";
    if (error) return "—";
    if (typeof value === "number") return formatNumber(value);
    return "—";
  };

  // Real API counters (0 fallback used only for proportional bar widths when data is loaded)
  const totalPeople = statistics?.totalPeople ?? 0;
  const totalDrivers = statistics?.totalDrivers ?? 0;
  const activeLicenses = statistics?.activeLicenses ?? 0;
  const localApps =
    localSummary?.totalApplications ??
    statistics?.localDrivingLicenseApplications ??
    0;
  const inProgressLocalApps = localSummary?.inProgressCount;
  const readyForIssuanceLocalApps = localSummary?.readyForIssuanceCount;
  // Unique open local applications with New status (InProgress + ReadyForIssuance),
  // excluding RetakeTestApplicationID rows stored in the shared Applications table.
  const openPendingLocalApps =
    typeof inProgressLocalApps === "number" &&
    typeof readyForIssuanceLocalApps === "number"
      ? inProgressLocalApps + readyForIssuanceLocalApps
      : statistics?.pendingApplications;
  const completedLocalApps =
    (hasSummary || hasData) && typeof openPendingLocalApps === "number"
      ? Math.max(0, localApps - openPendingLocalApps)
      : undefined;
  const internationalLicenses = statistics?.internationalLicenses ?? 0;
  const detainedLicenses = statistics?.detainedLicenses ?? 0;

  // Derived proportions computed strictly from real API counters when loaded
  const driverCoveragePct =
    hasData && totalPeople > 0
      ? Math.min(100, Math.round((totalDrivers / totalPeople) * 100))
      : null;

  const nonDriverPeople =
    hasData && totalPeople >= totalDrivers ? totalPeople - totalDrivers : null;

  const inProgressShareOfLocalAppsPct =
    hasSummary && localApps > 0 && typeof inProgressLocalApps === "number"
      ? Math.min(100, Math.round((inProgressLocalApps / localApps) * 100))
      : null;

  const totalTrackedLocalPermits = activeLicenses + detainedLicenses;
  const activeCompliancePct =
    hasData && totalTrackedLocalPermits > 0
      ? Math.round((activeLicenses / totalTrackedLocalPermits) * 100)
      : null;
  const detainedSharePct =
    hasData && totalTrackedLocalPermits > 0
      ? Math.max(0, 100 - (activeCompliancePct ?? 0))
      : null;

  const internationalToActivePct =
    hasData && activeLicenses > 0
      ? Math.min(
          100,
          Math.round((internationalLicenses / activeLicenses) * 100),
        )
      : null;

  const internationalToDriverPct =
    hasData && totalDrivers > 0
      ? Math.min(100, Math.round((internationalLicenses / totalDrivers) * 100))
      : null;

  const totalMandatoryExamFees =
    testTypes.length > 0
      ? testTypes.reduce((sum, item) => sum + (item.testTypeFees || 0), 0)
      : null;

  // Comparative workload bars (scaled relative to the largest real counter in the application workload group)
  const workloadMax = Math.max(
    1,
    localApps,
    openPendingLocalApps ?? 0,
    inProgressLocalApps ?? 0,
    readyForIssuanceLocalApps ?? 0,
    completedLocalApps ?? 0,
  );

  // Donut chart geometry for License Portfolio Distribution (enlarged for better visual breathing room)
  const totalPortfolioRecords =
    activeLicenses + internationalLicenses + detainedLicenses;
  const donutRadius = 66;
  const donutCircumference = 2 * Math.PI * donutRadius;

  const activeArc =
    hasData && totalPortfolioRecords > 0
      ? (activeLicenses / totalPortfolioRecords) * donutCircumference
      : 0;
  const intlArc =
    hasData && totalPortfolioRecords > 0
      ? (internationalLicenses / totalPortfolioRecords) * donutCircumference
      : 0;
  const detainedArc =
    hasData && totalPortfolioRecords > 0
      ? (detainedLicenses / totalPortfolioRecords) * donutCircumference
      : 0;

  const activePortfolioSharePct =
    hasData && totalPortfolioRecords > 0
      ? Math.round((activeLicenses / totalPortfolioRecords) * 100)
      : null;
  const intlPortfolioSharePct =
    hasData && totalPortfolioRecords > 0
      ? Math.round((internationalLicenses / totalPortfolioRecords) * 100)
      : null;
  const detainedPortfolioSharePct =
    hasData && totalPortfolioRecords > 0
      ? Math.round((detainedLicenses / totalPortfolioRecords) * 100)
      : null;

  return (
    <section
      className="page-content space-y-8"
      aria-label="Department Executive Dashboard"
    >
      {/* 1. Untouched Hero Section */}
      <PageHero pageId="Dashboard" />

      {/* 2. Operational Statistics Section (Unified 8-Card Executive Telemetry Matrix) */}
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
              onClick={handleRefreshAll}
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
            onRetry={handleRefreshAll}
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

      {/* 3. Application & Licensing Activity Overview */}
      <section
        className="space-y-4"
        aria-labelledby="dashboard-workflow-analytics-heading"
      >
        <div className="border-b border-slate-800/80 pb-3">
          <h2
            id="dashboard-workflow-analytics-heading"
            className="text-lg sm:text-xl font-bold text-white tracking-tight"
          >
            Application & Licensing Activity Overview
          </h2>
        </div>

        <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-stretch">
          {/* Left Analytical Card: Application Workflow & Workload Distribution Bar Chart */}
          <Card
            specular
            specularVariant="blue"
            className="xl:col-span-7 flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg sm:text-xl font-bold text-white">
                    <FileCheck className="size-5 text-blue-400 shrink-0" />
                    <span>Licensing Operations & Throughput</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    Local driving license filings, active exam pipeline, and ready-for-issuance queue
                  </CardDescription>
                </div>
                <ApplicationDossierIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-5 space-y-5 flex-1 flex flex-col justify-between">
              {/* Top Summary Counters (Directly from /LocalDrivingLicenseApplications/summary & /Dashboard/statistics) */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
                <SpecularCard
                  variant="blue"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <span className="text-xs sm:text-[13px] font-medium text-slate-300 block">
                    Local License Applications
                  </span>
                  <strong className="mt-1.5 block text-2xl sm:text-3xl font-extrabold text-white tabular-nums">
                    {formatStatValue(
                      statistics?.localDrivingLicenseApplications ??
                        localSummary?.totalApplications,
                    )}
                  </strong>
                  <span className="mt-1.5 inline-block text-xs font-medium text-blue-400">
                    Total recorded filings
                  </span>
                </SpecularCard>

                <SpecularCard
                  variant="amber"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <span className="text-xs sm:text-[13px] font-medium text-slate-300 block">
                    In Testing Pipeline
                  </span>
                  <strong className="mt-1.5 block text-2xl sm:text-3xl font-extrabold text-amber-400 tabular-nums">
                    {summaryLoading || loading
                      ? "..."
                      : typeof inProgressLocalApps === "number"
                        ? formatNumber(inProgressLocalApps)
                        : "—"}
                  </strong>
                  <span className="mt-1.5 inline-block text-xs font-medium text-slate-300">
                    Active (&lt; 3 exams passed)
                  </span>
                </SpecularCard>

                <SpecularCard
                  variant="emerald"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <span className="text-xs sm:text-[13px] font-medium text-slate-300 block">
                    Ready for Issuance
                  </span>
                  <strong className="mt-1.5 block text-2xl sm:text-3xl font-extrabold text-emerald-400 tabular-nums">
                    {summaryLoading || loading
                      ? "..."
                      : typeof readyForIssuanceLocalApps === "number"
                        ? formatNumber(readyForIssuanceLocalApps)
                        : "—"}
                  </strong>
                  <span className="mt-1.5 inline-block text-xs font-medium text-slate-300">
                    All 3 exams passed
                  </span>
                </SpecularCard>
              </div>

              {/* Comparative Horizontal Volume Bars */}
              <SpecularCard
                variant="slate"
                radius={12}
                className="space-y-4 border border-slate-800/80 bg-[#0a1220]/75 p-4 sm:p-5"
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs sm:text-sm font-semibold text-slate-100">
                    Application Queue & Stage Distribution
                  </span>
                  <span className="text-xs text-slate-400">
                    Relative volume across application states
                  </span>
                </div>

                {[
                  {
                    label: "Total Local Driving License Applications",
                    display: formatStatValue(
                      localSummary?.totalApplications ??
                        statistics?.localDrivingLicenseApplications,
                    ),
                    raw: localApps,
                    barColor: "bg-blue-500",
                    textColor: "text-blue-300",
                  },
                  {
                    label: "Open Pending Applications (New Status)",
                    display:
                      summaryLoading || loading
                        ? "..."
                        : typeof openPendingLocalApps === "number"
                          ? formatNumber(openPendingLocalApps)
                          : "—",
                    raw: openPendingLocalApps ?? 0,
                    barColor: "bg-cyan-500",
                    textColor: "text-cyan-300",
                  },
                  {
                    label: "In Testing Pipeline (< 3 Exams Passed)",
                    display:
                      summaryLoading || loading
                        ? "..."
                        : typeof inProgressLocalApps === "number"
                          ? formatNumber(inProgressLocalApps)
                          : "—",
                    raw: inProgressLocalApps ?? 0,
                    barColor: "bg-amber-500",
                    textColor: "text-amber-300",
                  },
                  {
                    label: "Ready for License Issuance (3/3 Exams Passed)",
                    display:
                      summaryLoading || loading
                        ? "..."
                        : typeof readyForIssuanceLocalApps === "number"
                          ? formatNumber(readyForIssuanceLocalApps)
                          : "—",
                    raw: readyForIssuanceLocalApps ?? 0,
                    barColor: "bg-emerald-500",
                    textColor: "text-emerald-300",
                  },
                  {
                    label: "Completed Applications (License Issued)",
                    display:
                      summaryLoading || loading
                        ? "..."
                        : typeof completedLocalApps === "number"
                          ? formatNumber(completedLocalApps)
                          : "—",
                    raw: completedLocalApps ?? 0,
                    barColor: "bg-teal-500",
                    textColor: "text-teal-300",
                  },
                ].map((row) => {
                  const widthPct =
                    (hasData || hasSummary) && row.raw > 0
                      ? Math.max(4, Math.round((row.raw / workloadMax) * 100))
                      : 0;
                  return (
                    <div key={row.label} className="space-y-1.5">
                      <div className="flex items-center justify-between text-xs sm:text-sm gap-2">
                        <span className="text-slate-200 font-medium">
                          {row.label}
                        </span>
                        <span
                          className={cn(
                            "font-extrabold tabular-nums text-sm",
                            row.textColor,
                          )}
                        >
                          {row.display}
                        </span>
                      </div>
                      <div className="h-3 w-full overflow-hidden rounded-full bg-slate-800/90">
                        <div
                          className={cn(
                            "h-full rounded-full transition-all duration-500",
                            row.barColor,
                          )}
                          style={{ width: `${widthPct}%` }}
                        />
                      </div>
                    </div>
                  );
                })}
              </SpecularCard>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Source: DVLD Local Applications Summary & Registry</span>
              <span className="tabular-nums font-semibold text-blue-300">
                {inProgressShareOfLocalAppsPct !== null
                  ? `${inProgressShareOfLocalAppsPct}% in active testing`
                  : "Synchronized"}
              </span>
            </CardFooter>
          </Card>

          {/* Right Analytical Card: License Portfolio Donut Chart & Breakdown */}
          <Card
            specular
            specularVariant="emerald"
            className="xl:col-span-5 flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg sm:text-xl font-bold text-white">
                    <Activity className="size-5 text-emerald-400 shrink-0" />
                    <span>Permit Portfolio Distribution</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    Active local permits, international IDPs, and detained licenses
                  </CardDescription>
                </div>
                <LicenseCardIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-6 pb-4 flex-1 flex flex-col justify-center">
              <div className="flex flex-col sm:flex-row items-center gap-6 sm:gap-7">
                {/* Enlarged Accessible SVG Donut Chart */}
                <div className="relative flex items-center justify-center shrink-0 p-1">
                  <svg
                    width="176"
                    height="176"
                    viewBox="0 0 176 176"
                    className="-rotate-90 transform"
                    role="img"
                    aria-label="License portfolio distribution donut chart"
                  >
                    {/* Base Track */}
                    <circle
                      cx="88"
                      cy="88"
                      r={donutRadius}
                      fill="transparent"
                      stroke="#1e293b"
                      strokeWidth="18"
                    />
                    {/* Segment 1: Active Local Licenses (Emerald) */}
                    {activeArc > 0 && (
                      <circle
                        cx="88"
                        cy="88"
                        r={donutRadius}
                        fill="transparent"
                        stroke="#10b981"
                        strokeWidth="18"
                        strokeDasharray={`${activeArc} ${donutCircumference}`}
                        strokeDashoffset={0}
                      />
                    )}
                    {/* Segment 2: International Permits (Blue) */}
                    {intlArc > 0 && (
                      <circle
                        cx="88"
                        cy="88"
                        r={donutRadius}
                        fill="transparent"
                        stroke="#3b82f6"
                        strokeWidth="18"
                        strokeDasharray={`${intlArc} ${donutCircumference}`}
                        strokeDashoffset={-activeArc}
                      />
                    )}
                    {/* Segment 3: Detained Licenses (Amber) */}
                    {detainedArc > 0 && (
                      <circle
                        cx="88"
                        cy="88"
                        r={donutRadius}
                        fill="transparent"
                        stroke="#f59e0b"
                        strokeWidth="18"
                        strokeDasharray={`${detainedArc} ${donutCircumference}`}
                        strokeDashoffset={-(activeArc + intlArc)}
                      />
                    )}
                  </svg>
                  <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
                    <span className="text-xs font-semibold uppercase tracking-wider text-slate-300">
                      Portfolio
                    </span>
                    <strong className="mt-0.5 text-2xl sm:text-3xl font-extrabold text-white tabular-nums">
                      {hasData
                        ? formatNumber(totalPortfolioRecords)
                        : loading
                          ? "..."
                          : "—"}
                    </strong>
                    <span className="text-xs text-slate-300">Permits</span>
                  </div>
                </div>

                {/* Donut Legend & Exact Counters + Portfolio Shares */}
                <div className="w-full flex-1 space-y-3">
                  <SpecularCard
                    variant="emerald"
                    radius={12}
                    className="flex items-center justify-between border border-slate-800 bg-[#0a1220]/90 px-4 py-3 hover:-translate-y-0.5"
                  >
                    <div className="flex items-center gap-3">
                      <span className="size-3.5 rounded-sm bg-emerald-500 shrink-0" />
                      <div>
                        <span className="block text-xs sm:text-sm font-semibold text-white">
                          Active Local Licenses
                        </span>
                        <span className="block text-xs text-slate-300">
                          {activePortfolioSharePct !== null
                            ? `${activePortfolioSharePct}% of permit portfolio`
                            : "Valid standard permits"}
                        </span>
                      </div>
                    </div>
                    <strong className="text-base sm:text-lg font-extrabold text-emerald-400 tabular-nums">
                      {formatStatValue(statistics?.activeLicenses)}
                    </strong>
                  </SpecularCard>

                  <SpecularCard
                    variant="blue"
                    radius={12}
                    className="flex items-center justify-between border border-slate-800 bg-[#0a1220]/90 px-4 py-3 hover:-translate-y-0.5"
                  >
                    <div className="flex items-center gap-3">
                      <span className="size-3.5 rounded-sm bg-blue-500 shrink-0" />
                      <div>
                        <span className="block text-xs sm:text-sm font-semibold text-white">
                          International Permits
                        </span>
                        <span className="block text-xs text-slate-300">
                          {intlPortfolioSharePct !== null
                            ? `${intlPortfolioSharePct}% of permit portfolio`
                            : "Cross-border IDPs"}
                        </span>
                      </div>
                    </div>
                    <strong className="text-base sm:text-lg font-extrabold text-blue-400 tabular-nums">
                      {formatStatValue(statistics?.internationalLicenses)}
                    </strong>
                  </SpecularCard>

                  <SpecularCard
                    variant="amber"
                    radius={12}
                    className="flex items-center justify-between border border-slate-800 bg-[#0a1220]/90 px-4 py-3 hover:-translate-y-0.5"
                  >
                    <div className="flex items-center gap-3">
                      <span className="size-3.5 rounded-sm bg-amber-500 shrink-0" />
                      <div>
                        <span className="block text-xs sm:text-sm font-semibold text-white">
                          Detained Licenses
                        </span>
                        <span className="block text-xs text-slate-300">
                          {detainedPortfolioSharePct !== null
                            ? `${detainedPortfolioSharePct}% of permit portfolio`
                            : "Under administrative hold"}
                        </span>
                      </div>
                    </div>
                    <strong className="text-base sm:text-lg font-extrabold text-amber-400 tabular-nums">
                      {formatStatValue(statistics?.detainedLicenses)}
                    </strong>
                  </SpecularCard>
                </div>
              </div>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Local Permit Compliance Standing</span>
              <span className="font-semibold text-emerald-400 tabular-nums">
                {activeCompliancePct !== null
                  ? `${activeCompliancePct}% active standing`
                  : "—"}
              </span>
            </CardFooter>
          </Card>
        </div>
      </section>

      {/* 4. Department Performance & Intelligence */}
      <section
        className="space-y-4"
        aria-labelledby="dashboard-intelligence-grid-heading"
      >
        <div className="border-b border-slate-800/80 pb-3">
          <h2
            id="dashboard-intelligence-grid-heading"
            className="text-lg sm:text-xl font-bold text-white tracking-tight"
          >
            Department Performance & Intelligence
          </h2>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-stretch">
          {/* Analytical Module 1: Testing & Qualification Pipeline */}
          <Card
            specular
            specularVariant="cyan"
            className="flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg font-bold text-white">
                    <CalendarCheck className="size-5 text-cyan-400 shrink-0" />
                    <span>Testing & Qualification Pipeline</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    Mandatory 3-stage sequential exams: Vision, Written Theory, and Street Practical
                  </CardDescription>
                </div>
                <ExaminationTrackIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-5 space-y-4 flex-1 flex flex-col justify-between">
              <SpecularCard
                variant="cyan"
                radius={12}
                className="flex flex-wrap items-center justify-between gap-4 border border-slate-800 bg-[#0a1220]/90 p-4"
              >
                <div className="flex items-center gap-3.5">
                  <div className="flex size-11 items-center justify-center rounded-xl bg-cyan-500/10 border border-cyan-500/25 text-cyan-400">
                    <Clock className="size-5" />
                  </div>
                  <div>
                    <span className="text-xs sm:text-sm font-medium text-slate-300 block">
                      Scheduled Exam Appointments
                    </span>
                    <strong className="text-2xl font-extrabold text-white tabular-nums">
                      {formatStatValue(statistics?.upcomingTests)}
                    </strong>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  {totalMandatoryExamFees !== null && (
                    <div className="text-right">
                      <span className="block text-xs text-slate-300">
                        Full 3-Stage Track Fees
                      </span>
                      <strong className="text-sm sm:text-base font-bold font-mono text-cyan-300 tabular-nums">
                        {formatCurrency(totalMandatoryExamFees)}
                      </strong>
                    </div>
                  )}
                  <StatusBadge variant="info" label="Active Exam Queue" />
                </div>
              </SpecularCard>

              {/* 3-Stage Sequential Architecture Visualization reusing STAGE_CONFIG + real TestType fees */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                {([1, 2, 3] as const).map((stageId) => {
                  const stage = STAGE_CONFIG[stageId];
                  const StageIcon = stage.icon;
                  const stageVariant =
                    stageId === 1 ? "cyan" : stageId === 2 ? "blue" : "emerald";
                  const dbTestType = testTypes.find(
                    (t) => t.testTypeId === stageId,
                  );
                  return (
                    <SpecularCard
                      key={stage.id}
                      variant={stageVariant}
                      radius={12}
                      className="border border-slate-800/90 bg-[#0a1220]/90 p-4 flex flex-col justify-between hover:-translate-y-0.5"
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs font-mono font-bold text-slate-300">
                          Stage 0{stage.id}
                        </span>
                        <StageIcon className={cn("size-4", stage.color)} />
                      </div>
                      <div className="mt-2.5">
                        <strong className="block text-sm font-bold text-white">
                          {dbTestType?.testTypeTitle || stage.name}
                        </strong>
                        <span className="mt-1 block text-xs text-slate-300">
                          Prerequisite: {stage.requiredPasses} passed{" "}
                          {stage.requiredPasses === 1 ? "stage" : "stages"}
                        </span>
                        {dbTestType && (
                          <span className="mt-2 inline-block rounded-md bg-slate-800/90 px-2.5 py-1 text-xs font-mono font-semibold text-cyan-300 border border-slate-700">
                            Fee: {formatCurrency(dbTestType.testTypeFees)}
                          </span>
                        )}
                      </div>
                    </SpecularCard>
                  );
                })}
              </div>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Sequential Progression Rule Enforced</span>
              <span className="text-slate-200 font-semibold">
                {testTypes.length || 3} Mandatory Stages
              </span>
            </CardFooter>
          </Card>

          {/* Analytical Module 2: Licensing Overview & Detained Compliance */}
          <Card
            specular
            specularVariant="amber"
            className="flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg font-bold text-white">
                    <ShieldAlert className="size-5 text-amber-400 shrink-0" />
                    <span>Permit Standing & Detention Monitor</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    Active local permits in good standing versus licenses under administrative hold
                  </CardDescription>
                </div>
                <LicenseCardIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-5 space-y-4 flex-1 flex flex-col justify-between">
              <div className="grid grid-cols-2 gap-3.5">
                <SpecularCard
                  variant="emerald"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs sm:text-sm font-medium text-slate-300">
                      Active Standing
                    </span>
                    <CheckCircle2 className="size-4 text-emerald-400" />
                  </div>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-emerald-400 tabular-nums">
                    {loading
                      ? "..."
                      : error
                        ? "—"
                        : activeCompliancePct !== null
                          ? `${activeCompliancePct}%`
                          : `${formatNumber(activeLicenses)} Permits`}
                  </strong>
                  <span className="mt-1 block text-xs text-slate-300">
                    {hasData
                      ? `${formatNumber(activeLicenses)} unencumbered permits`
                      : "Valid local permits"}
                  </span>
                </SpecularCard>

                <SpecularCard
                  variant="amber"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs sm:text-sm font-medium text-slate-300">
                      Detention Exposure
                    </span>
                    <ShieldAlert className="size-4 text-amber-400" />
                  </div>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-amber-400 tabular-nums">
                    {loading
                      ? "..."
                      : error
                        ? "—"
                        : detainedSharePct !== null
                          ? `${detainedSharePct}%`
                          : `${formatNumber(detainedLicenses)} Detained`}
                  </strong>
                  <span className="mt-1 block text-xs text-slate-300">
                    {hasData
                      ? `${formatNumber(detainedLicenses)} held pending release`
                      : "Impounded permits"}
                  </span>
                </SpecularCard>
              </div>

              {/* Stacked Compliance Bar */}
              <SpecularCard
                variant="slate"
                radius={12}
                className="space-y-2.5 border border-slate-800/80 bg-[#0a1220]/80 p-4"
              >
                <div className="flex items-center justify-between text-xs sm:text-sm">
                  <span className="font-semibold text-slate-100">
                    Active vs. Detained Permit Ratio
                  </span>
                  <span className="font-bold text-slate-200 tabular-nums">
                    {activeCompliancePct !== null
                      ? `${activeCompliancePct}% Active / ${detainedSharePct}% Held`
                      : "—"}
                  </span>
                </div>
                <div className="flex h-3.5 w-full overflow-hidden rounded-full bg-slate-800">
                  <div
                    className="h-full bg-emerald-500 transition-all duration-500"
                    style={{ width: `${activeCompliancePct ?? 0}%` }}
                  />
                  <div
                    className="h-full bg-amber-500 transition-all duration-500"
                    style={{ width: `${detainedSharePct ?? 0}%` }}
                  />
                </div>
              </SpecularCard>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Detention Registry Status</span>
              <StatusBadge
                variant={detainedLicenses > 0 ? "warning" : "success"}
                label={
                  detainedLicenses > 0
                    ? "Active Detentions Recorded"
                    : "Zero Detentions"
                }
              />
            </CardFooter>
          </Card>

          {/* Analytical Module 3: International Licensing & Official License Classes */}
          <Card
            specular
            specularVariant="blue"
            className="flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg font-bold text-white">
                    <Award className="size-5 text-blue-400 shrink-0" />
                    <span>Active Permits (IDP) & Coverage</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    International driving permit adoption and official vehicle license classes
                  </CardDescription>
                </div>
                <InternationalPermitIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-5 space-y-4 flex-1 flex flex-col justify-between">
              <div className="grid grid-cols-2 gap-3.5">
                <SpecularCard
                  variant="blue"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4"
                >
                  <span className="text-xs sm:text-sm font-medium text-slate-300 block">
                    IDP Adoption Rate
                  </span>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-white tabular-nums">
                    {loading
                      ? "..."
                      : error
                        ? "—"
                        : internationalToActivePct !== null
                          ? `${internationalToActivePct}%`
                          : `${formatNumber(internationalLicenses)} IDPs`}
                  </strong>
                  <span className="mt-1 block text-xs text-blue-400">
                    {hasData
                      ? `${formatNumber(internationalLicenses)} of ${formatNumber(activeLicenses)} active permits`
                      : "Cross-border permits"}
                  </span>
                </SpecularCard>

                <SpecularCard
                  variant="cyan"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4"
                >
                  <span className="text-xs sm:text-sm font-medium text-slate-300 block">
                    Vehicle License Classes
                  </span>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-cyan-300 tabular-nums">
                    {licenseClasses.length > 0
                      ? `${licenseClasses.length} Classes`
                      : "7 Classes"}
                  </strong>
                  <span className="mt-1 block text-xs text-slate-300">
                    {internationalToDriverPct !== null
                      ? `${internationalToDriverPct}% driver IDP coverage`
                      : "Official vehicle categories"}
                  </span>
                </SpecularCard>
              </div>

              {/* Coverage Bars + License Classes Preview */}
              <SpecularCard
                variant="slate"
                radius={12}
                className="space-y-3 border border-slate-800/80 bg-[#0a1220]/80 p-4"
              >
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs sm:text-sm">
                    <span className="font-semibold text-slate-200">
                      IDP Share of Active Local Licenses
                    </span>
                    <span className="font-bold text-blue-300 tabular-nums">
                      {internationalToActivePct !== null
                        ? `${internationalToActivePct}%`
                        : "—"}
                    </span>
                  </div>
                  <div className="h-2.5 w-full overflow-hidden rounded-full bg-slate-800">
                    <div
                      className="h-full rounded-full bg-blue-500 transition-all duration-500"
                      style={{ width: `${internationalToActivePct ?? 0}%` }}
                    />
                  </div>
                </div>

                {licenseClasses.length > 0 ? (
                  <div className="pt-2 border-t border-slate-800/80">
                    <span className="text-xs font-semibold text-slate-300 block mb-2.5">
                      Official Vehicle Class Specifications
                    </span>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      {licenseClasses.slice(0, 4).map((lc) => (
                        <div
                          key={lc.licenseClassId}
                          className="flex items-center justify-between rounded-lg bg-slate-900/90 px-3 py-2 text-xs border border-slate-800/80"
                        >
                          <span className="text-slate-100 font-medium truncate mr-2">
                            {lc.licenseClassName}
                          </span>
                          <span className="text-cyan-300 font-mono font-semibold shrink-0">
                            {lc.minAllowedAge}y+ • {formatCurrency(lc.licenseClassFees)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between text-xs sm:text-sm">
                      <span className="text-slate-200">
                        IDP Share of Certified Drivers
                      </span>
                      <span className="font-bold text-cyan-300 tabular-nums">
                        {internationalToDriverPct !== null
                          ? `${internationalToDriverPct}%`
                          : "—"}
                      </span>
                    </div>
                    <div className="h-2.5 w-full overflow-hidden rounded-full bg-slate-800">
                      <div
                        className="h-full rounded-full bg-cyan-500 transition-all duration-500"
                        style={{ width: `${internationalToDriverPct ?? 0}%` }}
                      />
                    </div>
                  </div>
                )}
              </SpecularCard>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Requires Valid Local Class 3 Permit</span>
              <span className="text-slate-200 font-semibold">
                Cross-Border Qualification
              </span>
            </CardFooter>
          </Card>

          {/* Analytical Module 4: Citizen & Driver Registry Composition + Service Catalog */}
          <Card
            specular
            specularVariant="emerald"
            className="flex flex-col justify-between border-slate-800/90 bg-[#0e182a]/95 shadow-lg hover:-translate-y-0.5"
          >
            <CardHeader className="border-b border-slate-800/80 pb-4">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <CardTitle className="flex items-center gap-2 text-lg font-bold text-white">
                    <Layers className="size-5 text-emerald-400 shrink-0" />
                    <span>Centralized Registry Integration</span>
                  </CardTitle>
                  <CardDescription className="text-xs sm:text-sm text-slate-300/90">
                    Civil directory breakdown and configured department service types
                  </CardDescription>
                </div>
                <RegistryCompositionIllustration />
              </div>
            </CardHeader>

            <CardContent className="pt-5 space-y-4 flex-1 flex flex-col justify-between">
              <div className="grid grid-cols-2 gap-3.5">
                <SpecularCard
                  variant="emerald"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <span className="text-xs sm:text-sm font-medium text-slate-300 block">
                    Driver Conversion Rate
                  </span>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-emerald-400 tabular-nums">
                    {loading
                      ? "..."
                      : error
                        ? "—"
                        : driverCoveragePct !== null
                          ? `${driverCoveragePct}%`
                          : `${formatNumber(totalDrivers)} Drivers`}
                  </strong>
                  <span className="mt-1 block text-xs text-slate-300">
                    {hasData
                      ? `${formatNumber(totalDrivers)} certified drivers`
                      : "Licensed operators"}
                  </span>
                </SpecularCard>

                <SpecularCard
                  variant="blue"
                  radius={12}
                  className="border border-slate-800 bg-[#0a1220]/90 p-4 hover:-translate-y-0.5"
                >
                  <span className="text-xs sm:text-sm font-medium text-slate-300 block">
                    Non-Driver Citizens
                  </span>
                  <strong className="mt-1.5 block text-2xl font-extrabold text-blue-300 tabular-nums">
                    {loading
                      ? "..."
                      : error || nonDriverPeople === null
                        ? "—"
                        : `${formatNumber(nonDriverPeople)} People`}
                  </strong>
                  <span className="mt-1 block text-xs text-slate-300">
                    {driverCoveragePct !== null
                      ? `${Math.max(0, 100 - driverCoveragePct)}% of civil directory`
                      : "Civil profiles only"}
                  </span>
                </SpecularCard>
              </div>

              {/* Population Composition Segmented Bar + Service Types */}
              <SpecularCard
                variant="slate"
                radius={12}
                className="space-y-3 border border-slate-800/80 bg-[#0a1220]/80 p-4"
              >
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs sm:text-sm">
                    <span className="font-semibold text-slate-100">
                      Registry Population Breakdown
                    </span>
                    <span className="font-bold text-slate-200 tabular-nums">
                      {hasData ? `${formatNumber(totalPeople)} Registered` : "—"}
                    </span>
                  </div>
                  <div className="flex h-3.5 w-full overflow-hidden rounded-full bg-slate-800">
                    <div
                      className="h-full bg-emerald-500 transition-all duration-500"
                      style={{ width: `${driverCoveragePct ?? 0}%` }}
                    />
                    <div
                      className="h-full bg-blue-500/70 transition-all duration-500"
                      style={{
                        width: `${driverCoveragePct !== null ? Math.max(0, 100 - driverCoveragePct) : 0}%`,
                      }}
                    />
                  </div>
                </div>

                {applicationTypes.length > 0 && (
                  <div className="pt-2 border-t border-slate-800/80">
                    <span className="text-xs font-semibold text-slate-300 block mb-2.5">
                      Configured Department Services ({applicationTypes.length} Types)
                    </span>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      {applicationTypes.slice(0, 4).map((at) => (
                        <div
                          key={at.applicationTypeId}
                          className="flex items-center justify-between rounded-lg bg-slate-900/90 px-3 py-2 text-xs border border-slate-800/80"
                        >
                          <span className="text-slate-100 font-medium truncate mr-2">
                            {at.applicationTypeTitle}
                          </span>
                          <span className="text-emerald-300 font-mono font-semibold shrink-0">
                            {formatCurrency(at.applicationTypeFees)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </SpecularCard>
            </CardContent>

            <CardFooter className="border-t border-slate-800/80 pt-3.5 text-xs sm:text-[13px] text-slate-300 flex items-center justify-between">
              <span>Civil & Driver Directory Sync</span>
              <StatusBadge variant="neutral" label="Read-Only Analytics" />
            </CardFooter>
          </Card>
        </div>
      </section>
    </section>
  );
}
