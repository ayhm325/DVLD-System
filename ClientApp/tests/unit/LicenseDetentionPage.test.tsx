import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LicenseDetentionPage from "../../src/components/detained/LicenseDetentionPage";
import LicenseRenewalReplacementPage from "../../src/components/licenses/LicenseRenewalReplacementPage";
import {
  buildVisibleSections,
  SIDEBAR_SECTIONS,
} from "../../src/components/layout/sidebar/sidebarNavigation";
import * as detainedService from "../../src/services/detainedLicenseService";
import * as licenseService from "../../src/services/licenseService";
import type { DetainedLicense } from "../../src/types/DetainedLicense";
import type { LocalLicenseInfo } from "../../src/types/License";

describe("Unified License Detention Module", () => {
  const mockDetentions: DetainedLicense[] = [
    {
      detainId: 101,
      licenseId: 12,
      detainDate: "2026-10-01T00:00:00Z",
      fineFees: 50,
      createdByUserId: 1,
      isReleased: false,
      releaseDate: null,
      releasedByUserId: null,
      releaseApplicationId: null,
      fullName: "Ahmad Al-Mansoor",
      nationalNo: "N1001",
    },
    {
      detainId: 102,
      licenseId: 15,
      detainDate: "2026-09-10T00:00:00Z",
      fineFees: 30,
      createdByUserId: 1,
      isReleased: true,
      releaseDate: "2026-09-15T00:00:00Z",
      releasedByUserId: 1,
      releaseApplicationId: 88,
      fullName: "Sara Haddad",
      nationalNo: "N2002",
    },
  ];

  const mockEligibleLicense: LocalLicenseInfo = {
    licenseId: 25,
    applicationId: 40,
    driverId: 7,
    personId: 9,
    fullName: "Khaled Obeidat",
    nationalNo: "N3003",
    gender: "Male",
    dateOfBirth: "1995-05-20T00:00:00Z",
    imagePath: null,
    licenseClassId: 3,
    licenseClass: "Class 3 - Ordinary driving license",
    licenseClassFees: 20,
    issueDate: "2025-01-01T00:00:00Z",
    expirationDate: "2035-01-01T00:00:00Z",
    notes: null,
    paidFees: 20,
    isActive: true,
    isDetained: false,
    issueReason: "FirstTime",
    createdByUserId: 1,
  };

  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState({}, "", "/license-detention");
  });

  it("consolidates sidebar items into Renew & Replace License and License Detention", () => {
    const licenseSection = SIDEBAR_SECTIONS.find(
      (section) => section.id === "license-services",
    );
    expect(licenseSection).toBeDefined();

    const pages = licenseSection!.entries
      .filter((entry) => entry.kind === "link")
      .map((entry) => entry.page);

    expect(pages).toContain("Licenses");
    expect(pages).toContain("Renew & Replace License");
    expect(pages).toContain("License Detention");
    expect(pages).not.toContain("Renew License");
    expect(pages).not.toContain("Replace License");
    expect(pages).not.toContain("Detain License");
    expect(pages).not.toContain("Detained Licenses");

    const staffVisible = buildVisibleSections("Staff");
    const staffLicenseSection = staffVisible.find(
      (s) => s.id === "license-services",
    );
    expect(staffLicenseSection).toBeDefined();
  });

  it("renders unified PageHero, defaults to Registry tab, supports search/filter, and processes Release License", async () => {
    const getDetentionsSpy = vi
      .spyOn(detainedService, "getDetainedLicensesPage")
      .mockImplementation(async (_token, _page, _size, searchQuery) => {
        const filtered = searchQuery
          ? mockDetentions.filter((d) =>
              (d.fullName ?? "").toLowerCase().includes(searchQuery.toLowerCase()),
            )
          : mockDetentions;
        return {
          items: filtered,
          pageNumber: 1,
          pageSize: 10,
          totalCount: filtered.length,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        };
      });
    const releaseSpy = vi
      .spyOn(detainedService, "releaseDetainedLicense")
      .mockResolvedValue(undefined);

    render(<LicenseDetentionPage accessToken="test-token" />);

    // Verify single Hero and default tab selection
    const registryTab = screen.getByRole("tab", {
      name: /detained licenses registry/i,
    });
    const detainTab = screen.getByRole("tab", {
      name: /^detain license$/i,
    });
    expect(registryTab).toHaveAttribute("aria-selected", "true");
    expect(detainTab).toHaveAttribute("aria-selected", "false");

    // Verify no legacy Back button exists
    expect(
      screen.queryByRole("button", { name: /back to detained licenses/i }),
    ).not.toBeInTheDocument();

    // Wait for registry table records
    await waitFor(() => {
      expect(screen.getByText("Ahmad Al-Mansoor")).toBeInTheDocument();
      expect(screen.getByText("Sara Haddad")).toBeInTheDocument();
    });

    // Test server-backed search filtering (debounced)
    const searchInput = screen.getByPlaceholderText(
      /search detained licenses/i,
    );
    fireEvent.change(searchInput, { target: { value: "Ahmad" } });
    await waitFor(() => {
      expect(screen.getByText("Ahmad Al-Mansoor")).toBeInTheDocument();
      expect(screen.queryByText("Sara Haddad")).not.toBeInTheDocument();
    });

    fireEvent.change(searchInput, { target: { value: "" } });
    await waitFor(() => {
      expect(screen.getByText("Sara Haddad")).toBeInTheDocument();
    });

    // Open Release modal for active detention (#101)
    const releaseButton = screen.getByRole("button", { name: /^release$/i });
    fireEvent.click(releaseButton);

    await waitFor(() => {
      expect(screen.getByText("Release Detained License")).toBeInTheDocument();
    });

    const confirmReleaseBtn = screen.getByRole("button", {
      name: /confirm & release/i,
    });
    fireEvent.click(confirmReleaseBtn);

    await waitFor(() => {
      expect(releaseSpy).toHaveBeenCalledWith("test-token", 101);
      expect(
        screen.getByText(/license released successfully/i),
      ).toBeInTheDocument();
    });
    expect(getDetentionsSpy).toHaveBeenCalled();
  });

  it("switches to Detain License tab, preserves state across tab switches, supports Browser Back/Forward, and executes Detain workflow", async () => {
    const getDetentionsSpy = vi
      .spyOn(detainedService, "getDetainedLicensesPage")
      .mockResolvedValue({
        items: mockDetentions,
        pageNumber: 1,
        pageSize: 10,
        totalCount: mockDetentions.length,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      });
    const getLicenseSpy = vi
      .spyOn(licenseService, "getLicenseDetailsById")
      .mockResolvedValue(mockEligibleLicense);
    const detainSpy = vi
      .spyOn(detainedService, "detainLicense")
      .mockResolvedValue({
        detainId: 305,
        licenseId: 25,
        detainDate: "2026-11-17T00:00:00Z",
        fineFees: 75,
        createdByUserId: 1,
        isReleased: false,
        releaseDate: null,
        releasedByUserId: null,
        releaseApplicationId: null,
      });

    render(<LicenseDetentionPage accessToken="test-token" />);

    await waitFor(() => {
      expect(screen.getByText("Ahmad Al-Mansoor")).toBeInTheDocument();
    });

    const registryTab = screen.getByRole("tab", {
      name: /detained licenses registry/i,
    });
    const detainTab = screen.getByRole("tab", {
      name: /^detain license$/i,
    });

    // Switch to Detain License tab
    fireEvent.click(detainTab);
    expect(detainTab).toHaveAttribute("aria-selected", "true");
    expect(window.location.search).toBe("?tab=detain");

    // Enter License ID and search
    const licenseIdInput = screen.getByLabelText(/license id/i);
    fireEvent.change(licenseIdInput, { target: { value: "25" } });

    const searchBtn = screen.getByRole("button", { name: /search license/i });
    fireEvent.click(searchBtn);

    await waitFor(() => {
      expect(getLicenseSpy).toHaveBeenCalledWith("test-token", 25);
      expect(screen.getByText("Khaled Obeidat")).toBeInTheDocument();
    });

    // Enter Fine Fee amount
    const fineInput = screen.getByLabelText(/fine fee amount/i);
    fireEvent.change(fineInput, { target: { value: "75" } });

    // Switch back to Registry tab and verify state preservation when returning to Detain tab
    fireEvent.click(registryTab);
    expect(registryTab).toHaveAttribute("aria-selected", "true");
    expect(window.location.search).toBe("");

    // Simulate Browser Back button (popstate back to ?tab=detain)
    window.history.pushState({}, "", "/license-detention?tab=detain");
    window.dispatchEvent(new PopStateEvent("popstate"));

    await waitFor(() => {
      expect(detainTab).toHaveAttribute("aria-selected", "true");
    });

    // Verify entered License and Fine Fee state was preserved across tab switches
    expect(screen.getByText("Khaled Obeidat")).toBeInTheDocument();
    expect(fineInput).toHaveValue("75");

    // Execute Detain License
    const submitDetainBtn = screen.getByRole("button", {
      name: /^detain license$/i,
    });
    fireEvent.click(submitDetainBtn);

    await waitFor(() => {
      expect(detainSpy).toHaveBeenCalledWith("test-token", {
        licenseId: 25,
        fineFees: 75,
      });
      expect(screen.getByText("Detention Registered")).toBeInTheDocument();
      expect(
        screen.getByText(/detention record id: #305/i),
      ).toBeInTheDocument();
    });

    // Verify Registry refreshed automatically after detention
    expect(getDetentionsSpy).toHaveBeenCalledTimes(2);
  });

  it("renders unified Renew & Replace License page with KPI cards above the tabs bar and switches between Renew and Replace workflows", async () => {
    vi.spyOn(licenseService, "getLicenseRenewalSummary").mockResolvedValue({
      totalExpired: 8,
      eligibleForRenewal: 5,
      ineligibleForRenewal: 3,
    });
    vi.spyOn(licenseService, "getLicensesPage").mockResolvedValue({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 10,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });
    vi.spyOn(licenseService, "getLicenseDetailsById").mockResolvedValue(
      mockEligibleLicense,
    );

    window.history.replaceState({}, "", "/renew-replace-license");
    render(<LicenseRenewalReplacementPage accessToken="test-token" />);

    const renewTab = screen.getByRole("tab", { name: /^renew license$/i });
    const replaceTab = screen.getByRole("tab", { name: /^replace license$/i });

    expect(renewTab).toHaveAttribute("aria-selected", "true");
    expect(replaceTab).toHaveAttribute("aria-selected", "false");

    await waitFor(() => {
      expect(screen.getByText("Total Expired Licenses")).toBeInTheDocument();
      expect(screen.getByText("Eligible for Renewal")).toBeInTheDocument();
      expect(screen.getByText("Ineligible for Renewal")).toBeInTheDocument();
    });

    // Switch to Replace License tab
    fireEvent.click(replaceTab);
    expect(replaceTab).toHaveAttribute("aria-selected", "true");
    expect(window.location.search).toBe("?tab=replace");
    expect(screen.getByText("Find Original License")).toBeInTheDocument();
  });
});
