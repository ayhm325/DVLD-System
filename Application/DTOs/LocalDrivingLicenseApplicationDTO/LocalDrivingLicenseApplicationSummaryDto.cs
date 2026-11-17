namespace Application.DTOs.LocalDrivingLicenseApplicationDTO;

public sealed class LocalDrivingLicenseApplicationSummaryDto
{
    public int TotalApplications { get; init; }
    public int InProgressCount { get; init; }
    public int ReadyForIssuanceCount { get; init; }
}
