using System.Data;

using Application.Common.Results;
using Application.DTOs.AuthDTO;
using Application.Interfaces;
using Application.Options;
using Application.Services;

using Domain.Entities;
using Domain.Enums;

using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Infrastructure.IntegrationTests.Auth;

public sealed partial class AuthSessionRefreshSqlConcurrencyTests
{
    private sealed class CoordinatingSessionRepository : IUserSessionRepository
    {
        private readonly IUserSessionRepository _inner;

        public CoordinatingSessionRepository(IUserSessionRepository inner)
        {
            _inner = inner;
        }

        public Func<Guid, int, Task<bool?>>? OnAcquireFamilyLockAsync { get; set; }
        public Func<Guid, int, Task>? BeforeAcquireFamilyLockAsync { get; set; }
        public Func<Guid, int, bool, Task>? AfterAcquireFamilyLockAsync { get; set; }

        public Func<int, int, Task<bool?>>? OnAcquireUserSessionsLockAsync { get; set; }
        public Func<int, int, Task>? BeforeAcquireUserSessionsLockAsync { get; set; }
        public Func<int, int, bool, Task>? AfterAcquireUserSessionsLockAsync { get; set; }

        public Func<Guid, DateTime, Guid, Task>? BeforeTryConsumeAsync { get; set; }
        public Func<Guid, DateTime, Guid, bool, Task>? AfterTryConsumeAsync { get; set; }
        public Func<UserSession, Task>? BeforeAddAsync { get; set; }
        public Func<Guid, DateTime, string, Task>? BeforeRevokeFamilyAsync { get; set; }
        public Func<Guid, DateTime, string, int, Task>? AfterRevokeFamilyAsync { get; set; }
        public Func<int, DateTime, string, Task>? BeforeRevokeAllForUserAsync { get; set; }
        public Func<int, DateTime, string, int, Task>? AfterRevokeAllForUserAsync { get; set; }

        public async Task<bool> AcquireFamilyLockAsync(Guid tokenFamilyId, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            if (BeforeAcquireFamilyLockAsync != null) await BeforeAcquireFamilyLockAsync(tokenFamilyId, timeoutMs);
            bool result;
            if (OnAcquireFamilyLockAsync != null)
            {
                var overridden = await OnAcquireFamilyLockAsync(tokenFamilyId, timeoutMs);
                result = overridden ?? await _inner.AcquireFamilyLockAsync(tokenFamilyId, timeoutMs, cancellationToken);
            }
            else
            {
                result = await _inner.AcquireFamilyLockAsync(tokenFamilyId, timeoutMs, cancellationToken);
            }
            if (AfterAcquireFamilyLockAsync != null) await AfterAcquireFamilyLockAsync(tokenFamilyId, timeoutMs, result);
            return result;
        }

        public async Task<bool> AcquireUserSessionsLockAsync(int userId, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            if (BeforeAcquireUserSessionsLockAsync != null) await BeforeAcquireUserSessionsLockAsync(userId, timeoutMs);
            bool result;
            if (OnAcquireUserSessionsLockAsync != null)
            {
                var overridden = await OnAcquireUserSessionsLockAsync(userId, timeoutMs);
                result = overridden ?? await _inner.AcquireUserSessionsLockAsync(userId, timeoutMs, cancellationToken);
            }
            else
            {
                result = await _inner.AcquireUserSessionsLockAsync(userId, timeoutMs, cancellationToken);
            }
            if (AfterAcquireUserSessionsLockAsync != null) await AfterAcquireUserSessionsLockAsync(userId, timeoutMs, result);
            return result;
        }

        public Task<UserSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            _inner.GetByTokenHashAsync(tokenHash, cancellationToken);

        public async Task<bool> TryConsumeAsync(Guid sessionId, DateTime nowUtc, Guid replacedBySessionId, CancellationToken cancellationToken = default)
        {
            if (BeforeTryConsumeAsync != null) await BeforeTryConsumeAsync(sessionId, nowUtc, replacedBySessionId);
            var result = await _inner.TryConsumeAsync(sessionId, nowUtc, replacedBySessionId, cancellationToken);
            if (AfterTryConsumeAsync != null) await AfterTryConsumeAsync(sessionId, nowUtc, replacedBySessionId, result);
            return result;
        }

        public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            if (BeforeAddAsync != null) await BeforeAddAsync(session);
            await _inner.AddAsync(session, cancellationToken);
        }

        public async Task<int> RevokeFamilyAsync(Guid tokenFamilyId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default)
        {
            if (BeforeRevokeFamilyAsync != null) await BeforeRevokeFamilyAsync(tokenFamilyId, nowUtc, reason);
            var affected = await _inner.RevokeFamilyAsync(tokenFamilyId, nowUtc, reason, cancellationToken);
            if (AfterRevokeFamilyAsync != null) await AfterRevokeFamilyAsync(tokenFamilyId, nowUtc, reason, affected);
            return affected;
        }

        public async Task<int> RevokeAllForUserAsync(int userId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default)
        {
            if (BeforeRevokeAllForUserAsync != null) await BeforeRevokeAllForUserAsync(userId, nowUtc, reason);
            var affected = await _inner.RevokeAllForUserAsync(userId, nowUtc, reason, cancellationToken);
            if (AfterRevokeAllForUserAsync != null) await AfterRevokeAllForUserAsync(userId, nowUtc, reason, affected);
            return affected;
        }

        public Task<bool> HasActiveSessionInFamilyAsync(Guid tokenFamilyId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
            _inner.HasActiveSessionInFamilyAsync(tokenFamilyId, nowUtc, cancellationToken);

        public Task<int> RevokeByTokenHashAsync(string tokenHash, DateTime nowUtc, string reason, CancellationToken cancellationToken = default) =>
            _inner.RevokeByTokenHashAsync(tokenHash, nowUtc, reason, cancellationToken);
    }

    private sealed class StubJwtTokenService : IJwtTokenService
    {
        public JwtTokenResult GenerateToken(JwtTokenRequest request) =>
            new()
            {
                AccessToken = "access-" + Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            };
    }

    private sealed class MutableClock : IJordanClock
    {
        public MutableClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }

        public DateTime Now =>
            Application.Common.Time.JordanTimeZoneProvider.ConvertUtcToJordan(UtcNow);

        public DateOnly Today => DateOnly.FromDateTime(Now);
    }
}
