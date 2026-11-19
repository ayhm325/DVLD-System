import { Activity, FileCheck } from "lucide-react";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
  SpecularCard,
} from "@/components/ui/card";
import { cn } from "@/lib/utils";
import type { DashboardStatistics } from "../../../types/Dashboard";
import type { LocalDrivingLicenseApplicationSummary } from "../../../types/LocalDrivingLicenseApplication";
import { formatNumber } from "../../../utils/formatters";
import {
  formatDashboardStatValue,
  type DashboardLoadStatus,
  type DashboardMetrics,
} from "../utils/dashboardMetrics";
import ApplicationDossierIllustration from "./illustrations/ApplicationDossierIllustration";
import LicenseCardIllustration from "./illustrations/LicenseCardIllustration";

export interface ApplicationLicensingOverviewProps {
  statistics: DashboardStatistics | null;
  localSummary: LocalDrivingLicenseApplicationSummary | null;
  metrics: DashboardMetrics;
  status: DashboardLoadStatus;
}

/**
 * Application & Licensing Activity Overview Section:
 * displays comparative workload bars and the Permit Portfolio Distribution donut chart.
 */
export default function ApplicationLicensingOverview({
  statistics,
  localSummary,
  metrics,
  status,
}: ApplicationLicensingOverviewProps) {
  const { loading, summaryLoading, error, hasData, hasSummary } = status;
  const {
    localApps,
    inProgressLocalApps,
    readyForIssuanceLocalApps,
    openPendingLocalApps,
    completedLocalApps,
    inProgressShareOfLocalAppsPct,
    activeCompliancePct,
    workloadMax,
    totalPortfolioRecords,
    donutRadius,
    donutCircumference,
    activeArc,
    intlArc,
    detainedArc,
    activePortfolioSharePct,
    intlPortfolioSharePct,
    detainedPortfolioSharePct,
  } = metrics;

  const formatStatValue = (value: number | undefined) =>
    formatDashboardStatValue(value, loading, error);

  return (
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
  );
}
