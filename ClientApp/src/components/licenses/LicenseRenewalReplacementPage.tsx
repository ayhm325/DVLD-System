import { useCallback, useEffect, useState } from "react";
import { Copy, RotateCw } from "lucide-react";
import { cn } from "@/lib/utils";
import PageHero from "../common/PageHero";
import RenewLicensePage from "./RenewLicensePage";
import ReplaceLicensePage from "./ReplaceLicensePage";

export type LicenseRenewalReplacementTab = "renew" | "replace";

export interface LicenseRenewalReplacementPageProps {
  accessToken: string;
  initialTab?: LicenseRenewalReplacementTab;
}

function resolveTabFromUrl(
  fallback?: LicenseRenewalReplacementTab,
): LicenseRenewalReplacementTab {
  if (typeof window === "undefined") {
    return fallback ?? "renew";
  }

  const pathname = window.location.pathname.replace(/\/+$/, "").toLowerCase();
  const params = new URLSearchParams(window.location.search);
  const tabParam = params.get("tab")?.toLowerCase();

  if (tabParam === "replace") return "replace";
  if (tabParam === "renew") return "renew";
  if (pathname === "/replace-license") return "replace";
  if (pathname === "/renew-license") return "renew";

  return fallback ?? "renew";
}

export default function LicenseRenewalReplacementPage({
  accessToken,
  initialTab,
}: LicenseRenewalReplacementPageProps) {
  const [activeTab, setActiveTab] = useState<LicenseRenewalReplacementTab>(() =>
    resolveTabFromUrl(initialTab),
  );

  // Synchronize active tab with browser Back / Forward (popstate)
  useEffect(() => {
    const handlePopState = () => {
      setActiveTab(resolveTabFromUrl(initialTab));
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [initialTab]);

  const handleSelectTab = useCallback((nextTab: LicenseRenewalReplacementTab) => {
    setActiveTab((prevTab) => {
      if (prevTab === nextTab) return prevTab;

      if (typeof window !== "undefined") {
        const nextUrl =
          nextTab === "replace"
            ? "/renew-replace-license?tab=replace"
            : "/renew-replace-license";
        const currentUrl =
          window.location.pathname + window.location.search;

        if (currentUrl !== nextUrl) {
          window.history.pushState(
            { page: "Renew & Replace License", tab: nextTab, params: {} },
            "",
            nextUrl,
          );
        }
      }

      return nextTab;
    });
  }, []);

  return (
    <section
      className="page-content space-y-6"
      aria-label="Renew and Replace License Module"
    >
      {/* Unified Module Hero */}
      <PageHero
        pageId="Renew & Replace License"
        title="Renew & Replace"
        highlight="License"
        description="Review expired driving licenses for renewal or issue replacement credentials for lost and damaged active licenses."
      />

      {/* KPI Cards (nothing above them) + Tabs Bar + Renew Panel */}
      <RenewLicensePage
        accessToken={accessToken}
        embedded
        hideRenewPanel={activeTab !== "renew"}
        afterStatsSlot={
          <div
            role="tablist"
            aria-label="Renew and Replace License Workflows"
            className="inline-flex flex-wrap items-center gap-2 rounded-2xl border border-[#1b2f52] bg-[#0a1426]/95 p-1.5 shadow-lg"
          >
            <button
              id="tab-renew-license"
              type="button"
              role="tab"
              aria-selected={activeTab === "renew"}
              aria-controls="panel-renew-license"
              onClick={() => handleSelectTab("renew")}
              className={cn(
                "inline-flex items-center gap-2.5 rounded-xl px-4 py-2.5 text-xs sm:text-sm font-bold transition-all cursor-pointer",
                activeTab === "renew"
                  ? "bg-linear-to-r from-blue-600/25 to-cyan-500/20 text-white border border-cyan-500/40 shadow-[0_0_20px_rgba(34,211,238,0.12)]"
                  : "text-slate-400 hover:text-slate-200 hover:bg-white/[0.03] border border-transparent",
              )}
            >
              <RotateCw
                className={cn(
                  "size-4 shrink-0",
                  activeTab === "renew" ? "text-cyan-400" : "text-slate-400",
                )}
              />
              <span>Renew License</span>
            </button>

            <button
              id="tab-replace-license"
              type="button"
              role="tab"
              aria-selected={activeTab === "replace"}
              aria-controls="panel-replace-license"
              onClick={() => handleSelectTab("replace")}
              className={cn(
                "inline-flex items-center gap-2.5 rounded-xl px-4 py-2.5 text-xs sm:text-sm font-bold transition-all cursor-pointer",
                activeTab === "replace"
                  ? "bg-linear-to-r from-violet-600/25 to-cyan-500/20 text-white border border-violet-500/40 shadow-[0_0_20px_rgba(139,92,246,0.12)]"
                  : "text-slate-400 hover:text-slate-200 hover:bg-white/[0.03] border border-transparent",
              )}
            >
              <Copy
                className={cn(
                  "size-4 shrink-0",
                  activeTab === "replace" ? "text-violet-400" : "text-slate-400",
                )}
              />
              <span>Replace License</span>
            </button>
          </div>
        }
      />

      {/* Tab Panel 2: Replace License Workflow (rendered directly below the Tabs Bar) */}
      <div
        id="panel-replace-license"
        role="tabpanel"
        aria-labelledby="tab-replace-license"
        hidden={activeTab !== "replace"}
        className={cn(activeTab !== "replace" && "hidden")}
      >
        <ReplaceLicensePage accessToken={accessToken} embedded />
      </div>
    </section>
  );
}
