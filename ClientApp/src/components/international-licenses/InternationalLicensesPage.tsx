import { useCallback, useEffect, useMemo, useState } from "react";
import type { InternationalLicense } from "../../types/InternationalLicense";
import { getInternationalLicensesPage } from "../../services/internationalLicenseService";
import { formatNumber } from "../../utils/formatters";
import { useAsyncData, useDebounce, usePagination } from "../../hooks";
import InternationalLicenseTable from "./InternationalLicenseTable";
import PageHero from "../common/PageHero";
import StatsGrid from "../common/StatsGrid";
import StatCard from "../common/StatCard";
import TableToolbar from "../common/TableToolbar";
import ErrorAlert from "../common/ErrorAlert";
import PaginationControls from "../common/PaginationControls";
import LicenseDetailsModal from "../licenses/LicenseDetailsModal";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../ui/select";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import {
  Plus as PlusIcon,
  Globe as GlobeIcon,
  Check as CheckIcon,
  Clock as ClockIcon,
} from "lucide-react";
import "./international-licenses.css";

type Props = {
  accessToken: string;
  onIssueNew?: () => void;
  onOpenDetails?: (id: number) => void;
};

type FilterKey =
  | "all"
  | "internationalLicenseId"
  | "applicationId"
  | "driverId"
  | "issuedUsingLocalLicenseId";

const SEARCH_OPTIONS: { value: FilterKey; label: string }[] = [
  { value: "all", label: "All Fields" },
  { value: "internationalLicenseId", label: "Int'l License ID" },
  { value: "applicationId", label: "Application ID" },
  { value: "driverId", label: "Driver ID" },
  { value: "issuedUsingLocalLicenseId", label: "Local License ID" },
];

export default function InternationalLicensesPage({
  accessToken,
  onIssueNew,
  onOpenDetails,
}: Props) {
  const [selectedIntlLicense, setSelectedIntlLicense] =
    useState<InternationalLicense | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<FilterKey>("all");
  const [status, setStatus] = useState<"All" | "Active" | "Inactive">("All");
  const debouncedSearch = useDebounce(search, 350);

  const pagination = usePagination({ pageSize: 10 });
  const { resetPage } = pagination;

  useEffect(() => {
    resetPage();
  }, [debouncedSearch, filter, status, resetPage]);

  const fetcher = useCallback(
    () =>
      getInternationalLicensesPage(
        accessToken,
        pagination.page,
        pagination.pageSize,
        debouncedSearch,
        filter,
        status,
      ),
    [
      accessToken,
      pagination.page,
      pagination.pageSize,
      debouncedSearch,
      filter,
      status,
    ],
  );

  const {
    data: page,
    loading,
    error,
    refresh,
  } = useAsyncData(fetcher, {
    enabled: Boolean(accessToken),
    dependencies: [
      accessToken,
      pagination.page,
      pagination.pageSize,
      debouncedSearch,
      filter,
      status,
    ],
    defaultErrorMessage: "Could not load international licenses.",
  });

  const items = useMemo(() => page?.items ?? [], [page?.items]);
  const totalCount = page?.totalCount ?? 0;
  const isFiltered = Boolean(search.trim() || status !== "All");

  const handleResetFilters = useCallback(() => {
    setSearch("");
    setFilter("all");
    setStatus("All");
    resetPage();
  }, [resetPage]);

  const placeholderText = useMemo(() => {
    switch (filter) {
      case "internationalLicenseId":
        return "Search by international license ID...";
      case "applicationId":
        return "Search by application ID...";
      case "driverId":
        return "Search by driver ID...";
      case "issuedUsingLocalLicenseId":
        return "Search by local license ID...";
      default:
        return "Search by any ID (intl, app, driver, local)...";
    }
  }, [filter]);

  return (
    <section className="page-content intl-page">
      <PageHero pageId="International Licenses" />

      <StatsGrid columns={3}>
        <StatCard
          label="Total Licenses"
          variant="violet"
          icon={<GlobeIcon size={20} />}
          value={loading || error ? "—" : formatNumber(totalCount)}
          description="International licenses recorded"
        />

        <StatCard
          label="Active Licenses"
          variant="emerald"
          icon={<CheckIcon size={20} />}
          value={
            loading || error
              ? "—"
              : formatNumber(items.filter((item) => item.isActive).length)
          }
          description="Currently active licenses"
        />

        <StatCard
          label="Inactive Licenses"
          variant="slate"
          icon={<ClockIcon size={20} />}
          value={
            loading || error
              ? "—"
              : formatNumber(items.filter((item) => !item.isActive).length)
          }
          description="Not currently active"
        />
      </StatsGrid>

      <Card className="panel border-[#1b2f52] bg-[#0c192e]">
        <div className="panel-header">
          <div>
            <h2>International Licenses Directory</h2>
            <p>Review issued international licenses and verify validity.</p>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <span className="record-count">
              {loading
                ? "Loading..."
                : `${formatNumber(totalCount)} Licenses`}
            </span>

            {onIssueNew && (
              <Button
                type="button"
                variant="default"
                size="default"
                onClick={onIssueNew}
              >
                <PlusIcon size={16} />
                <span>Issue International License</span>
              </Button>
            )}
          </div>
        </div>

        <TableToolbar<FilterKey>
          searchValue={search}
          onSearchChange={setSearch}
          searchPlaceholder={placeholderText}
          searchField={filter}
          searchFieldOptions={SEARCH_OPTIONS}
          onSearchFieldChange={setFilter}
          filters={
            <div style={{ minWidth: 140 }} className="shrink-0">
              <Select
                value={status}
                onValueChange={(val) =>
                  setStatus(val as typeof status)
                }
              >
                <SelectTrigger
                  className="h-10 min-h-10 w-full bg-[#111e33] border-[#223656] text-xs text-white hover:border-blue-500/50 hover:bg-[#14233c] focus:border-blue-500 focus:ring-2 focus:ring-blue-500/30 rounded-lg"
                  aria-label="Filter by status"
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="All">All statuses</SelectItem>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Inactive">Inactive</SelectItem>
                </SelectContent>
              </Select>
            </div>
          }
          onReset={handleResetFilters}
          isFiltered={isFiltered}
          onRefresh={refresh}
          refreshing={loading}
        />

        {loading && (
          <p className="empty-state">Loading international licenses...</p>
        )}

        {!loading && error && (
          <div style={{ margin: "0 25px 20px" }}>
            <ErrorAlert
              message={error}
              onRetry={refresh}
              retryLabel="Try again"
            />
          </div>
        )}

        {!loading && !error && (
          <InternationalLicenseTable
            items={items}
            onOpenDetails={onOpenDetails}
            onSelectLicense={setSelectedIntlLicense}
          />
        )}

        {!loading && !error && totalCount > 0 && (
          <PaginationControls
            pageNumber={page?.pageNumber ?? pagination.page}
            pageSize={pagination.pageSize}
            totalCount={totalCount}
            totalPages={page?.totalPages ?? 1}
            hasPreviousPage={Boolean(page?.hasPreviousPage)}
            hasNextPage={Boolean(page?.hasNextPage)}
            onPreviousPage={pagination.previousPage}
            onNextPage={pagination.nextPage}
            onPageSizeChange={pagination.setPageSize}
            loading={loading}
            itemName="international licenses"
          />
        )}

        {!loading && !error && (
          <footer
            className="panel-footer"
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <span>
              Showing <strong>{items.length}</strong> of{" "}
              <strong>{totalCount}</strong> licenses
            </span>
            <span>
              Right-click any license row or click View to inspect details.
            </span>
          </footer>
        )}
      </Card>

      {/* International License Details Modal */}
      <LicenseDetailsModal
        isOpen={Boolean(selectedIntlLicense)}
        onClose={() => setSelectedIntlLicense(null)}
        accessToken={accessToken}
        target={
          selectedIntlLicense
            ? { type: "international", item: selectedIntlLicense }
            : null
        }
      />
    </section>
  );
}
