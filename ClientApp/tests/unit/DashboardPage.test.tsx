import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import DashboardPage from "../../src/components/dashboard/DashboardPage";
import * as dashboardService from "../../src/services/dashboardService";
import * as localAppService from "../../src/services/localDrivingLicenseApplicationService";

describe("DashboardPage — Enterprise Analytical Redesign", () => {
  const mockStatistics = {
    totalPeople: 1250,
    totalDrivers: 840,
    activeLicenses: 620,
    pendingApplications: 45,
    localDrivingLicenseApplications: 110,
    internationalLicenses: 72,
    detainedLicenses: 14,
    upcomingTests: 18,
  };

  const mockLocalSummary = {
    totalApplications: 110,
    inProgressCount: 28,
    readyForIssuanceCount: 9,
  };

  it("renders all real statistics, local workflow summary, analytical sections, and excludes navigation shortcut buttons", async () => {
    const getStatsSpy = vi
      .spyOn(dashboardService, "getDashboardStatistics")
      .mockResolvedValue(mockStatistics);
    const getSummarySpy = vi
      .spyOn(localAppService, "getLocalDrivingLicenseApplicationsSummary")
      .mockResolvedValue(mockLocalSummary);

    render(<DashboardPage accessToken="test-token" />);

    // 1. Verify primary Operational Statistics KPIs & Local Summary render
    await waitFor(() => {
      expect(screen.getByText("Total People")).toBeInTheDocument();
      expect(screen.getByText("Total Drivers")).toBeInTheDocument();
      expect(screen.getByText("Active Licenses")).toBeInTheDocument();
      expect(screen.getByText("Pending Applications")).toBeInTheDocument();
      expect(screen.getByText("1,250")).toBeInTheDocument();
      expect(screen.getByText("In Testing Pipeline")).toBeInTheDocument();
      expect(screen.getByText("Ready for Issuance")).toBeInTheDocument();
    });

    // 2. Verify redesigned analytical sections and visualizations render
    expect(
      screen.getByText("Application & Licensing Activity Overview"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Licensing Operations & Throughput"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Permit Portfolio Distribution"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Department Performance & Intelligence"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Testing & Qualification Pipeline"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Permit Standing & Detention Monitor"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Active Permits (IDP) & Coverage"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Centralized Registry Integration"),
    ).toBeInTheDocument();

    // 3. Verify that shortcut/navigation buttons and embedded PeopleTable are absent
    expect(
      screen.queryByRole("button", { name: /new application/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /book exam/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /renew license/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /register person/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByPlaceholderText(/search people/i),
    ).not.toBeInTheDocument();

    // 4. Verify Refresh Counters button refreshes both endpoints
    const refreshBtn = screen.getByRole("button", {
      name: /refresh counters/i,
    });
    fireEvent.click(refreshBtn);
    expect(getStatsSpy).toHaveBeenCalledTimes(2);
    expect(getSummarySpy).toHaveBeenCalledTimes(2);
  });

  it("displays error alert and supports retry when statistics fetch fails", async () => {
    const getStatsSpy = vi
      .spyOn(dashboardService, "getDashboardStatistics")
      .mockRejectedValueOnce(new Error("Database connection timeout"))
      .mockResolvedValueOnce(mockStatistics);
    vi.spyOn(
      localAppService,
      "getLocalDrivingLicenseApplicationsSummary",
    ).mockResolvedValue(mockLocalSummary);

    render(<DashboardPage accessToken="test-token" />);

    await waitFor(() => {
      expect(screen.getByText("Statistics Loading Error")).toBeInTheDocument();
      expect(
        screen.getByText("Database connection timeout"),
      ).toBeInTheDocument();
    });

    const retryBtn = screen.getByRole("button", { name: /retry statistics/i });
    fireEvent.click(retryBtn);

    await waitFor(() => {
      expect(screen.getByText("1,250")).toBeInTheDocument();
    });
    expect(getStatsSpy).toHaveBeenCalledTimes(2);
  });

  it("computes unique Open Pending Applications (2) and Completed Applications (3) accurately when total is 5", async () => {
    vi.spyOn(dashboardService, "getDashboardStatistics").mockResolvedValue({
      totalPeople: 5,
      totalDrivers: 3,
      activeLicenses: 2,
      pendingApplications: 4, // includes 2 RetakeTestApplicationID rows in Applications
      localDrivingLicenseApplications: 5,
      internationalLicenses: 3,
      detainedLicenses: 1,
      upcomingTests: 2,
    });
    vi.spyOn(
      localAppService,
      "getLocalDrivingLicenseApplicationsSummary",
    ).mockResolvedValue({
      totalApplications: 5,
      inProgressCount: 2,
      readyForIssuanceCount: 0,
    });

    render(<DashboardPage accessToken="test-token" />);

    await waitFor(() => {
      expect(
        screen.getByText("Open Pending Applications (New Status)"),
      ).toBeInTheDocument();
      expect(
        screen.getByText("Completed Applications (License Issued)"),
      ).toBeInTheDocument();
    });

    const openPendingRow = screen
      .getByText("Open Pending Applications (New Status)")
      .closest("div");
    expect(openPendingRow).toHaveTextContent("2");

    const completedRow = screen
      .getByText("Completed Applications (License Issued)")
      .closest("div");
    expect(completedRow).toHaveTextContent("3");
  });
});
