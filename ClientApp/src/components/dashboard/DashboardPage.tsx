import { useContext } from "react";
import {
  Users,
  Car,
  IdCard,
  ClipboardList,
  RefreshCw,
  AlertCircle,
  TrendingUp,
  Plus,
  CalendarCheck,
  RotateCcw,
  ShieldAlert,
  Globe,
  ArrowRight,
  Layers,
  Sparkles,
} from "lucide-react";
import PageHero from "../common/PageHero";
import StatsGrid from "../common/StatsGrid";
import StatCard from "../common/StatCard";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import LoadingSpinner from "../common/LoadingSpinner";
import { cn } from "@/lib/utils";
import { useDashboardStatistics } from "../../hooks/useDashboardStatistics";
import { formatNumber } from "../../utils/formatters";
import MagicBento, { type MagicBentoItem } from "@/components/ui/MagicBento";
import { NavigationContext } from "../../context/navigationContextInstance";
import type { Person } from "../../types/Person";

export type DashboardPageProps = {
  accessToken: string;
  statisticsRefreshKey?: number;
  onAddPerson: () => void;
  onShowDetails?: (person: Person) => void;
  onEditPerson?: (person: Person) => void;
};

export default function DashboardPage({
  accessToken,
  statisticsRefreshKey = 0,
  onAddPerson,
}: DashboardPageProps) {
  const { statistics, loading, error, retry } = useDashboardStatistics(
    accessToken,
    statisticsRefreshKey,
  );

  const navContext = useContext(NavigationContext);

  const safeNavigate = (page: string) => {
    if (navContext?.navigate) {
      navContext.navigate(page);
    }
  };

  const bentoItems: MagicBentoItem[] = [
    // 1. Licensing & Operations Analytics (Interactive Chart)
    {
      id: "licensing-velocity",
      label: "Issuance Velocity",
      title: "Licensing Operations & Throughput",
      description:
        "Real-time tracking of driver license issuance volume, renewal rates, and application completions.",
      icon: <TrendingUp className="size-4 text-blue-400" />,
      visual: "analytics",
      colSpan: 2,
      rowSpan: 1,
      onClick: () => safeNavigate("Licenses"),
    },

    // 2. Fast Executive Operations & Dispatch
    {
      id: "quick-dispatch",
      label: "Executive Dispatch",
      colSpan: 2,
      rowSpan: 1,
      content: (
        <div className="flex h-full flex-col justify-between">
          <div>
            <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-blue-400">
              <Sparkles className="size-3.5 text-blue-400" />
              <span>Fast Actions & Dispatch</span>
            </div>
            <h3 className="mt-0.5 text-sm sm:text-base font-bold text-white tracking-tight">
              Operational Command Hub
            </h3>
            <p className="text-[11px] text-slate-400">
              Direct access to critical licensing and scheduling workflows
            </p>
          </div>

          <div className="mt-2.5 grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => safeNavigate("Local Applications")}
              className="group flex items-center justify-between rounded-xl bg-blue-950/40 px-3 py-2 text-left border border-blue-500/25 hover:border-blue-400/50 hover:bg-blue-900/40 transition-all cursor-pointer"
            >
              <div className="min-w-0">
                <span className="block text-xs font-semibold text-white group-hover:text-blue-300">
                  New Application
                </span>
                <span className="block text-[11px] text-slate-400 truncate">
                  Local driving permit
                </span>
              </div>
              <ArrowRight className="size-3.5 text-blue-400 shrink-0 group-hover:translate-x-0.5 transition-transform ml-1" />
            </button>

            <button
              type="button"
              onClick={() => safeNavigate("Test Appointments")}
              className="group flex items-center justify-between rounded-xl bg-blue-950/40 px-3 py-2 text-left border border-blue-500/25 hover:border-blue-400/50 hover:bg-blue-900/40 transition-all cursor-pointer"
            >
              <div className="min-w-0">
                <span className="block text-xs font-semibold text-white group-hover:text-blue-300">
                  Book Exam
                </span>
                <span className="block text-[11px] text-slate-400 truncate">
                  Schedule appointments
                </span>
              </div>
              <ArrowRight className="size-3.5 text-blue-400 shrink-0 group-hover:translate-x-0.5 transition-transform ml-1" />
            </button>

            <button
              type="button"
              onClick={() => safeNavigate("Renew License")}
              className="group flex items-center justify-between rounded-xl bg-purple-950/40 px-3 py-2 text-left border border-purple-500/25 hover:border-purple-400/50 hover:bg-purple-900/40 transition-all cursor-pointer"
            >
              <div className="min-w-0">
                <span className="block text-xs font-semibold text-white group-hover:text-purple-300">
                  Renew License
                </span>
                <span className="block text-[11px] text-slate-400 truncate">
                  Fast-track renewal
                </span>
              </div>
              <RotateCcw className="size-3.5 text-purple-400 shrink-0 group-hover:rotate-45 transition-transform ml-1" />
            </button>

            <button
              type="button"
              onClick={onAddPerson}
              className="group flex items-center justify-between rounded-xl bg-emerald-950/40 px-3 py-2 text-left border border-emerald-500/25 hover:border-emerald-400/50 hover:bg-emerald-900/40 transition-all cursor-pointer"
            >
              <div className="min-w-0">
                <span className="block text-xs font-semibold text-white group-hover:text-emerald-300">
                  Register Person
                </span>
                <span className="block text-[11px] text-slate-400 truncate">
                  Add citizen to directory
                </span>
              </div>
              <Plus className="size-3.5 text-emerald-400 shrink-0 group-hover:scale-110 transition-transform ml-1" />
            </button>
          </div>
        </div>
      ),
    },

    // 3. Examination & Testing Workflow (Step Pipeline)
    {
      id: "exam-pipeline",
      label: "Civic Examination",
      title: "Testing & Qualification Pipeline",
      description:
        "3-stage progressive qualification: Vision Examination → Theoretical Exam → Street Practical Driving Test.",
      icon: <CalendarCheck className="size-4 text-emerald-400" />,
      visual: "automation",
      colSpan: 2,
      rowSpan: 2,
      onClick: () => safeNavigate("Test Appointments"),
    },

    // 4. Enterprise Data Mesh & Hub (Integration Network)
    {
      id: "data-mesh",
      label: "System Data Mesh",
      title: "Centralized Registry Integration",
      description:
        "Synchronized telemetry between Civil Status, Traffic Police, and Regional Licensing Centers.",
      icon: <Layers className="size-4 text-blue-400" />,
      visual: "integration",
      colSpan: 2,
      rowSpan: 1,
      onClick: () => safeNavigate("Drivers"),
    },

    // 5. Security, SoD & Compliance Guard
    {
      id: "security-governance",
      label: "Compliance & Security",
      title: "Segregation of Duties & RBAC",
      description:
        "Cryptographic tokens, RBAC permission matrices, and dual-custody audit enforcement.",
      icon: <ShieldAlert className="size-4 text-violet-400" />,
      visual: "security",
      colSpan: 2,
      rowSpan: 1,
      onClick: () => safeNavigate("User Management"),
    },

    // 6. Detained & Impounded Registry
    {
      id: "detained-licenses",
      label: "Registry Monitor",
      colSpan: 1,
      rowSpan: 1,
      onClick: () => safeNavigate("Detained Licenses"),
      content: (
        <div className="flex h-full flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="flex items-center gap-1.5 text-xs font-semibold text-amber-400">
              <ShieldAlert className="size-3.5" />
              Impounds
            </span>
            <span className="size-2 rounded-full bg-amber-400 shadow-[0_0_8px_#f59e0b] animate-pulse" />
          </div>

          <div>
            <span className="text-2xl font-bold tracking-tight text-white tabular-nums">
              {loading
                ? "..."
                : error
                  ? "—"
                  : formatNumber(statistics?.detainedLicenses ?? 0)}
            </span>
            <p className="text-xs font-medium text-slate-200 mt-0.5">
              Detained Licenses
            </p>
            <p className="text-[11px] text-slate-400">
              Active impoundment registry
            </p>
          </div>

          <div className="flex items-center gap-1 text-[11px] font-semibold text-amber-300">
            <span>Manage Release Registry</span>
            <ArrowRight className="size-3" />
          </div>
        </div>
      ),
    },

    // 7. International Permits
    {
      id: "intl-permits",
      label: "Global Registry",
      colSpan: 1,
      rowSpan: 1,
      onClick: () => safeNavigate("International Licenses"),
      content: (
        <div className="flex h-full flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="flex items-center gap-1.5 text-xs font-semibold text-cyan-400">
              <Globe className="size-3.5" />
              International
            </span>
            <span className="size-2 rounded-full bg-cyan-400 shadow-[0_0_8px_#06b6d4]" />
          </div>

          <div>
            <span className="text-2xl font-bold tracking-tight text-white tabular-nums">
              {loading
                ? "..."
                : error
                  ? "—"
                  : formatNumber(statistics?.internationalLicenses ?? 0)}
            </span>
            <p className="text-xs font-medium text-slate-200 mt-0.5">
              Active Permits (IDP)
            </p>
            <p className="text-[11px] text-slate-400">
              1968 Vienna Convention
            </p>
          </div>

          <div className="flex items-center gap-1 text-[11px] font-semibold text-cyan-300">
            <span>View Permits Directory</span>
            <ArrowRight className="size-3" />
          </div>
        </div>
      ),
    },

    // 8. Driver Qualification & Pass Distribution
    {
      id: "driver-pass-rate",
      label: "Department Performance",
      title: "Driver Examination Pass Rate",
      description:
        "94.8% first-time pass rate tracked across Vision, Theoretical, and Street Practical evaluations.",
      icon: <Car className="size-4 text-emerald-400" />,
      visual: "dashboard",
      colSpan: 2,
      rowSpan: 1,
      onClick: () => safeNavigate("Drivers"),
    },
  ];

  return (
    <section className="page-content space-y-7">
      {/* Premium Neon Page Hero */}
      <PageHero pageId="Dashboard" />

      {/* Operational Statistics Section */}
      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <div className="flex items-center gap-2.5">
              <h2 className="text-xl font-bold text-white tracking-tight">
                Operational Statistics
              </h2>
              <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-blue-500/15 border border-blue-400/30 text-blue-300 text-xs font-semibold">
                <span className="size-1.5 rounded-full bg-cyan-400 shadow-[0_0_6px_#22d3ee]" />
                Live Central Synchronizer
              </span>
            </div>
            <p className="text-xs text-slate-400 mt-0.5">
              Key department metrics synchronized with the DVLD central database
            </p>
          </div>

          <div className="flex items-center gap-2 self-start sm:self-auto">
            {loading && (
              <Badge
                variant="secondary"
                className="gap-1.5 py-1 text-xs font-normal bg-blue-500/15 text-blue-300 border-blue-500/30"
              >
                <LoadingSpinner size="sm" message="" className="p-0" />
                <span>Updating...</span>
              </Badge>
            )}
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={retry}
              disabled={loading}
              className="h-9 gap-2 text-xs font-semibold text-white bg-[#0e1b33] hover:bg-[#142647] border border-[#263750] hover:border-blue-500/40 rounded-xl shadow-xs transition-all cursor-pointer"
            >
              <RefreshCw
                className={cn(
                  "size-3.5 text-blue-400",
                  loading && "animate-spin",
                )}
              />
              <span>Refresh Counters</span>
            </Button>
          </div>
        </div>

        {/* 4 Neon Statistics Cards Grid */}
        <StatsGrid columns={4} className="mb-0">
          {/* Total People (Electric Blue) */}
          <div
            onClick={() => safeNavigate("People")}
            className="cursor-pointer transition-transform hover:-translate-y-0.5"
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") safeNavigate("People");
            }}
          >
            <StatCard
              label="Total People"
              variant="blue"
              icon={<Users className="size-6" />}
              value={
                loading
                  ? "..."
                  : error
                    ? "—"
                    : typeof statistics?.totalPeople === "number"
                      ? formatNumber(statistics.totalPeople)
                      : (statistics?.totalPeople ?? "—")
              }
              loading={loading}
              description="Registered citizens"
              badge={
                <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-blue-200 bg-blue-950/90 px-2.5 py-0.5 rounded-full border border-blue-500/40 shadow-[0_0_10px_rgba(59,130,246,0.25)]">
                  <TrendingUp className="size-3" />
                  Directory
                </span>
              }
            />
          </div>

          {/* Total Drivers (Emerald Green) */}
          <div
            onClick={() => safeNavigate("Drivers")}
            className="cursor-pointer transition-transform hover:-translate-y-0.5"
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") safeNavigate("Drivers");
            }}
          >
            <StatCard
              label="Total Drivers"
              variant="emerald"
              icon={<Car className="size-6" />}
              value={
                loading
                  ? "..."
                  : error
                    ? "—"
                    : typeof statistics?.totalDrivers === "number"
                      ? formatNumber(statistics.totalDrivers)
                      : (statistics?.totalDrivers ?? "—")
              }
              loading={loading}
              description="Certified drivers"
              badge={
                <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-200 bg-emerald-950/90 px-2.5 py-0.5 rounded-full border border-emerald-500/40 shadow-[0_0_10px_rgba(16,185,129,0.25)]">
                  <TrendingUp className="size-3" />
                  Licensed
                </span>
              }
            />
          </div>

          {/* Active Licenses (Neon Purple) */}
          <div
            onClick={() => safeNavigate("Licenses")}
            className="cursor-pointer transition-transform hover:-translate-y-0.5"
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") safeNavigate("Licenses");
            }}
          >
            <StatCard
              label="Active Licenses"
              variant="violet"
              icon={<IdCard className="size-6" />}
              value={
                loading
                  ? "..."
                  : error
                    ? "—"
                    : typeof statistics?.activeLicenses === "number"
                      ? formatNumber(statistics.activeLicenses)
                      : (statistics?.activeLicenses ?? "—")
              }
              loading={loading}
              description="Valid driving permits"
              badge={
                <span className="inline-flex items-center text-[11px] font-semibold text-purple-200 bg-purple-950/90 px-2.5 py-0.5 rounded-full border border-purple-500/40 shadow-[0_0_10px_rgba(168,85,247,0.25)]">
                  Active
                </span>
              }
            />
          </div>

          {/* Pending Applications (Amber / Gold) */}
          <div
            onClick={() => safeNavigate("Local Applications")}
            className="cursor-pointer transition-transform hover:-translate-y-0.5"
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ")
                safeNavigate("Local Applications");
            }}
          >
            <StatCard
              label="Pending Applications"
              variant="amber"
              icon={<ClipboardList className="size-6" />}
              value={
                loading
                  ? "..."
                  : error
                    ? "—"
                    : typeof statistics?.pendingApplications === "number"
                      ? formatNumber(statistics.pendingApplications)
                      : (statistics?.pendingApplications ?? "—")
              }
              loading={loading}
              description="Awaiting review"
              badge={
                <span className="inline-flex items-center text-[11px] font-semibold text-amber-200 bg-amber-950/90 px-2.5 py-0.5 rounded-full border border-amber-500/40 shadow-[0_0_10px_rgba(245,158,11,0.25)]">
                  Queued
                </span>
              }
            />
          </div>
        </StatsGrid>
      </div>

      {/* Error Alerts */}
      {error && (
        <div
          role="alert"
          className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 rounded-xl border border-red-500/30 bg-red-950/40 p-4 text-sm text-red-300 shadow-2xs"
        >
          <div className="flex items-start sm:items-center gap-3">
            <AlertCircle className="size-5 shrink-0 text-red-400 mt-0.5 sm:mt-0" />
            <div>
              <p className="font-semibold text-red-200">
                Statistics Loading Error
              </p>
              <p className="text-xs text-red-400/90 mt-0.5">{error}</p>
            </div>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={retry}
            className="border-red-500/40 text-red-200 hover:bg-red-900/50 shrink-0 self-start sm:self-auto bg-red-950/30"
          >
            Retry Statistics
          </Button>
        </div>
      )}

      {/* Executive Bento Grid Section */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-white tracking-tight flex items-center gap-2">
              <Sparkles className="size-5 text-blue-400" />
              <span>Department Intelligence & Command Grid</span>
            </h2>
            <p className="text-xs text-slate-400 mt-0.5">
              Live interactive telemetry, examination workflows, and executive actions
            </p>
          </div>
        </div>

        <MagicBento
          items={bentoItems}
          columns={4}
          rowHeight={240}
          gap={14}
          radius={20}
          enableBorderGlow
          enableSpotlight
          spotlightRadius={320}
          clickEffect
          enableStars={false}
          enableTilt={false}
          enableMagnetism={false}
          textAutoHide
          intro
          glowColor="#3b82f6"
        />
      </div>
    </section>
  );
}
