using Application.Common.Pagination;
using Application.DTOs.DetainedLicenseDTO;
using Domain.Entities;

namespace Application.Interfaces;

public interface IDetainedLicenseRepository
{
    Task<List<DetainedLicense>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<DetainedLicense>> GetAllAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DetainedLicense>> GetAllAsync(
        DetainedLicenseQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DetainedLicense?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<DetainedLicense?> GetActiveDetainByLicenseIdAsync(
        int licenseId,
        CancellationToken cancellationToken = default);

    Task<bool> IsLicenseDetainedAsync(int licenseId, CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetActiveDetainedLicenseIdsAsync(
        IEnumerable<int> licenseIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(DetainedLicense entity, CancellationToken cancellationToken = default);

    Task<DetainedLicense?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default);
}
