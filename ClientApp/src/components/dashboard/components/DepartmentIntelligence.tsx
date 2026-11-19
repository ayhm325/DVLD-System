import {
  Award,
  CalendarCheck,
  CheckCircle2,
  Clock,
  Layers,
  ShieldAlert,
} from "lucide-react";
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
import { formatCurrency, formatNumber } from "../../../utils/formatters";
import StatusBadge from "../../common/StatusBadge";
import { STAGE_CONFIG } from "../../test-appointments/stageConfig";
import {
  formatDashboardStatValue,
  type DashboardCatalogs,
  type DashboardLoadStatus,
  type DashboardMetrics,
} from "../utils/dashboardMetrics";
import ExaminationTrackIllustration from "./illustrations/ExaminationTrackIllustration";
import InternationalPermitIllustration from "./illustrations/InternationalPermitIllustration";
import LicenseCardIllustration from "./illustrations/LicenseCardIllustration";
import RegistryCompositionIllustration from "./illustrations/RegistryCompositionIllustration";

export interface DepartmentIntelligenceProps {
  statistics: DashboardStatistics | null;
  catalogs: DashboardCatalogs;
  metrics: DashboardMetrics;
  status: DashboardLoadStatus;
}

/**
 * Department Performance & Intelligence Section:
 * renders the 4 analytical intelligence modules for examination tracks,
 * permit detention compliance, IDP coverage, and registry composition.
 */
export default function DepartmentIntelligence({
  statistics,
  catalogs,
  metrics,
  status,
}: DepartmentIntelligenceProps) {
  const { loading, error, hasData } = status;
  const { licenseClasses, testTypes, applicationTypes } = catalogs;
  const {
    totalPeople,
    totalDrivers,
    activeLicenses,
    internationalLicenses,
    detainedLicenses,
    driverCoveragePct,
    nonDriverPeople,
    activeCompliancePct,
    detainedSharePct,
    internationalToActivePct,
    internationalToDriverPct,
    totalMandatoryExamFees,
  } = metrics;

  const formatStatValue = (value: number | undefined) =>
    formatDashboardStatValue(value, loading, error);

  return (
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
  );
}
