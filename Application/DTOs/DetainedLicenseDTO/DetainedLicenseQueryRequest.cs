using Application.Common.Pagination;

namespace Application.DTOs.DetainedLicenseDTO;

public class DetainedLicenseQueryRequest : PaginationRequest
{
    public string? SearchQuery { get; init; }
    public string? SearchBy { get; init; }
    public string? StatusFilter { get; init; }
}
