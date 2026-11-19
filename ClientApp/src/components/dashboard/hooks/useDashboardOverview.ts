import { useCallback, useMemo } from "react";
import { useDashboardStatistics } from "../../../hooks/useDashboardStatistics";
import { useAsyncData } from "../../../hooks/useAsyncData";
import { getLocalDrivingLicenseApplicationsSummary } from "../../../services/localDrivingLicenseApplicationService";
import { getLicenseClasses } from "../../../services/licenseClassService";
import { getTestTypes } from "../../../services/testTypeService";
import { getApplicationTypes } from "../../../services/applicationTypeService";
import type { ApplicationType } from "../../../types/ApplicationType";
import type { DashboardStatistics } from "../../../types/Dashboard";
import type { LicenseClass } from "../../../types/LicenseClass";
import type { LocalDrivingLicenseApplicationSummary } from "../../../types/LocalDrivingLicenseApplication";
import type { TestType } from "../../../types/TestType";
import {
  computeDashboardMetrics,
  type DashboardCatalogs,
  type DashboardLoadStatus,
  type DashboardMetrics,
} from "../utils/dashboardMetrics";

export type DashboardSupplementaryData = {
  localSummary: LocalDrivingLicenseApplicationSummary | null;
  licenseClasses: LicenseClass[];
  testTypes: TestType[];
  applicationTypes: ApplicationType[];
};

export type DashboardOverviewState = {
  statistics: DashboardStatistics | null;
  localSummary: LocalDrivingLicenseApplicationSummary | null;
  catalogs: DashboardCatalogs;
  status: DashboardLoadStatus;
  metrics: DashboardMetrics;
  refreshAll: () => void;
};

const EMPTY_LICENSE_CLASSES: LicenseClass[] = [];
const EMPTY_TEST_TYPES: TestType[] = [];
const EMPTY_APPLICATION_TYPES: ApplicationType[] = [];

const INITIAL_SUPPLEMENTARY_DATA: DashboardSupplementaryData = {
  localSummary: null,
  licenseClasses: EMPTY_LICENSE_CLASSES,
  testTypes: EMPTY_TEST_TYPES,
  applicationTypes: EMPTY_APPLICATION_TYPES,
};

/**
 * Coordinates loading and refreshing dashboard operational statistics,
 * local application summaries, and department reference catalogs,
 * returning a memoized view model for dashboard presentation sections.
 */
export function useDashboardOverview(
  accessToken: string,
  statisticsRefreshKey = 0,
): DashboardOverviewState {
  const { statistics, loading, error, retry } = useDashboardStatistics(
    accessToken,
    statisticsRefreshKey,
  );

  // Fetch supplementary real database summaries & catalogs from existing services
  const supplementaryFetcher =
    useCallback(async (): Promise<DashboardSupplementaryData> => {
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
        applicationTypes: Array.isArray(applicationTypes)
          ? applicationTypes
          : [],
      };
    }, [accessToken]);

  const {
    data: supplementary,
    loading: summaryLoading,
    refresh: refreshSupplementary,
  } = useAsyncData<DashboardSupplementaryData>(supplementaryFetcher, {
    enabled: Boolean(accessToken),
    initialData: INITIAL_SUPPLEMENTARY_DATA,
    dependencies: [statisticsRefreshKey],
  });

  const localSummary = supplementary?.localSummary ?? null;
  const licenseClasses = supplementary?.licenseClasses ?? EMPTY_LICENSE_CLASSES;
  const testTypes = supplementary?.testTypes ?? EMPTY_TEST_TYPES;
  const applicationTypes =
    supplementary?.applicationTypes ?? EMPTY_APPLICATION_TYPES;

  const refreshAll = useCallback(() => {
    retry();
    void refreshSupplementary();
  }, [retry, refreshSupplementary]);

  const hasData = !loading && !error && statistics !== null;
  const hasSummary =
    !summaryLoading && localSummary !== null && localSummary !== undefined;

  const status = useMemo<DashboardLoadStatus>(
    () => ({
      loading,
      summaryLoading,
      error,
      hasData,
      hasSummary,
    }),
    [loading, summaryLoading, error, hasData, hasSummary],
  );

  const catalogs = useMemo<DashboardCatalogs>(
    () => ({
      licenseClasses,
      testTypes,
      applicationTypes,
    }),
    [licenseClasses, testTypes, applicationTypes],
  );

  const metrics = useMemo<DashboardMetrics>(
    () =>
      computeDashboardMetrics({
        statistics,
        localSummary,
        testTypes,
        hasData,
        hasSummary,
      }),
    [statistics, localSummary, testTypes, hasData, hasSummary],
  );

  return {
    statistics,
    localSummary,
    catalogs,
    status,
    metrics,
    refreshAll,
  };
}
