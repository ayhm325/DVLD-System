import { describe, expect, it } from "vitest";
import {
  computeDashboardMetrics,
  DONUT_CIRCUMFERENCE,
  DONUT_RADIUS,
  formatDashboardStatValue,
} from "../../src/components/dashboard/utils/dashboardMetrics";
import type { DashboardStatistics } from "../../src/types/Dashboard";
import type { LocalDrivingLicenseApplicationSummary } from "../../src/types/LocalDrivingLicenseApplication";
import type { TestType } from "../../src/types/TestType";

describe("dashboardMetrics", () => {
  const sampleStatistics: DashboardStatistics = {
    totalPeople: 1250,
    totalDrivers: 840,
    activeLicenses: 620,
    pendingApplications: 45,
    localDrivingLicenseApplications: 110,
    internationalLicenses: 72,
    detainedLicenses: 14,
    upcomingTests: 18,
  };

  const sampleLocalSummary: LocalDrivingLicenseApplicationSummary = {
    totalApplications: 110,
    inProgressCount: 28,
    readyForIssuanceCount: 9,
  };

  const sampleTestTypes: TestType[] = [
    {
      testTypeId: 1,
      testTypeTitle: "Vision Test",
      testTypeDescription: "Eye exam",
      testTypeFees: 10,
    },
    {
      testTypeId: 2,
      testTypeTitle: "Written Test",
      testTypeDescription: "Theory exam",
      testTypeFees: 20,
    },
    {
      testTypeId: 3,
      testTypeTitle: "Street Test",
      testTypeDescription: "Practical exam",
      testTypeFees: 35,
    },
  ];

  describe("formatDashboardStatValue", () => {
    it("returns ellipsis while loading, em dash on error or undefined, and formatted numbers otherwise", () => {
      expect(formatDashboardStatValue(1250, true, "")).toBe("...");
      expect(formatDashboardStatValue(1250, false, "Network error")).toBe("—");
      expect(formatDashboardStatValue(undefined, false, "")).toBe("—");
      expect(formatDashboardStatValue(0, false, "")).toBe("0");
      expect(formatDashboardStatValue(1250, false, "")).toBe("1,250");
    });
  });

  describe("computeDashboardMetrics", () => {
    it("derives counts, ratios, fees, workload scale, and donut arcs from loaded data", () => {
      const metrics = computeDashboardMetrics({
        statistics: sampleStatistics,
        localSummary: sampleLocalSummary,
        testTypes: sampleTestTypes,
        hasData: true,
        hasSummary: true,
      });

      expect(metrics.localApps).toBe(110);
      expect(metrics.openPendingLocalApps).toBe(37); // 28 + 9 overrides pendingApplications (45)
      expect(metrics.completedLocalApps).toBe(73); // 110 - 37
      expect(metrics.driverCoveragePct).toBe(67); // round(840 / 1250 * 100)
      expect(metrics.nonDriverPeople).toBe(410); // 1250 - 840
      expect(metrics.inProgressShareOfLocalAppsPct).toBe(25); // round(28 / 110 * 100)
      expect(metrics.totalTrackedLocalPermits).toBe(634); // 620 + 14
      expect(metrics.activeCompliancePct).toBe(98); // round(620 / 634 * 100)
      expect(metrics.detainedSharePct).toBe(2); // 100 - 98
      expect(metrics.internationalToActivePct).toBe(12); // round(72 / 620 * 100)
      expect(metrics.internationalToDriverPct).toBe(9); // round(72 / 840 * 100)
      expect(metrics.totalMandatoryExamFees).toBe(65); // 10 + 20 + 35
      expect(metrics.workloadMax).toBe(110);

      expect(metrics.donutRadius).toBe(DONUT_RADIUS);
      expect(metrics.donutCircumference).toBe(DONUT_CIRCUMFERENCE);
      expect(metrics.totalPortfolioRecords).toBe(706); // 620 + 72 + 14
      expect(metrics.activeArc + metrics.intlArc + metrics.detainedArc).toBeCloseTo(
        DONUT_CIRCUMFERENCE,
        8,
      );
      expect(metrics.activePortfolioSharePct).toBe(88);
      expect(metrics.intlPortfolioSharePct).toBe(10);
      expect(metrics.detainedPortfolioSharePct).toBe(2);
    });

    it("falls back cleanly to dashboard statistics when localSummary is null", () => {
      const metrics = computeDashboardMetrics({
        statistics: sampleStatistics,
        localSummary: null,
        testTypes: [],
        hasData: true,
        hasSummary: false,
      });

      expect(metrics.localApps).toBe(110);
      expect(metrics.openPendingLocalApps).toBe(45);
      expect(metrics.completedLocalApps).toBe(65);
      expect(metrics.inProgressShareOfLocalAppsPct).toBeNull();
      expect(metrics.totalMandatoryExamFees).toBeNull();
    });

    it("handles zero denominators and unloaded state without NaN or negative outputs", () => {
      const emptyStats: DashboardStatistics = {
        totalPeople: 0,
        totalDrivers: 0,
        activeLicenses: 0,
        pendingApplications: 0,
        localDrivingLicenseApplications: 0,
        internationalLicenses: 0,
        detainedLicenses: 0,
        upcomingTests: 0,
      };

      const zeroMetrics = computeDashboardMetrics({
        statistics: emptyStats,
        localSummary: {
          totalApplications: 0,
          inProgressCount: 0,
          readyForIssuanceCount: 0,
        },
        testTypes: [],
        hasData: true,
        hasSummary: true,
      });

      expect(zeroMetrics.workloadMax).toBe(1);
      expect(zeroMetrics.driverCoveragePct).toBeNull();
      expect(zeroMetrics.activeCompliancePct).toBeNull();
      expect(zeroMetrics.detainedSharePct).toBeNull();
      expect(zeroMetrics.internationalToActivePct).toBeNull();
      expect(zeroMetrics.internationalToDriverPct).toBeNull();
      expect(zeroMetrics.activePortfolioSharePct).toBeNull();
      expect(zeroMetrics.activeArc).toBe(0);
      expect(zeroMetrics.intlArc).toBe(0);
      expect(zeroMetrics.detainedArc).toBe(0);

      const unloadedMetrics = computeDashboardMetrics({
        statistics: null,
        localSummary: null,
        testTypes: [],
        hasData: false,
        hasSummary: false,
      });

      expect(unloadedMetrics.openPendingLocalApps).toBeUndefined();
      expect(unloadedMetrics.completedLocalApps).toBeUndefined();
      expect(unloadedMetrics.nonDriverPeople).toBeNull();
      expect(unloadedMetrics.activeArc).toBe(0);
    });
  });
});
