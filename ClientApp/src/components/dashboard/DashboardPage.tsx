import type { Person } from "../../types/Person";
import PageHero from "../common/PageHero";
import ApplicationLicensingOverview from "./components/ApplicationLicensingOverview";
import DashboardOperationalStats from "./components/DashboardOperationalStats";
import DepartmentIntelligence from "./components/DepartmentIntelligence";
import { useDashboardOverview } from "./hooks/useDashboardOverview";

export type DashboardPageProps = {
  accessToken: string;
  statisticsRefreshKey?: number;
  onAddPerson?: () => void;
  onShowDetails?: (person: Person) => void;
  onEditPerson?: (person: Person) => void;
};

export default function DashboardPage({
  accessToken,
  statisticsRefreshKey = 0,
}: DashboardPageProps) {
  const {
    statistics,
    localSummary,
    catalogs,
    status,
    metrics,
    refreshAll,
  } = useDashboardOverview(accessToken, statisticsRefreshKey);

  return (
    <section
      className="page-content space-y-8"
      aria-label="Department Executive Dashboard"
    >
      {/* 1. Untouched Hero Section */}
      <PageHero pageId="Dashboard" />

      {/* 2. Operational Statistics Section (Unified 8-Card Executive Telemetry Matrix) */}
      <DashboardOperationalStats
        statistics={statistics}
        localSummary={localSummary}
        metrics={metrics}
        status={status}
        onRefresh={refreshAll}
      />

      {/* 3. Application & Licensing Activity Overview */}
      <ApplicationLicensingOverview
        statistics={statistics}
        localSummary={localSummary}
        metrics={metrics}
        status={status}
      />

      {/* 4. Department Performance & Intelligence */}
      <DepartmentIntelligence
        statistics={statistics}
        catalogs={catalogs}
        metrics={metrics}
        status={status}
      />
    </section>
  );
}
