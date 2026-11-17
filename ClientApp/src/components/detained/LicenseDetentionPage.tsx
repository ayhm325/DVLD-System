import { useCallback, useEffect, useState } from "react";
import { Lock, ShieldAlert } from "lucide-react";
import { cn } from "@/lib/utils";
import PageHero from "../common/PageHero";
import DetainedLicensesPage from "./DetainedLicensesPage";
import DetainLicensePage from "./DetainLicensePage";

export type LicenseDetentionTab = "registry" | "detain";

export interface LicenseDetentionPageProps {
  accessToken: string;
  initialTab?: LicenseDetentionTab;
}

function resolveTabFromUrl(fallback?: LicenseDetentionTab): LicenseDetentionTab {
  if (typeof window === "undefined") {
    return fallback ?? "registry";
  }

  const pathname = window.location.pathname.replace(/\/+$/, "").toLowerCase();
  const params = new URLSearchParams(window.location.search);
  const tabParam = params.get("tab")?.toLowerCase();

  if (tabParam === "detain") return "detain";
  if (tabParam === "registry") return "registry";
  if (pathname === "/detain-license") return "detain";
  if (pathname === "/detained-licenses") return "registry";

  return fallback ?? "registry";
}

export default function LicenseDetentionPage({
  accessToken,
  initialTab,
}: LicenseDetentionPageProps) {
  const [activeTab, setActiveTab] = useState<LicenseDetentionTab>(() =>
    resolveTabFromUrl(initialTab),
  );
  const [registryRefreshKey, setRegistryRefreshKey] = useState(0);

  // Synchronize active tab with browser Back / Forward (popstate)
  useEffect(() => {
    const handlePopState = () => {
      setActiveTab(resolveTabFromUrl(initialTab));
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [initialTab]);

  const handleSelectTab = useCallback((nextTab: LicenseDetentionTab) => {
    setActiveTab((prevTab) => {
      if (prevTab === nextTab) return prevTab;

      if (typeof window !== "undefined") {
        const nextUrl =
          nextTab === "detain"
            ? "/license-detention?tab=detain"
            : "/license-detention";
        const currentUrl =
          window.location.pathname + window.location.search;

        if (currentUrl !== nextUrl) {
          window.history.pushState(
            { page: "License Detention", tab: nextTab, params: {} },
            "",
            nextUrl,
          );
        }
      }

      return nextTab;
    });
  }, []);

  const handleLicenseDetained = useCallback(() => {
    setRegistryRefreshKey((prev) => prev + 1);
  }, []);

  return (
    <section
      className="page-content space-y-6"
      aria-label="License Detention Module"
    >
      {/* Unified Module Hero */}
      <PageHero
        pageId="License Detention"
        title="License Detention"
        highlight="Management"
        description="Manage active and released detained driving licenses, monitor outstanding fine fees, and execute official license detention and release workflows."
      />

      {/* KPI Cards + Tabs Bar + Registry Panel */}
      <DetainedLicensesPage
        accessToken={accessToken}
        embedded
        refreshTrigger={registryRefreshKey}
        hideRegistryPanel={activeTab !== "registry"}
        onNavigateToDetain={() => handleSelectTab("detain")}
        afterStatsSlot={
          <div
            role="tablist"
            aria-label="License Detention Workflows"
            className="inline-flex flex-wrap items-center gap-2 rounded-2xl border border-[#1b2f52] bg-[#0a1426]/95 p-1.5 shadow-lg"
          >
            <button
              id="tab-detained-registry"
              type="button"
              role="tab"
              aria-selected={activeTab === "registry"}
              aria-controls="panel-detained-registry"
              onClick={() => handleSelectTab("registry")}
              className={cn(
                "inline-flex items-center gap-2.5 rounded-xl px-4 py-2.5 text-xs sm:text-sm font-bold transition-all cursor-pointer",
                activeTab === "registry"
                  ? "bg-linear-to-r from-blue-600/25 to-cyan-500/20 text-white border border-cyan-500/40 shadow-[0_0_20px_rgba(34,211,238,0.12)]"
                  : "text-slate-400 hover:text-slate-200 hover:bg-white/[0.03] border border-transparent",
              )}
            >
              <Lock
                className={cn(
                  "size-4 shrink-0",
                  activeTab === "registry" ? "text-cyan-400" : "text-slate-400",
                )}
              />
              <span>Detained Licenses Registry</span>
            </button>

            <button
              id="tab-detain-license"
              type="button"
              role="tab"
              aria-selected={activeTab === "detain"}
              aria-controls="panel-detain-license"
              onClick={() => handleSelectTab("detain")}
              className={cn(
                "inline-flex items-center gap-2.5 rounded-xl px-4 py-2.5 text-xs sm:text-sm font-bold transition-all cursor-pointer",
                activeTab === "detain"
                  ? "bg-linear-to-r from-rose-600/25 to-amber-500/20 text-white border border-rose-500/40 shadow-[0_0_20px_rgba(244,63,94,0.12)]"
                  : "text-slate-400 hover:text-slate-200 hover:bg-white/[0.03] border border-transparent",
              )}
            >
              <ShieldAlert
                className={cn(
                  "size-4 shrink-0",
                  activeTab === "detain" ? "text-rose-400" : "text-slate-400",
                )}
              />
              <span>Detain License</span>
            </button>
          </div>
        }
      />

      {/* Tab Panel 2: Detain License Workflow (rendered directly below the Tabs Bar) */}
      <div
        id="panel-detain-license"
        role="tabpanel"
        aria-labelledby="tab-detain-license"
        hidden={activeTab !== "detain"}
        className={cn(activeTab !== "detain" && "hidden")}
      >
        <DetainLicensePage
          accessToken={accessToken}
          embedded
          onDetained={handleLicenseDetained}
        />
      </div>
    </section>
  );
}
