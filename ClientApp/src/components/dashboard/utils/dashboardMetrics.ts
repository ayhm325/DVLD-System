import { formatNumber } from "../../../utils/formatters";
import type { ApplicationType } from "../../../types/ApplicationType";
import type { DashboardStatistics } from "../../../types/Dashboard";
import type { LicenseClass } from "../../../types/LicenseClass";
import type { LocalDrivingLicenseApplicationSummary } from "../../../types/LocalDrivingLicenseApplication";
import type { TestType } from "../../../types/TestType";

export const DONUT_RADIUS = 66;
export const DONUT_CIRCUMFERENCE = 2 * Math.PI * DONUT_RADIUS;

export type DashboardLoadStatus = {
  loading: boolean;
  summaryLoading: boolean;
  error: string;
  hasData: boolean;
  hasSummary: boolean;
};

export type DashboardCatalogs = {
  licenseClasses: LicenseClass[];
  testTypes: TestType[];
  applicationTypes: ApplicationType[];
};

export type DashboardMetrics = {
  totalPeople: number;
  totalDrivers: number;
  activeLicenses: number;
  localApps: number;
  inProgressLocalApps: number | undefined;
  readyForIssuanceLocalApps: number | undefined;
  openPendingLocalApps: number | undefined;
  completedLocalApps: number | undefined;
  internationalLicenses: number;
  detainedLicenses: number;
  driverCoveragePct: number | null;
  nonDriverPeople: number | null;
  inProgressShareOfLocalAppsPct: number | null;
  totalTrackedLocalPermits: number;
  activeCompliancePct: number | null;
  detainedSharePct: number | null;
  internationalToActivePct: number | null;
  internationalToDriverPct: number | null;
  totalMandatoryExamFees: number | null;
  workloadMax: number;
  totalPortfolioRecords: number;
  donutRadius: number;
  donutCircumference: number;
  activeArc: number;
  intlArc: number;
  detainedArc: number;
  activePortfolioSharePct: number | null;
  intlPortfolioSharePct: number | null;
  detainedPortfolioSharePct: number | null;
};

export type DashboardMetricsInput = {
  statistics: DashboardStatistics | null;
  localSummary: LocalDrivingLicenseApplicationSummary | null;
  testTypes: TestType[];
  hasData: boolean;
  hasSummary: boolean;
};

/**
 * Formats a dashboard numeric KPI value according to loading and error state.
 */
export function formatDashboardStatValue(
  value: number | undefined,
  loading: boolean,
  error: string,
): string {
  if (loading) return "...";
  if (error) return "—";
  if (typeof value === "number") return formatNumber(value);
  return "—";
}

/**
 * Computes pure derived metrics, compliance shares, workload scales, and SVG donut
 * geometry from the loaded dashboard statistics, workflow summary, and test catalog.
 */
export function computeDashboardMetrics({
  statistics,
  localSummary,
  testTypes,
  hasData,
  hasSummary,
}: DashboardMetricsInput): DashboardMetrics {
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

  const activeArc =
    hasData && totalPortfolioRecords > 0
      ? (activeLicenses / totalPortfolioRecords) * DONUT_CIRCUMFERENCE
      : 0;
  const intlArc =
    hasData && totalPortfolioRecords > 0
      ? (internationalLicenses / totalPortfolioRecords) * DONUT_CIRCUMFERENCE
      : 0;
  const detainedArc =
    hasData && totalPortfolioRecords > 0
      ? (detainedLicenses / totalPortfolioRecords) * DONUT_CIRCUMFERENCE
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

  return {
    totalPeople,
    totalDrivers,
    activeLicenses,
    localApps,
    inProgressLocalApps,
    readyForIssuanceLocalApps,
    openPendingLocalApps,
    completedLocalApps,
    internationalLicenses,
    detainedLicenses,
    driverCoveragePct,
    nonDriverPeople,
    inProgressShareOfLocalAppsPct,
    totalTrackedLocalPermits,
    activeCompliancePct,
    detainedSharePct,
    internationalToActivePct,
    internationalToDriverPct,
    totalMandatoryExamFees,
    workloadMax,
    totalPortfolioRecords,
    donutRadius: DONUT_RADIUS,
    donutCircumference: DONUT_CIRCUMFERENCE,
    activeArc,
    intlArc,
    detainedArc,
    activePortfolioSharePct,
    intlPortfolioSharePct,
    detainedPortfolioSharePct,
  };
}
