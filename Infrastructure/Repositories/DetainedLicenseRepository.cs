using Application.Common.Pagination;
using Application.DTOs.DetainedLicenseDTO;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class DetainedLicenseRepository(
    DVLDDbContext context) : IDetainedLicenseRepository
{
    private readonly DVLDDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));

    private IQueryable<DetainedLicense> Query() =>
        _context.DetainedLicenses
            .Include(d => d.License)
                .ThenInclude(l => l.Driver)
                    .ThenInclude(d => d.Person)
            .Include(d => d.CreatedByUser)
            .Include(d => d.ReleasedByUser)
            .Include(d => d.ReleaseApplication);

    public Task<List<DetainedLicense>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Query()
            .AsNoTracking()
            .OrderByDescending(d => d.DetainDate)
            .ThenByDescending(d => d.DetainID)
            .ToListAsync(cancellationToken);

    public Task<PagedResult<DetainedLicense>> GetAllAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is DetainedLicenseQueryRequest queryRequest)
        {
            return GetAllAsync(queryRequest, cancellationToken);
        }

        return GetAllAsync(new DetainedLicenseQueryRequest
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
    }

    public async Task<PagedResult<DetainedLicense>> GetAllAsync(
        DetainedLicenseQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            var status = request.StatusFilter.Trim().ToLowerInvariant();
            if (status == "active")
            {
                query = query.Where(d => !d.IsReleased);
            }
            else if (status == "released")
            {
                query = query.Where(d => d.IsReleased);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var term = request.SearchQuery.Trim();
            var searchBy = request.SearchBy?.Trim().ToLowerInvariant() ?? "all";

            switch (searchBy)
            {
                case "detainid":
                case "id":
                    if (int.TryParse(term.TrimStart('#'), out var detainId))
                    {
                        query = query.Where(d => d.DetainID == detainId);
                    }
                    else
                    {
                        query = query.Where(_ => false);
                    }
                    break;

                case "licenseid":
                    if (int.TryParse(term.TrimStart('#'), out var licenseId))
                    {
                        query = query.Where(d => d.LicenseID == licenseId);
                    }
                    else
                    {
                        query = query.Where(_ => false);
                    }
                    break;

                case "nationalno":
                    query = query.Where(d => d.License.Driver.Person.NationalNo.Contains(term));
                    break;

                case "fullname":
                case "name":
                    query = query.Where(d =>
                        d.License.Driver.Person.FirstName.Contains(term) ||
                        d.License.Driver.Person.SecondName.Contains(term) ||
                        (d.License.Driver.Person.ThirdName != null && d.License.Driver.Person.ThirdName.Contains(term)) ||
                        d.License.Driver.Person.LastName.Contains(term) ||
                        (d.License.Driver.Person.FirstName + " " + d.License.Driver.Person.LastName).Contains(term) ||
                        (d.License.Driver.Person.FirstName + " " + d.License.Driver.Person.SecondName + " " + d.License.Driver.Person.LastName).Contains(term));
                    break;

                case "all":
                default:
                    var isNum = int.TryParse(term.TrimStart('#'), out var parsedNum);
                    query = query.Where(d =>
                        d.License.Driver.Person.NationalNo.Contains(term) ||
                        d.License.Driver.Person.FirstName.Contains(term) ||
                        d.License.Driver.Person.SecondName.Contains(term) ||
                        (d.License.Driver.Person.ThirdName != null && d.License.Driver.Person.ThirdName.Contains(term)) ||
                        d.License.Driver.Person.LastName.Contains(term) ||
                        (d.License.Driver.Person.FirstName + " " + d.License.Driver.Person.LastName).Contains(term) ||
                        (d.License.Driver.Person.FirstName + " " + d.License.Driver.Person.SecondName + " " + d.License.Driver.Person.LastName).Contains(term) ||
                        (isNum && (d.DetainID == parsedNum || d.LicenseID == parsedNum)));
                    break;
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.DetainDate)
            .ThenByDescending(d => d.DetainID)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DetainedLicense>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }

    public Task<DetainedLicense?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        id <= 0
            ? Task.FromResult<DetainedLicense?>(null)
            : Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DetainID == id, cancellationToken);

    public Task<DetainedLicense?> GetActiveDetainByLicenseIdAsync(
        int licenseId,
        CancellationToken cancellationToken = default) =>
        licenseId <= 0
            ? Task.FromResult<DetainedLicense?>(null)
            : Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.LicenseID == licenseId &&
                    !d.IsReleased,
                    cancellationToken);

    public Task<bool> IsLicenseDetainedAsync(int licenseId, CancellationToken cancellationToken = default) =>
        licenseId > 0
            ? _context.DetainedLicenses
                .AsNoTracking()
                .AnyAsync(d =>
                    d.LicenseID == licenseId &&
                    !d.IsReleased,
                    cancellationToken)
            : Task.FromResult(false);

    public async Task<HashSet<int>> GetActiveDetainedLicenseIdsAsync(
        IEnumerable<int> licenseIds,
        CancellationToken cancellationToken = default)
    {
        if (licenseIds is null)
            return [];

        var ids = licenseIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return [];

        var activeIds = await _context.DetainedLicenses
            .AsNoTracking()
            .Where(d => !d.IsReleased && ids.Contains(d.LicenseID))
            .Select(d => d.LicenseID)
            .ToListAsync(cancellationToken);

        return activeIds.ToHashSet();
    }

    public async Task AddAsync(DetainedLicense entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await _context.DetainedLicenses.AddAsync(entity, cancellationToken);
    }

    public Task<DetainedLicense?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Task.FromResult<DetainedLicense?>(null);

        return _context.DetainedLicenses
            .FromSqlInterpolated($"""
                SELECT *
                FROM DetainedLicenses WITH (UPDLOCK, HOLDLOCK)
                WHERE DetainID = {id}
                """)
            .Include(d => d.License)
                .ThenInclude(l => l.Driver)
                    .ThenInclude(d => d.Person)
            .Include(d => d.CreatedByUser)
            .Include(d => d.ReleasedByUser)
            .Include(d => d.ReleaseApplication)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
