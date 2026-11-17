import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import DashboardPage from "../../src/components/dashboard/DashboardPage";
import * as dashboardService from "../../src/services/dashboardService";
import {
  NavigationContext,
  type NavigationContextValue,
} from "../../src/context/navigationContextInstance";

describe("DashboardPage with MagicBento Command Center", () => {
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

  const createMockNavigationContext = (
    overrides?: Partial<NavigationContextValue>,
  ): NavigationContextValue => ({
    activePage: "Dashboard",
    params: {},
    selectedPersonId: null,
    selectedDriverId: null,
    selectedDriver: null,
    personDetailsSource: "",
    licenseHistorySource: "",
    selectedAppointmentId: null,
    appointmentDetailsSource: "",
    scheduleAppointmentContext: null,
    manageTestAppointmentsContext: null,
    takeExaminationContext: null,
    reopenModalTarget: null,
    navigate: vi.fn(),
    goBack: vi.fn(),
    navigateToPersonDetails: vi.fn(),
    navigateToEditPerson: vi.fn(),
    navigateToAddPerson: vi.fn(),
    navigateToPersonLicenseHistory: vi.fn(),
    navigateToAppointmentDetails: vi.fn(),
    navigateToScheduleAppointment: vi.fn(),
    navigateToManageTestAppointments: vi.fn(),
    navigateToTakeExamination: vi.fn(),
    setSelectedPersonId: vi.fn(),
    setSelectedDriver: vi.fn(),
    clearReopenModalTarget: vi.fn(),
    ...overrides,
  });

  it("renders executive metrics, magic bento command grid, and excludes the people table", async () => {
    vi.spyOn(dashboardService, "getDashboardStatistics").mockResolvedValue(
      mockStatistics,
    );

    const mockNavigate = vi.fn();
    const mockAddPerson = vi.fn();
    const navContext = createMockNavigationContext({ navigate: mockNavigate });

    render(
      <NavigationContext.Provider value={navContext}>
        <DashboardPage
          accessToken="test-token"
          onAddPerson={mockAddPerson}
        />
      </NavigationContext.Provider>,
    );

    // 1. Verify Operational Statistics Counters render
    await waitFor(() => {
      expect(screen.getByText("1,250")).toBeInTheDocument();
      expect(screen.getByText("840")).toBeInTheDocument();
      expect(screen.getByText("620")).toBeInTheDocument();
      expect(screen.getByText("45")).toBeInTheDocument();
    });

    // 2. Verify Magic Bento Command Grid items are present
    expect(
      screen.getByText("Department Intelligence & Command Grid"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Licensing Operations & Throughput"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Testing & Qualification Pipeline"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Operational Command Hub"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Detained Licenses"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Active Permits (IDP)"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Centralized Registry Integration"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Segregation of Duties & RBAC"),
    ).toBeInTheDocument();

    // 3. Confirm that the embedded PeopleTable is removed from the Dashboard
    expect(screen.queryByPlaceholderText(/search people/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/find person/i)).not.toBeInTheDocument();

    // 4. Test quick dispatch interactions
    const bookExamBtn = screen.getByRole("button", { name: /book exam/i });
    fireEvent.click(bookExamBtn);
    expect(mockNavigate).toHaveBeenCalledWith("Test Appointments");

    const registerPersonBtn = screen.getByRole("button", {
      name: /register person/i,
    });
    fireEvent.click(registerPersonBtn);
    expect(mockAddPerson).toHaveBeenCalled();
  });
});
