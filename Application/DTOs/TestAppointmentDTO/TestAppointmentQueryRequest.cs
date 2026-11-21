using Application.Common.Pagination;

namespace Application.DTOs.TestAppointmentDTO;

public class TestAppointmentQueryRequest : PaginationRequest
{
    public string? SearchQuery { get; init; }
    public int? TestTypeId { get; init; }
    public string? StatusFilter { get; init; }
    public string? Tab { get; init; }
    public DateOnly? TodayDate { get; set; }
}
