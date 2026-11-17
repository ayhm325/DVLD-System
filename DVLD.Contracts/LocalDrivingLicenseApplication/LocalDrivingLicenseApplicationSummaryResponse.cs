namespace DVLD.Contracts.LocalDrivingLicenseApplication;

public sealed record LocalDrivingLicenseApplicationSummaryResponse(
    int TotalApplications,
    int InProgressCount,
    int ReadyForIssuanceCount);
