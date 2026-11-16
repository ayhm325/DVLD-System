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

/// <summary>
/// AUTH-01-POST: real SQL Server concurrency for refresh rotation. Each attempt uses
/// its own DbContext, repositories, and UnitOfWork, so only the database atomic
/// conditional UPDATE can decide the winner.
/// </summary>
public sealed class AuthSessionRefreshSqlConcurrencyTests
    : IClassFixture<SqlServerTestDatabase>
{
    private static readonly IRefreshTokenService TokenService = new RefreshTokenService();

    private readonly SqlServerTestDatabase _database;

    public AuthSessionRefreshSqlConcurrencyTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task ConcurrentRefresh_OnSqlServer_ExactlyOneRotationWins()
    {
        var userId = await SeedUserAsync("auth.sql.concurrent");
        var rawToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, rawToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Result<LoginResponseDto>> AttemptAsync()
        {
            await using var context = _database.CreateContext();
            var service = BuildAuthService(context, new MutableClock(DateTime.UtcNow));

            await gate.Task;

            return await service.RefreshAsync(rawToken);
        }

        var first = AttemptAsync();
        var second = AttemptAsync();
        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(1, results.Count(result => result.ErrorType == ErrorType.Conflict));

        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        // Exactly the consumed generation and one replacement: no second chain.
        Assert.Equal(2, family.Count);
        Assert.Single(family.Where(item => item.RevokedAtUtc is null));

        var consumed = family.Single(item => item.Id == session.Id);
        Assert.NotNull(consumed.RevokedAtUtc);
        Assert.Equal(SessionRevocationReasons.Rotated, consumed.RevocationReason);
        Assert.NotNull(consumed.ReplacedBySessionId);

        var replacement = family.Single(item => item.Id != session.Id);
        Assert.Equal(consumed.ReplacedBySessionId, replacement.Id);
        Assert.Equal(session.ExpiresAtUtc, replacement.ExpiresAtUtc);
    }

    [Fact]
    public async Task ReuseAfterGrace_OnSqlServer_RevokesTheWholeFamily()
    {
        var userId = await SeedUserAsync("auth.sql.reuse");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        LoginResponseDto rotated;
        await using (var rotateContext = _database.CreateContext())
        {
            var service = BuildAuthService(rotateContext, new MutableClock(now));
            var rotation = await service.RefreshAsync(initialToken);

            Assert.True(rotation.IsSuccess);
            rotated = rotation.Value!;
        }

        // Beyond the 30-second grace window: replaying the consumed token is theft.
        await using (var replayContext = _database.CreateContext())
        {
            var service = BuildAuthService(
                replayContext,
                new MutableClock(now.AddSeconds(61)));

            var replay = await service.RefreshAsync(initialToken);

            Assert.Equal(ErrorType.Unauthorized, replay.ErrorType);
        }

        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        Assert.Equal(2, family.Count);
        Assert.All(family, item => Assert.NotNull(item.RevokedAtUtc));

        // The generation that was still active is revoked for reuse; the already
        // consumed generation keeps its original "Rotated" audit reason.
        Assert.Contains(
            family,
            item => item.RevocationReason == SessionRevocationReasons.ReuseDetected);

        // Legitimate replacement token is dead as well: re-authentication is required.
        await using (var afterContext = _database.CreateContext())
        {
            var service = BuildAuthService(
                afterContext,
                new MutableClock(now.AddSeconds(62)));

            var after = await service.RefreshAsync(rotated.RefreshToken);

            Assert.Equal(ErrorType.Unauthorized, after.ErrorType);
        }
    }

    [Fact]
    public async Task Boundary1_RefreshConsumesOldSessionBeforeLogoutExecutesFamilyRevocation_NoActiveSessionRemains()
    {
        var userId = await SeedUserAsync("auth.sql.boundary1");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var refreshCommittedGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 1. Refresh coordinates: completes consume, insert, and commits transaction, then signals gate.
        // This avoids holding an exclusive row lock while awaiting logout (preventing circular deadlock).
        async Task<Result<LoginResponseDto>> RefreshOperation()
        {
            await using var refreshContext = _database.CreateContext();
            var service = BuildAuthService(refreshContext, new MutableClock(now));
            var result = await service.RefreshAsync(initialToken);
            Assert.True(result.IsSuccess);

            // Signal that refresh committed its replacement session
            refreshCommittedGate.SetResult();
            return result;
        }

        // 2. Logout coordinates: waits until Refresh has committed the rotation
        async Task<Result> LogoutOperation()
        {
            await refreshCommittedGate.Task;

            await using var logoutContext = _database.CreateContext();
            var service = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)));
            return await service.LogoutAsync(initialToken);
        }

        var refreshTask = RefreshOperation();
        var logoutTask = LogoutOperation();

        var refreshResult = await refreshTask;
        var logoutResult = await logoutTask;

        Assert.True(refreshResult.IsSuccess);
        Assert.True(logoutResult.IsSuccess);

        // 3. Verify on SQL Server: No active session remains in that family
        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        Assert.Equal(2, family.Count);
        Assert.All(family, item => Assert.NotNull(item.RevokedAtUtc));
        Assert.DoesNotContain(family, item => item.RevokedAtUtc == null);

        // The replacement generation must be revoked with reason Logout
        var replacement = family.Single(item => item.Id != session.Id);
        Assert.NotNull(replacement.RevokedAtUtc);
        Assert.Equal(SessionRevocationReasons.Logout, replacement.RevocationReason);

        var repo = new UserSessionRepository(verify);
        Assert.False(await repo.HasActiveSessionInFamilyAsync(familyId, now.AddSeconds(5)));
    }

    [Fact]
    public async Task Boundary2_LogoutRevokesFamilyBeforeRefreshAttemptsToConsumeOldSession_RefreshFailsAndNeverCommitsReplacement()
    {
        var userId = await SeedUserAsync("auth.sql.boundary2");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        // 1. Logout completes successfully and commits family revocation
        await using (var logoutContext = _database.CreateContext())
        {
            var service = BuildAuthService(logoutContext, new MutableClock(now));
            var logoutResult = await service.LogoutAsync(initialToken);
            Assert.True(logoutResult.IsSuccess);
        }

        // 2. Refresh is then attempted using the original refresh token
        Result<LoginResponseDto> refreshResult;
        await using (var refreshContext = _database.CreateContext())
        {
            var service = BuildAuthService(refreshContext, new MutableClock(now.AddSeconds(1)));
            refreshResult = await service.RefreshAsync(initialToken);
        }

        // Refresh must fail because the original session has already been revoked
        Assert.False(refreshResult.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, refreshResult.ErrorType);

        // 3. Verify on SQL Server: Exactly 1 session exists, revoked by Logout; ZERO replacement sessions committed
        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        Assert.Single(family);
        Assert.NotNull(family[0].RevokedAtUtc);
        Assert.Equal(SessionRevocationReasons.Logout, family[0].RevocationReason);

        // Verify that no active session remains in the token family
        var repo = new UserSessionRepository(verify);
        Assert.False(await repo.HasActiveSessionInFamilyAsync(familyId, now.AddSeconds(5)));
    }

    [Fact]
    public async Task Boundary3_RefreshHoldsLock_LogoutWaitsOnAppLockUntilRefreshCommits_NoActiveReplacementSessionRemains()
    {
        var userId = await SeedUserAsync("auth.sql.boundary3");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var refreshLockAcquiredGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutStartedGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 1. Refresh acquires lock and pauses after acquire to allow Logout to enter sp_getapplock
        async Task<Result<LoginResponseDto>> RefreshOperation()
        {
            await using var refreshContext = _database.CreateContext();
            var realRepo = new UserSessionRepository(refreshContext);
            var coordinatingRepo = new CoordinatingSessionRepository(realRepo)
            {
                AfterAcquireFamilyLockAsync = async (famId, timeout, acquired) =>
                {
                    // Refresh has acquired the exclusive applock in SQL Server
                    refreshLockAcquiredGate.SetResult();
                    // Wait until Logout has begun its request
                    await logoutStartedGate.Task;
                    // Yield briefly to let Logout enter sp_getapplock and block
                    await Task.Delay(50);
                }
            };

            var service = BuildAuthService(refreshContext, new MutableClock(now), coordinatingRepo);
            return await service.RefreshAsync(initialToken);
        }

        // 2. Logout starts after Refresh has acquired lock; sp_getapplock blocks Logout in SQL Server until Refresh commits
        async Task<Result> LogoutOperation()
        {
            await refreshLockAcquiredGate.Task;
            logoutStartedGate.SetResult();

            await using var logoutContext = _database.CreateContext();
            var service = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)));
            return await service.LogoutAsync(initialToken);
        }

        var refreshTask = RefreshOperation();
        var logoutTask = LogoutOperation();

        var refreshResult = await refreshTask;
        var logoutResult = await logoutTask;

        Assert.True(refreshResult.IsSuccess);
        Assert.True(logoutResult.IsSuccess);

        // 3. Verify on SQL Server: Refresh created a replacement session, but Logout acquired lock after Refresh commit and revoked it!
        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        Assert.Equal(2, family.Count);
        Assert.All(family, item => Assert.NotNull(item.RevokedAtUtc));
        Assert.DoesNotContain(family, item => item.RevokedAtUtc == null);

        var replacement = family.Single(item => item.Id != session.Id);
        Assert.Equal(SessionRevocationReasons.Logout, replacement.RevocationReason);

        var repo = new UserSessionRepository(verify);
        Assert.False(await repo.HasActiveSessionInFamilyAsync(familyId, now.AddSeconds(5)));
    }

    [Fact]
    public async Task ScenarioC_WhenFamilyLockFails_RefreshReturnsConflictAndLogoutReturnsFailure()
    {
        var userId = await SeedUserAsync("auth.sql.scenarioC");
        var token1 = TokenService.CreateToken();
        var family1 = Guid.NewGuid();
        var session1 = NewSession(userId, token1, family1);

        var token2 = TokenService.CreateToken();
        var family2 = Guid.NewGuid();
        var session2 = NewSession(userId, token2, family2);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.AddRange(session1, session2);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        // 1. Refresh when lock fails
        await using (var refreshContext = _database.CreateContext())
        {
            var realRepo = new UserSessionRepository(refreshContext);
            var coordinatingRepo = new CoordinatingSessionRepository(realRepo)
            {
                OnAcquireFamilyLockAsync = (famId, timeout) => Task.FromResult<bool?>(false)
            };

            var service = BuildAuthService(refreshContext, new MutableClock(now), coordinatingRepo);
            var refreshResult = await service.RefreshAsync(token1);

            Assert.False(refreshResult.IsSuccess);
            Assert.Equal(ErrorType.Conflict, refreshResult.ErrorType);
        }

        // 2. Logout when lock fails
        await using (var logoutContext = _database.CreateContext())
        {
            var realRepo = new UserSessionRepository(logoutContext);
            var coordinatingRepo = new CoordinatingSessionRepository(realRepo)
            {
                OnAcquireFamilyLockAsync = (famId, timeout) => Task.FromResult<bool?>(false)
            };

            var service = BuildAuthService(logoutContext, new MutableClock(now), coordinatingRepo);
            var logoutResult = await service.LogoutAsync(token2);

            Assert.False(logoutResult.IsSuccess);
        }

        // Verify database state: neither session was altered
        await using var verify = _database.CreateContext();
        var s1 = await verify.UserSessions.AsNoTracking().SingleAsync(s => s.Id == session1.Id);
        var s2 = await verify.UserSessions.AsNoTracking().SingleAsync(s => s.Id == session2.Id);

        Assert.Null(s1.RevokedAtUtc);
        Assert.Null(s2.RevokedAtUtc);
    }

    [Fact]
    public async Task ScenarioD_IndependentTokenFamilies_ExecuteConcurrentlyWithoutBlocking()
    {
        var userId = await SeedUserAsync("auth.sql.scenarioD");
        var token1 = TokenService.CreateToken();
        var family1 = Guid.NewGuid();
        var session1 = NewSession(userId, token1, family1);

        var token2 = TokenService.CreateToken();
        var family2 = Guid.NewGuid();
        var session2 = NewSession(userId, token2, family2);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.AddRange(session1, session2);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        // Both families refresh concurrently
        async Task<Result<LoginResponseDto>> RefreshFamily1()
        {
            await using var ctx = _database.CreateContext();
            var service = BuildAuthService(ctx, new MutableClock(now));
            return await service.RefreshAsync(token1);
        }

        async Task<Result<LoginResponseDto>> RefreshFamily2()
        {
            await using var ctx = _database.CreateContext();
            var service = BuildAuthService(ctx, new MutableClock(now));
            return await service.RefreshAsync(token2);
        }

        var task1 = RefreshFamily1();
        var task2 = RefreshFamily2();

        var results = await Task.WhenAll(task1, task2);

        Assert.All(results, r => Assert.True(r.IsSuccess));

        // Verify both families rotated independently
        await using var verify = _database.CreateContext();
        var sessions1 = await verify.UserSessions.AsNoTracking().Where(s => s.TokenFamilyId == family1).ToListAsync();
        var sessions2 = await verify.UserSessions.AsNoTracking().Where(s => s.TokenFamilyId == family2).ToListAsync();

        Assert.Equal(2, sessions1.Count);
        Assert.Equal(2, sessions2.Count);
        Assert.Single(sessions1.Where(s => s.RevokedAtUtc == null));
        Assert.Single(sessions2.Where(s => s.RevokedAtUtc == null));
    }

    [Fact]
    public async Task Boundary4_RefreshAfterLogout_WhenTokenRevokedByLogout_ServerRejectsRefresh()
    {
        var userId = await SeedUserAsync("auth.sql.boundary4");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        // 1. Logout
        await using (var logoutContext = _database.CreateContext())
        {
            var service = BuildAuthService(logoutContext, new MutableClock(now));
            var logoutResult = await service.LogoutAsync(initialToken);
            Assert.True(logoutResult.IsSuccess);
        }

        // 2. Attempt refresh with logged out token
        await using (var refreshContext = _database.CreateContext())
        {
            var service = BuildAuthService(refreshContext, new MutableClock(now.AddSeconds(1)));
            var refreshResult = await service.RefreshAsync(initialToken);

            Assert.False(refreshResult.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, refreshResult.ErrorType);
        }

        // 3. Confirm only the single revoked session exists in family
        await using var verify = _database.CreateContext();
        var family = await verify.UserSessions
            .AsNoTracking()
            .Where(item => item.TokenFamilyId == familyId)
            .ToListAsync();

        Assert.Single(family);
        Assert.NotNull(family[0].RevokedAtUtc);
        Assert.Equal(SessionRevocationReasons.Logout, family[0].RevocationReason);
    }

    [Fact]
    public async Task Boundary5_LogoutAllRacingWithRefresh_WhenLogoutAllExecutes_EnsuresNoActiveReplacementSessionRemains()
    {
        // Interleaving scenario:
        // 1. Initial state: user has an active session.
        // 2. Refresh begins, acquires DVLD_UserSessions_{userId} and DVLD_TokenFamily_{familyId}.
        // 3. Refresh calls AddAsync for replacement session, holds locks, and pauses.
        // 4. LogoutAll begins, attempts to acquire DVLD_UserSessions_{userId}, and blocks in SQL Server engine.
        // 5. Test coordinator polls SQL Server DMVs (sys.dm_tran_locks, sys.dm_exec_requests) to confirm
        //    LogoutAll's SPID is actively waiting on the APPLICATION lock held by Refresh's SPID.
        // 6. Test coordinator releases Refresh to commit.
        // 7. Refresh commits, releasing locks.
        // 8. LogoutAll unblocks, acquires DVLD_UserSessions_{userId}, revokes all sessions, and commits.
        // Invariant: Exactly zero active replacement sessions remain for this user.
        var userId = await SeedUserAsync("auth.sql.boundary5");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var refreshHoldingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshToCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutAttemptingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var now = DateTime.UtcNow;

        var testCts = new CancellationTokenSource();

        var refreshContext = _database.CreateContext();
        var logoutContext = _database.CreateContext();

        await refreshContext.Database.OpenConnectionAsync();
        int refreshSpid;
        await using (var cmd = refreshContext.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "SELECT @@SPID";
            refreshSpid = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await logoutContext.Database.OpenConnectionAsync();
        int logoutSpid;
        await using (var cmd = logoutContext.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "SELECT @@SPID";
            logoutSpid = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        var refreshRepo = new CoordinatingSessionRepository(new UserSessionRepository(refreshContext));
        var logoutRepo = new CoordinatingSessionRepository(new UserSessionRepository(logoutContext));

        // When refresh adds the replacement session, it holds both user and family application locks.
        // Signal that locks are held, and wait until the test coordinator confirms LogoutAll is blocked in SQL Server.
        refreshRepo.BeforeAddAsync = async _ =>
        {
            refreshHoldingLock.TrySetResult(true);
            await allowRefreshToCommit.Task.WaitAsync(testCts.Token);
        };

        logoutRepo.BeforeAcquireUserSessionsLockAsync = (uid, timeout) =>
        {
            logoutAttemptingLock.TrySetResult(true);
            return Task.CompletedTask;
        };

        var refreshService = BuildAuthService(refreshContext, new MutableClock(now), refreshRepo);
        var logoutService = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)), logoutRepo);

        Task<Result<LoginResponseDto>>? refreshTask = null;
        Task<Result>? logoutAllTask = null;
        Exception? testException = null;

        try
        {
            refreshTask = refreshService.RefreshAsync(initialToken, testCts.Token);

            // Wait until Refresh is holding the application locks in SQL Server
            await refreshHoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Start LogoutAll concurrently
            logoutAllTask = Task.Run(() => logoutService.LogoutAllAsync(userId, testCts.Token), testCts.Token);

            // Wait until LogoutAll has reached sp_getapplock
            await logoutAttemptingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Verify deterministically via SQL Server DMVs that logoutSpid is actively WAITING on the APPLICATION lock
            // held specifically by refreshSpid for the exact user resource DVLD_UserSessions_{userId}
            await using (var monitorContext = _database.CreateContext())
            {
                await WaitForApplicationLockWaitAsync(
                    monitorContext,
                    waitingSpid: logoutSpid,
                    blockingSpid: refreshSpid,
                    expectedResourceName: $"DVLD_UserSessions_{userId}",
                    timeout: TimeSpan.FromSeconds(5));
            }

            // Release Refresh to commit its transaction and release locks
            allowRefreshToCommit.TrySetResult(true);

            await Task.WhenAll(refreshTask, logoutAllTask);

            var refreshResult = await refreshTask;
            var logoutAllResult = await logoutAllTask;

            Assert.True(refreshResult.IsSuccess, "Refresh should complete rotation successfully before logoutAll takes lock.");
            Assert.True(logoutAllResult.IsSuccess, "LogoutAll should complete revocation after unblocking.");

            // Invariant: Final persisted database verification
            await using var verify = _database.CreateContext();
            var userSessions = await verify.UserSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .ToListAsync();

            Assert.NotEmpty(userSessions);
            // Every single session belonging to this user MUST be revoked
            Assert.All(userSessions, s => Assert.NotNull(s.RevokedAtUtc));

            // The replacement session must have been revoked by LogoutAll
            var replacementSession = Assert.Single(userSessions, s => s.Id != session.Id);
            Assert.Equal(SessionRevocationReasons.LogoutAll, replacementSession.RevocationReason);

            // The initial session was consumed by refresh
            var initialSession = Assert.Single(userSessions, s => s.Id == session.Id);
            Assert.Equal(SessionRevocationReasons.Rotated, initialSession.RevocationReason);

            // Exactly zero unrevoked active sessions remain
            var activeCount = userSessions.Count(s => s.RevokedAtUtc == null);
            Assert.Equal(0, activeCount);
        }
        catch (Exception ex)
        {
            testException = ex;
            throw;
        }
        finally
        {
            // Stage A: Release all coordination gates unconditionally with true so background tasks never hang
            allowRefreshToCommit.TrySetResult(true);
            refreshHoldingLock.TrySetResult(true);
            logoutAttemptingLock.TrySetResult(true);

            // Stage B: Collect and track each initiated task with its descriptive name
            var tasksToAwait = new List<(string Name, Task Task)>();
            if (refreshTask is not null) tasksToAwait.Add(("RefreshTask", refreshTask));
            if (logoutAllTask is not null) tasksToAwait.Add(("LogoutAllTask", logoutAllTask));

            var cleanupErrors = new List<Exception>();

            if (tasksToAwait.Count > 0)
            {
                var combinedTasks = Task.WhenAll(tasksToAwait.Select(t => t.Task));

                try
                {
                    // Stage C: Bounded wait for natural completion
                    await combinedTasks.WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException)
                {
                    // Stage D: Timeout during natural wait -> request cooperative cancellation
                    testCts.Cancel();

                    allowRefreshToCommit.TrySetResult(true);
                    refreshHoldingLock.TrySetResult(true);
                    logoutAttemptingLock.TrySetResult(true);

                    try
                    {
                        await combinedTasks.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        // Stage E: Tasks still failed to terminate even after cancellation
                        var unresponsive = tasksToAwait.Where(t => !t.Task.IsCompleted).Select(t => t.Name).ToList();
                        cleanupErrors.Add(new TimeoutException(
                            $"Boundary5 cleanup critical failure: The following background tasks failed to terminate even after cancellation request: {string.Join(", ", unresponsive)}. Resource disposal was aborted to avoid corrupting active connections."));
                    }
                    catch
                    {
                        // Task-level exceptions are observed and inspected individually below
                    }
                }
                catch
                {
                    // Task-level exceptions are observed and inspected individually below
                }

                // Stage E2: Inspect each task individually to avoid swallowing ANY unexpected exception
                foreach (var (name, task) in tasksToAwait)
                {
                    if (task.IsFaulted && task.Exception is not null)
                    {
                        foreach (var inner in task.Exception.Flatten().InnerExceptions)
                        {
                            // If cancellation was requested, OperationCanceledException/TaskCanceledException is expected
                            if (testCts.IsCancellationRequested && (inner is OperationCanceledException or TaskCanceledException))
                            {
                                continue;
                            }

                            // Unexpected task fault (SqlException, InvalidOperationException, etc.)
                            cleanupErrors.Add(new InvalidOperationException($"Background task '{name}' faulted: {inner.Message}", inner));
                        }
                    }
                    else if (task.IsCanceled && !testCts.IsCancellationRequested)
                    {
                        // Task canceled before cleanup cancellation was requested -> unexpected!
                        cleanupErrors.Add(new InvalidOperationException($"Background task '{name}' was canceled unexpectedly before cleanup was requested."));
                    }
                }
            }

            var anyTaskStillRunning = tasksToAwait.Any(t => !t.Task.IsCompleted);

            // Stage F: Safely dispose DbContexts and CancellationTokenSource ONLY if all tasks have terminated
            if (!anyTaskStillRunning)
            {
                if (logoutContext is not null)
                {
                    try { await logoutContext.DisposeAsync(); }
                    catch (Exception ex) { cleanupErrors.Add(new InvalidOperationException($"Failed to dispose logoutContext: {ex.Message}", ex)); }
                }

                if (refreshContext is not null)
                {
                    try { await refreshContext.DisposeAsync(); }
                    catch (Exception ex) { cleanupErrors.Add(new InvalidOperationException($"Failed to dispose refreshContext: {ex.Message}", ex)); }
                }

                try { testCts.Dispose(); }
                catch (Exception ex) { cleanupErrors.Add(new InvalidOperationException($"Failed to dispose testCts: {ex.Message}", ex)); }
            }

            // Stage G: Aggregate exceptions preserving the primary test failure
            Exception? combinedCleanupException = cleanupErrors.Count switch
            {
                0 => null,
                1 => cleanupErrors[0],
                _ => new AggregateException("Multiple cleanup errors occurred in Boundary5.", cleanupErrors)
            };

            if (testException is not null)
            {
                if (combinedCleanupException is not null)
                {
                    throw new AggregateException(
                        "Boundary5 encountered a primary test failure and subsequent cleanup error(s).",
                        testException,
                        combinedCleanupException);
                }
            }
            else if (combinedCleanupException is not null)
            {
                throw combinedCleanupException;
            }
        }
    }

    [Fact]
    public async Task Boundary5_WhenFailureAfterRefreshStarts_GateUnblockedAndContextDisposedAfterTaskEnds()
    {
        var userId = await SeedUserAsync("auth.sql.boundary5_fail_refresh");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var refreshHoldingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshToCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var now = DateTime.UtcNow;
        using var testCts = new CancellationTokenSource();

        var refreshContext = _database.CreateContext();
        var refreshRepo = new CoordinatingSessionRepository(new UserSessionRepository(refreshContext));

        refreshRepo.BeforeAddAsync = async _ =>
        {
            refreshHoldingLock.TrySetResult(true);
            await allowRefreshToCommit.Task.WaitAsync(testCts.Token);
        };

        var refreshService = BuildAuthService(refreshContext, new MutableClock(now), refreshRepo);
        Task<Result<LoginResponseDto>>? refreshTask = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            try
            {
                refreshTask = refreshService.RefreshAsync(initialToken, testCts.Token);
                await refreshHoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // Simulate test assertion failure while Refresh holds lock
                throw new InvalidOperationException("Simulated test failure after Refresh started");
            }
            finally
            {
                // Gate is unblocked in cleanup
                allowRefreshToCommit.TrySetResult(true);

                if (refreshTask is not null)
                {
                    await refreshTask.WaitAsync(TimeSpan.FromSeconds(10));
                }

                // DbContext is safely disposed ONLY after task completes
                await refreshContext.DisposeAsync();
            }
        });

        Assert.Equal("Simulated test failure after Refresh started", ex.Message);
        Assert.NotNull(refreshTask);
        Assert.True(refreshTask.IsCompleted);
    }

    [Fact]
    public async Task Boundary5_WhenFailureAfterLogoutAllStarts_AllGatesUnblockedAndBothTasksTerminateCleanly()
    {
        var userId = await SeedUserAsync("auth.sql.boundary5_fail_logout");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var refreshHoldingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshToCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutAttemptingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var now = DateTime.UtcNow;
        using var testCts = new CancellationTokenSource();

        var refreshContext = _database.CreateContext();
        var logoutContext = _database.CreateContext();

        var refreshRepo = new CoordinatingSessionRepository(new UserSessionRepository(refreshContext));
        var logoutRepo = new CoordinatingSessionRepository(new UserSessionRepository(logoutContext));

        refreshRepo.BeforeAddAsync = async _ =>
        {
            refreshHoldingLock.TrySetResult(true);
            await allowRefreshToCommit.Task.WaitAsync(testCts.Token);
        };

        logoutRepo.BeforeAcquireUserSessionsLockAsync = (uid, timeout) =>
        {
            logoutAttemptingLock.TrySetResult(true);
            return Task.CompletedTask;
        };

        var refreshService = BuildAuthService(refreshContext, new MutableClock(now), refreshRepo);
        var logoutService = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)), logoutRepo);

        Task<Result<LoginResponseDto>>? refreshTask = null;
        Task<Result>? logoutAllTask = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            try
            {
                refreshTask = refreshService.RefreshAsync(initialToken, testCts.Token);
                await refreshHoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

                logoutAllTask = Task.Run(() => logoutService.LogoutAllAsync(userId, testCts.Token), testCts.Token);
                await logoutAttemptingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // Simulate test assertion failure while LogoutAll is blocked on lock
                throw new InvalidOperationException("Simulated test failure after LogoutAll started");
            }
            finally
            {
                // Unblock gates
                allowRefreshToCommit.TrySetResult(true);
                refreshHoldingLock.TrySetResult(true);
                logoutAttemptingLock.TrySetResult(true);

                var tasks = new List<Task>();
                if (refreshTask is not null) tasks.Add(refreshTask);
                if (logoutAllTask is not null) tasks.Add(logoutAllTask);

                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

                await logoutContext.DisposeAsync();
                await refreshContext.DisposeAsync();
            }
        });

        Assert.Equal("Simulated test failure after LogoutAll started", ex.Message);
        Assert.NotNull(refreshTask);
        Assert.True(refreshTask.IsCompleted);
        Assert.NotNull(logoutAllTask);
        Assert.True(logoutAllTask.IsCompleted);
    }

    [Fact]
    public async Task Boundary5_WhenCancellationRequestedDuringLockWait_TaskAbortsAndResourcesCleanedUp()
    {
        var userId = await SeedUserAsync("auth.sql.boundary5_cancel");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var refreshHoldingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshToCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutAttemptingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var now = DateTime.UtcNow;
        using var testCts = new CancellationTokenSource();

        var refreshContext = _database.CreateContext();
        var logoutContext = _database.CreateContext();

        var refreshRepo = new CoordinatingSessionRepository(new UserSessionRepository(refreshContext));
        var logoutRepo = new CoordinatingSessionRepository(new UserSessionRepository(logoutContext));

        refreshRepo.BeforeAddAsync = async _ =>
        {
            refreshHoldingLock.TrySetResult(true);
            await allowRefreshToCommit.Task.WaitAsync(testCts.Token);
        };

        logoutRepo.BeforeAcquireUserSessionsLockAsync = (uid, timeout) =>
        {
            logoutAttemptingLock.TrySetResult(true);
            return Task.CompletedTask;
        };

        var refreshService = BuildAuthService(refreshContext, new MutableClock(now), refreshRepo);
        var logoutService = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)), logoutRepo);

        Task<Result<LoginResponseDto>>? refreshTask = null;
        Task<Result>? logoutAllTask = null;

        try
        {
            refreshTask = refreshService.RefreshAsync(initialToken, testCts.Token);
            await refreshHoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

            logoutAllTask = Task.Run(() => logoutService.LogoutAllAsync(userId, testCts.Token), testCts.Token);
            await logoutAttemptingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Give LogoutAll time to enter sp_getapplock
            await Task.Delay(200);

            // Cancel the cancellation token while LogoutAll is waiting on the lock
            testCts.Cancel();

            // Observe that the cancellation is handled
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await Task.WhenAll(refreshTask, logoutAllTask);
            });
        }
        finally
        {
            allowRefreshToCommit.TrySetResult(true);
            refreshHoldingLock.TrySetResult(true);
            logoutAttemptingLock.TrySetResult(true);

            var tasks = new List<Task>();
            if (refreshTask is not null) tasks.Add(refreshTask);
            if (logoutAllTask is not null) tasks.Add(logoutAllTask);

            // Both tasks must terminate after cancellation
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => { });

            Assert.True(refreshTask?.IsCompleted ?? false);
            Assert.True(logoutAllTask?.IsCompleted ?? false);

            await logoutContext.DisposeAsync();
            await refreshContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task Boundary5_WhenUnexpectedExceptionOccursInBackgroundTask_ExceptionIsNotSwallowedAndTaskIsIdentified()
    {
        var userId = await SeedUserAsync("auth.sql.boundary5_task_fault");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var refreshHoldingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshToCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutAttemptingLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var now = DateTime.UtcNow;
        var testCts = new CancellationTokenSource();

        var refreshContext = _database.CreateContext();
        var logoutContext = _database.CreateContext();

        var refreshRepo = new CoordinatingSessionRepository(new UserSessionRepository(refreshContext));
        var logoutRepo = new CoordinatingSessionRepository(new UserSessionRepository(logoutContext));

        refreshRepo.BeforeAddAsync = async _ =>
        {
            refreshHoldingLock.TrySetResult(true);
            await allowRefreshToCommit.Task.WaitAsync(testCts.Token);
        };

        // Injected unexpected task failure in LogoutAll before acquiring lock
        logoutRepo.BeforeAcquireUserSessionsLockAsync = (uid, timeout) =>
        {
            logoutAttemptingLock.TrySetResult(true);
            throw new InvalidOperationException("Injected unexpected failure in LogoutAllTask");
        };

        var refreshService = BuildAuthService(refreshContext, new MutableClock(now), refreshRepo);
        var logoutService = BuildAuthService(logoutContext, new MutableClock(now.AddSeconds(1)), logoutRepo);

        Task<Result<LoginResponseDto>>? refreshTask = null;
        Task<Result>? logoutAllTask = null;

        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            try
            {
                refreshTask = refreshService.RefreshAsync(initialToken, testCts.Token);
                await refreshHoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

                logoutAllTask = Task.Run(() => logoutService.LogoutAllAsync(userId, testCts.Token), testCts.Token);
                await logoutAttemptingLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // Await tasks
                await Task.WhenAll(refreshTask, logoutAllTask);
            }
            finally
            {
                allowRefreshToCommit.TrySetResult(true);
                refreshHoldingLock.TrySetResult(true);
                logoutAttemptingLock.TrySetResult(true);

                var tasksToAwait = new List<(string Name, Task Task)>();
                if (refreshTask is not null) tasksToAwait.Add(("RefreshTask", refreshTask));
                if (logoutAllTask is not null) tasksToAwait.Add(("LogoutAllTask", logoutAllTask));

                var cleanupErrors = new List<Exception>();

                if (tasksToAwait.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(tasksToAwait.Select(t => t.Task)).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch
                    {
                    }

                    foreach (var (name, task) in tasksToAwait)
                    {
                        if (task.IsFaulted && task.Exception is not null)
                        {
                            foreach (var inner in task.Exception.Flatten().InnerExceptions)
                            {
                                if (testCts.IsCancellationRequested && (inner is OperationCanceledException or TaskCanceledException))
                                    continue;

                                cleanupErrors.Add(new InvalidOperationException($"Background task '{name}' faulted: {inner.Message}", inner));
                            }
                        }
                    }
                }

                var anyRunning = tasksToAwait.Any(t => !t.Task.IsCompleted);
                if (!anyRunning)
                {
                    await logoutContext.DisposeAsync();
                    await refreshContext.DisposeAsync();
                    testCts.Dispose();
                }

                if (cleanupErrors.Count > 0)
                {
                    throw new AggregateException("Cleanup detected faulted background tasks.", cleanupErrors);
                }
            }
        });

        // Verify the injected exception was NOT swallowed and the task name is explicitly identified
        Assert.Contains(ex.Flatten().InnerExceptions, inner =>
            inner.Message.Contains("LogoutAllTask") && inner.Message.Contains("Injected unexpected failure"));

        Assert.True(refreshTask?.IsCompleted ?? false);
        Assert.True(logoutAllTask?.IsCompleted ?? false);
    }

    private static async Task WaitForApplicationLockWaitAsync(
        DVLDDbContext monitorContext,
        int waitingSpid,
        int blockingSpid,
        string expectedResourceName,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var connection = monitorContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        while (DateTime.UtcNow < deadline)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM sys.dm_tran_locks l
                JOIN sys.dm_exec_requests r ON l.request_session_id = r.session_id
                WHERE l.request_session_id = @waitingSpid
                  AND l.resource_type = 'APPLICATION'
                  AND l.request_status = 'WAIT'
                  AND r.blocking_session_id = @blockingSpid
                  AND CHARINDEX(@expectedResourceName, l.resource_description) > 0";

            var p1 = cmd.CreateParameter();
            p1.ParameterName = "@waitingSpid";
            p1.Value = waitingSpid;
            cmd.Parameters.Add(p1);

            var p2 = cmd.CreateParameter();
            p2.ParameterName = "@blockingSpid";
            p2.Value = blockingSpid;
            cmd.Parameters.Add(p2);

            var p3 = cmd.CreateParameter();
            p3.ParameterName = "@expectedResourceName";
            p3.Value = expectedResourceName;
            cmd.Parameters.Add(p3);

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (count > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Timed out waiting for SPID {waitingSpid} to be blocked by SPID {blockingSpid} on application lock '{expectedResourceName}' in SQL Server engine.");
    }

    [Fact]
    public async Task Boundary6_LogoutAllRevokesAllSessionsBeforeRefreshAttemptsRotation_RefreshFailsAndNeverCommitsReplacement()
    {
        // Scenario:
        // 1. Initial state: user has an active session.
        // 2. LogoutAll completes first and commits.
        // 3. Refresh is attempted using the original token.
        // Invariant: Refresh fails with Unauthorized; no replacement session is created.
        var userId = await SeedUserAsync("auth.sql.boundary6");
        var initialToken = TokenService.CreateToken();
        var familyId = Guid.NewGuid();
        var session = NewSession(userId, initialToken, familyId);

        await using (var seed = _database.CreateContext())
        {
            seed.UserSessions.Add(session);
            await seed.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        await using (var logoutContext = _database.CreateContext())
        {
            var logoutService = BuildAuthService(logoutContext, new MutableClock(now));
            var logoutResult = await logoutService.LogoutAllAsync(userId);
            Assert.True(logoutResult.IsSuccess);
        }

        await using (var refreshContext = _database.CreateContext())
        {
            var refreshService = BuildAuthService(refreshContext, new MutableClock(now.AddSeconds(1)));
            var refreshResult = await refreshService.RefreshAsync(initialToken);

            Assert.False(refreshResult.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, refreshResult.ErrorType);
        }

        await using var verify = _database.CreateContext();
        var userSessions = await verify.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();

        Assert.Single(userSessions);
        Assert.NotNull(userSessions[0].RevokedAtUtc);
        Assert.Equal(SessionRevocationReasons.LogoutAll, userSessions[0].RevocationReason);
    }

    private static AuthService BuildAuthService(
        DVLDDbContext context,
        IJordanClock clock,
        IUserSessionRepository? sessionRepo = null) =>
        new(
            new UserRepository(context),
            sessionRepo ?? new UserSessionRepository(context),
            new StubJwtTokenService(),
            TokenService,
            new UnitOfWork(context),
            clock,
            Options.Create(new AuthSessionOptions()),
            NullLogger<AuthService>.Instance);

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

    private async Task<int> SeedUserAsync(string userName)
    {
        await using var context = _database.CreateContext();

        var country = new Country
        {
            CountryName = "Country-" + userName
        };

        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var person = new Person
        {
            NationalNo = "N" + Guid.NewGuid().ToString("N")[..9],
            FirstName = "Auth",
            SecondName = "Sql",
            LastName = userName,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Male,
            Address = "Test Address",
            Phone = "0790000000",
            NationalityCountryID = country.CountryId
        };

        context.People.Add(person);
        await context.SaveChangesAsync();

        var user = new User
        {
            PersonId = person.PersonId,
            UserName = userName,
            Password = "not-a-real-hash",
            IsActive = true,
            Role = UserRole.Staff
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.UserId;
    }

    private static UserSession NewSession(
        int userId,
        string rawToken,
        Guid familyId)
    {
        var now = DateTime.UtcNow;

        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = TokenService.HashToken(rawToken),
            TokenFamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(14),
            LastUsedAtUtc = now
        };
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
