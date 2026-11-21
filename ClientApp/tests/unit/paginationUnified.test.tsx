import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import DetainedLicensesPage from "../../src/components/detained/DetainedLicensesPage";
import InternationalLicensesPage from "../../src/components/international-licenses/InternationalLicensesPage";
import TestAppointmentsPage from "../../src/components/test-appointments/TestAppointmentsPage";
import * as detainedService from "../../src/services/detainedLicenseService";
import * as internationalService from "../../src/services/internationalLicenseService";
import * as testAppointmentService from "../../src/services/testAppointmentService";

describe("Unified Pagination across Detained, International, and TestAppointments", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("DetainedLicensesPage requests page 2 when Next Page is clicked", async () => {
    const spy = vi
      .spyOn(detainedService, "getDetainedLicensesPage")
      .mockImplementation(async (_token, pageNumber) => ({
        items: [
          {
            detainId: pageNumber === 2 ? 201 : 101,
            licenseId: 50,
            detainDate: "2026-10-01T00:00:00Z",
            fineFees: 50,
            createdByUserId: 1,
            isReleased: false,
            releaseDate: null,
            releasedByUserId: null,
            releaseApplicationId: null,
            fullName: pageNumber === 2 ? "Page Two Driver" : "Page One Driver",
            nationalNo: "N1001",
          },
        ],
        pageNumber,
        pageSize: 10,
        totalCount: 15,
        totalPages: 2,
        hasPreviousPage: pageNumber > 1,
        hasNextPage: pageNumber < 2,
      }));

    render(
      <DetainedLicensesPage
        accessToken="tok"
        onNavigateToDetain={vi.fn()}
      />,
    );

    await screen.findByText("Page One Driver");
    const nextBtn = screen.getByRole("button", { name: /next page/i });
    fireEvent.click(nextBtn);

    await screen.findByText("Page Two Driver");
    expect(spy).toHaveBeenCalledWith("tok", 2, 10, "", "all", "all");
  });

  it("InternationalLicensesPage renders PaginationControls and navigates to page 2", async () => {
    const spy = vi
      .spyOn(internationalService, "getInternationalLicensesPage")
      .mockImplementation(async (_token, pageNumber) => ({
        items: [
          {
            internationalLicenseId: pageNumber === 2 ? 2002 : 1001,
            applicationId: 10,
            driverId: 20,
            issuedUsingLocalLicenseId: 30,
            issueDate: "2026-01-01T00:00:00Z",
            expirationDate: "2027-01-01T00:00:00Z",
            isActive: true,
            createdByUserId: 1,
          },
        ],
        pageNumber,
        pageSize: 10,
        totalCount: 12,
        totalPages: 2,
        hasPreviousPage: pageNumber > 1,
        hasNextPage: pageNumber < 2,
      }));

    render(<InternationalLicensesPage accessToken="tok" />);

    await screen.findByText("#1001");
    const nextBtn = screen.getByRole("button", { name: /next page/i });
    fireEvent.click(nextBtn);

    await screen.findByText("#2002");
    expect(spy).toHaveBeenCalledWith("tok", 2, 10, "", "all", "All");
  });

  it("TestAppointmentsPage passes active tab and navigates to page 2", async () => {
    const spy = vi
      .spyOn(testAppointmentService, "getAppointmentsPage")
      .mockImplementation(async (_token, pageNumber, _pageSize, _q, _tt, _st, tab) => ({
        items: [
          {
            testAppointmentId: pageNumber === 2 ? 902 : 901,
            testTypeId: 1,
            testTypeName: "Vision Test",
            localDrivingLicenseApplicationId: 77,
            applicantFullName: tab === "today" ? "Today Applicant" : `Applicant P${pageNumber}`,
            nationalNo: "N500",
            licenseClassName: "Class 3",
            appointmentDate: "2026-11-21T10:00:00Z",
            paidFees: 10,
            createdByUserId: 1,
            isLocked: false,
            retakeTestApplicationId: null,
            testResult: -1,
            status: "Scheduled",
          },
        ],
        pageNumber,
        pageSize: 10,
        totalCount: 14,
        totalPages: 2,
        hasPreviousPage: pageNumber > 1,
        hasNextPage: pageNumber < 2,
      }));

    render(<TestAppointmentsPage accessToken="tok" />);

    await screen.findAllByText("Applicant P1");
    fireEvent.click(screen.getByRole("button", { name: /next page/i }));

    await screen.findAllByText("Applicant P2");
    expect(spy).toHaveBeenCalledWith("tok", 2, 10, "", "all", "all", "all");

    fireEvent.click(screen.getByRole("button", { name: /today/i }));
    await waitFor(() => {
      expect(spy).toHaveBeenCalledWith("tok", 1, 10, "", "all", "all", "today");
    });
  });
});
