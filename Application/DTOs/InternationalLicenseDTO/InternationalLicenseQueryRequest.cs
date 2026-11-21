using Application.Common.Pagination;

namespace Application.DTOs.InternationalLicenseDTO;

public class InternationalLicenseQueryRequest : PaginationRequest
{
    public string? SearchQuery { get; init; }
    public string? SearchBy { get; init; }
    public string? StatusFilter { get; init; }
}
