import { lazy } from "react";
import { useNavigation } from "../../../context/useNavigation";

const LocalApplicationsPage = lazy(
  () => import("../../local-applications/LocalApplicationsPage"),
);
const TestAppointmentsPage = lazy(
  () => import("../../test-appointments/TestAppointmentsPage"),
);
const AppointmentDetailsPage = lazy(
  () => import("../../test-appointments/AppointmentDetailsPage"),
);
const ManageTestAppointmentsPage = lazy(
  () => import("../../test-appointments/ManageTestAppointmentsPage"),
);
const TakeExaminationPage = lazy(
  () => import("../../test-appointments/TakeExaminationPage"),
);
const ScheduleAppointmentPage = lazy(
  () => import("../../test-appointments/ScheduleAppointmentPage"),
);

export type AppointmentRoutesProps = {
  accessToken: string;
  activePage: string;
  userRole?: string | null;
};

export default function AppointmentRoutes({
  accessToken,
  activePage,
  userRole,
}: AppointmentRoutesProps) {
  const {
    navigate,
    selectedAppointmentId,
    appointmentDetailsSource,
    scheduleAppointmentContext,
    reopenModalTarget,
    clearReopenModalTarget,
    manageTestAppointmentsContext,
    takeExaminationContext,
    navigateToAppointmentDetails,
    navigateToScheduleAppointment,
    navigateToManageTestAppointments,
    navigateToTakeExamination,
    navigateToPersonLicenseHistory,
  } = useNavigation();

  if (activePage === "Local Applications") {
    return (
      <LocalApplicationsPage
        accessToken={accessToken}
        onShowLicenseHistory={navigateToPersonLicenseHistory}
        onShowAppointmentDetails={(id) =>
          navigateToAppointmentDetails(id, "Local Applications")
        }
        onScheduleAppointment={navigateToScheduleAppointment}
        onManageTestAppointments={navigateToManageTestAppointments}
        reopenModalTarget={reopenModalTarget}
        onClearReopenModalTarget={clearReopenModalTarget}
      />
    );
  }

  if (activePage === "Test Appointments") {
    return (
      <TestAppointmentsPage
        accessToken={accessToken}
        onNavigate={navigate}
        onShowAppointmentDetails={(id) =>
          navigateToAppointmentDetails(id, "Test Appointments")
        }
        onTakeExamination={navigateToTakeExamination}
      />
    );
  }

  if (activePage === "Appointment Details") {
    const detailsReturnPage =
      appointmentDetailsSource === "Manage Test Appointments" ||
      appointmentDetailsSource === "Written Test Appointments"
        ? manageTestAppointmentsContext?.localAppId &&
          manageTestAppointmentsContext.localAppId > 0
          ? appointmentDetailsSource
          : "Test Appointments"
        : appointmentDetailsSource || "Test Appointments";

    return (
      <AppointmentDetailsPage
        accessToken={accessToken}
        appointmentId={selectedAppointmentId ?? null}
        onBack={() => {
          if (
            detailsReturnPage === "Manage Test Appointments" ||
            detailsReturnPage === "Written Test Appointments"
          ) {
            navigate(detailsReturnPage, {
              manageTestAppointmentsContext,
            });
            return;
          }
          navigate(detailsReturnPage);
        }}
        onNavigate={navigate}
        onTakeExamination={navigateToTakeExamination}
        userRole={userRole}
      />
    );
  }

  if (
    activePage === "Manage Test Appointments" ||
    activePage === "Written Test Appointments"
  ) {
    return (
      <ManageTestAppointmentsPage
        accessToken={accessToken}
        localAppId={manageTestAppointmentsContext?.localAppId ?? null}
        onBack={() =>
          navigate(
            manageTestAppointmentsContext?.returnPage || "Local Applications",
          )
        }
        onNavigate={navigate}
        onScheduleAppointment={navigateToScheduleAppointment}
        onShowAppointmentDetails={navigateToAppointmentDetails}
        onTakeExamination={navigateToTakeExamination}
      />
    );
  }

  if (activePage === "Take Examination") {
    const requestedReturnPage =
      takeExaminationContext?.returnPage || "Test Appointments";

    const safeReturnPage =
      requestedReturnPage === "Manage Test Appointments" ||
      requestedReturnPage === "Written Test Appointments"
        ? manageTestAppointmentsContext?.localAppId &&
          manageTestAppointmentsContext.localAppId > 0
          ? requestedReturnPage
          : "Test Appointments"
        : requestedReturnPage === "Appointment Details"
          ? selectedAppointmentId && selectedAppointmentId > 0
            ? "Appointment Details"
            : "Test Appointments"
          : requestedReturnPage;

    return (
      <TakeExaminationPage
        accessToken={accessToken}
        appointmentId={takeExaminationContext?.appointmentId ?? null}
        onBack={() => {
          if (
            safeReturnPage === "Manage Test Appointments" ||
            safeReturnPage === "Written Test Appointments"
          ) {
            navigate(safeReturnPage, {
              manageTestAppointmentsContext,
            });
            return;
          }
          if (safeReturnPage === "Appointment Details") {
            navigate("Appointment Details", {
              selectedAppointmentId,
              appointmentDetailsSource,
              manageTestAppointmentsContext,
            });
            return;
          }
          navigate(safeReturnPage);
        }}
        returnPage={safeReturnPage}
      />
    );
  }

  if (activePage === "Schedule Appointment") {
    const scheduleReturnPage =
      scheduleAppointmentContext?.returnPage === "Manage Test Appointments" ||
      scheduleAppointmentContext?.returnPage === "Written Test Appointments"
        ? manageTestAppointmentsContext?.localAppId &&
          manageTestAppointmentsContext.localAppId > 0
          ? scheduleAppointmentContext.returnPage
          : "Local Applications"
        : scheduleAppointmentContext?.returnPage || "Local Applications";

    return (
      <ScheduleAppointmentPage
        accessToken={accessToken}
        initialLocalAppId={scheduleAppointmentContext?.localAppId ?? null}
        initialTestTypeId={scheduleAppointmentContext?.testTypeId ?? 1}
        returnPage={scheduleReturnPage}
        onBack={() => {
          if (
            scheduleReturnPage === "Manage Test Appointments" ||
            scheduleReturnPage === "Written Test Appointments"
          ) {
            navigate(scheduleReturnPage, {
              manageTestAppointmentsContext,
            });
            return;
          }
          navigate(scheduleReturnPage);
        }}
        onNavigate={navigate}
        onShowAppointmentDetails={navigateToAppointmentDetails}
      />
    );
  }

  return null;
}
