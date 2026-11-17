import { lazy } from "react";
import { useNavigation } from "../../../context/useNavigation";

const LicensesPage = lazy(() => import("../../licenses/LicensesPage"));
const InternationalLicensesPage = lazy(
  () => import("../../international-licenses/InternationalLicensesPage"),
);
const IssueInternationalLicensePage = lazy(
  () => import("../../international-licenses/IssueInternationalLicensePage"),
);
const LicenseRenewalReplacementPage = lazy(
  () => import("../../licenses/LicenseRenewalReplacementPage"),
);
const LicenseDetentionPage = lazy(
  () => import("../../detained/LicenseDetentionPage"),
);

export type LicenseRoutesProps = {
  accessToken: string;
  activePage: string;
  showIssueInternational: boolean;
  setShowIssueInternational: (show: boolean) => void;
  internationalRefreshKey: number;
  onInternationalIssued: () => void;
};

export default function LicenseRoutes({
  accessToken,
  activePage,
  showIssueInternational,
  setShowIssueInternational,
  internationalRefreshKey,
  onInternationalIssued,
}: LicenseRoutesProps) {
  const { navigate } = useNavigation();

  if (activePage === "Licenses") {
    return <LicensesPage accessToken={accessToken} onNavigate={navigate} />;
  }

  if (activePage === "International Licenses") {
    return showIssueInternational ? (
      <IssueInternationalLicensePage
        accessToken={accessToken}
        onBack={() => setShowIssueInternational(false)}
        onIssued={onInternationalIssued}
      />
    ) : (
      <InternationalLicensesPage
        key={internationalRefreshKey}
        accessToken={accessToken}
        onIssueNew={() => setShowIssueInternational(true)}
      />
    );
  }

  if (
    activePage === "Renew & Replace License" ||
    activePage === "Renew License" ||
    activePage === "Replace License"
  ) {
    return (
      <LicenseRenewalReplacementPage
        accessToken={accessToken}
        initialTab={activePage === "Replace License" ? "replace" : undefined}
      />
    );
  }

  if (
    activePage === "License Detention" ||
    activePage === "Detained Licenses" ||
    activePage === "Detain License"
  ) {
    return (
      <LicenseDetentionPage
        accessToken={accessToken}
        initialTab={activePage === "Detain License" ? "detain" : undefined}
      />
    );
  }

  return null;
}
