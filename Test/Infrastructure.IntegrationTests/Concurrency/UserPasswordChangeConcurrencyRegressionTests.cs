using System.Runtime.ExceptionServices;
using Application.Common.Results;
using Application.DTOs.AuthDTO;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.IntegrationTests.Concurrency;

/// <summary>
/// Regression tests verifying pessimistic locking and concurrency serialization for User password changes.
/// Proves that because ChangePasswordAsync executes inside a transaction acquiring an UPDLOCK on the User row:
/// 1. Concurrent requests for the same user row are serialized at the database level (LCK_M_U row lock).
/// 2. When the first request commits a new password, the second unblocks, reloads the fresh hash,
///    and fails current-password verification ("Current password is incorrect.").
/// 3. The second request never overwrites the first request's password (Lost Update prevented).
/// </summary>
public sealed class UserPasswordChangeConcurrencyRegressionTests : IClassFixture<SqlServerTestDatabase>
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly SqlServerTestDatabase _database;
    private readonly ITestOutputHelper _output;
    private readonly IJordanClock _clock = new JordanClock();

    public UserPasswordChangeConcurrencyRegressionTests(SqlServerTestDatabase database, ITestOutputHelper output)
    {
        _database = database;
        _output = output;
    }

    public sealed record LockWaitEvidence(
        int WaitingSpid,
        int BlockingSpid,
        string WaitType,
        string ResourceType,
        string ResourceDescription,
        string WaiterRequestMode,
        string WaiterRequestStatus,
        string HolderRequestMode,
        string HolderRequestStatus,
        string? TableName,
        long WaitDurationMs);

    private static async Task<int> GetSpidAsync(DVLDDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        var conn = (SqlConnection)context.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT @@SPID;";
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var spid = Convert.ToInt32(result);

        if (conn.ServerProcessId != spid)
        {
            throw new InvalidOperationException($"Captured @@SPID ({spid}) does not match SqlConnection.ServerProcessId ({conn.ServerProcessId}).");
        }

        return spid;
    }

    private static async Task<string> GetUserRowLockResourceAsync(
        DVLDDbContext context,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var conn = (SqlConnection)context.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT %%lockres%% FROM [Users] WHERE [UserId] = @id;";
        cmd.Parameters.Add(new SqlParameter("@id", userId));
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            throw new InvalidOperationException($"Could not determine %%lockres%% for User {userId}.");
        }
        return result.ToString()!;
    }

    private async Task<LockWaitEvidence> WaitForLockContentionAsync(
        int expectedWaitingSpid,
        int expectedBlockingSpid,
        string expectedLockResource,
        string expectedTableName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        await using var monitorContext = _database.CreateContext();
        await monitorContext.Database.OpenConnectionAsync(linkedCts.Token);
        var conn = (SqlConnection)monitorContext.Database.GetDbConnection();

        while (!linkedCts.IsCancellationRequested)
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT
                        wt.session_id AS WaitingSpid,
                        wt.blocking_session_id AS BlockingSpid,
                        wt.wait_type AS WaitType,
                        wt.wait_duration_ms AS WaitDurationMs,
                        tl_waiter.resource_type AS ResourceType,
                        tl_waiter.resource_description AS ResourceDescription,
                        tl_waiter.request_mode AS WaiterRequestMode,
                        tl_waiter.request_status AS WaiterRequestStatus,
                        tl_holder.request_mode AS HolderRequestMode,
                        tl_holder.request_status AS HolderRequestStatus,
                        OBJECT_NAME(p.object_id, tl_waiter.resource_database_id) AS TableName
                    FROM sys.dm_os_waiting_tasks wt
                    JOIN sys.dm_tran_locks tl_waiter
                        ON tl_waiter.request_session_id = wt.session_id
                       AND tl_waiter.resource_type = 'KEY'
                       AND tl_waiter.request_mode = 'U'
                       AND tl_waiter.request_status = 'WAIT'
                    JOIN sys.dm_tran_locks tl_holder
                        ON tl_holder.request_session_id = wt.blocking_session_id
                       AND tl_holder.resource_type = tl_waiter.resource_type
                       AND tl_holder.resource_description = tl_waiter.resource_description
                       AND tl_holder.resource_associated_entity_id = tl_waiter.resource_associated_entity_id
                       AND tl_holder.resource_database_id = tl_waiter.resource_database_id
                       AND tl_holder.request_mode = 'U'
                       AND tl_holder.request_status = 'GRANT'
                    LEFT JOIN sys.partitions p
                        ON tl_waiter.resource_associated_entity_id = p.hobt_id
                    WHERE wt.session_id = @waitingSpid
                      AND wt.blocking_session_id = @blockingSpid
                      AND tl_waiter.resource_database_id = DB_ID()
                      AND tl_waiter.resource_description = @expectedLockResource;
                    """;

                cmd.Parameters.Add(new SqlParameter("@waitingSpid", expectedWaitingSpid));
                cmd.Parameters.Add(new SqlParameter("@blockingSpid", expectedBlockingSpid));
                cmd.Parameters.Add(new SqlParameter("@expectedLockResource", expectedLockResource));

                await using var reader = await cmd.ExecuteReaderAsync(linkedCts.Token);
                if (await reader.ReadAsync(linkedCts.Token))
                {
                    var waitingSpid = Convert.ToInt32(reader["WaitingSpid"]);
                    var blockingSpid = Convert.ToInt32(reader["BlockingSpid"]);
                    var waitType = reader.GetString(reader.GetOrdinal("WaitType"));
                    var tableName = reader.IsDBNull(reader.GetOrdinal("TableName")) ? null : reader.GetString(reader.GetOrdinal("TableName"));
                    var resourceType = reader.GetString(reader.GetOrdinal("ResourceType"));
                    var resourceDescription = reader.GetString(reader.GetOrdinal("ResourceDescription")).Trim();
                    var waiterMode = reader.GetString(reader.GetOrdinal("WaiterRequestMode"));
                    var waiterStatus = reader.GetString(reader.GetOrdinal("WaiterRequestStatus"));
                    var holderMode = reader.GetString(reader.GetOrdinal("HolderRequestMode"));
                    var holderStatus = reader.GetString(reader.GetOrdinal("HolderRequestStatus"));
                    var waitDuration = Convert.ToInt64(reader["WaitDurationMs"]);

                    if (string.Equals(tableName, expectedTableName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(resourceType, "KEY", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(waiterMode, "U", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(waiterStatus, "WAIT", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(holderMode, "U", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(holderStatus, "GRANT", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(resourceDescription, expectedLockResource, StringComparison.OrdinalIgnoreCase) &&
                        waitType.StartsWith("LCK_M_", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LockWaitEvidence(
                            WaitingSpid: waitingSpid,
                            BlockingSpid: blockingSpid,
                            WaitType: waitType,
                            ResourceType: resourceType,
                            ResourceDescription: resourceDescription,
                            WaiterRequestMode: waiterMode,
                            WaiterRequestStatus: waiterStatus,
                            HolderRequestMode: holderMode,
                            HolderRequestStatus: holderStatus,
                            TableName: tableName,
                            WaitDurationMs: waitDuration);
                    }
                }
            }

            await Task.Delay(50, linkedCts.Token);
        }

        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds}s waiting for SPID {expectedWaitingSpid} to be blocked by SPID {expectedBlockingSpid} on {expectedTableName} resource {expectedLockResource}.");
    }

    /// <summary>
    /// Verifies that two concurrent password change requests for the same user are serialized:
    /// 1. Request 1 acquires UPDLOCK, pauses.
    /// 2. Request 2 attempts to change password with old password, blocks in SQL Server on LCK_M_U.
    /// 3. Request 1 resumes, hashes NewPassword1, revokes sessions, and commits.
    /// 4. Request 2 unblocks, reloads user row, verifies old password against NewPassword1 hash, fails with ValidationFailure.
    /// 5. Database state reflects NewPassword1. NewPassword2 was never applied.
    /// </summary>
    [Fact]
    public async Task ConcurrentPasswordChanges_SerializedByPessimisticLock_FirstRequestSucceeds_SecondFailsWithInvalidPassword()
    {
        const string oldPassword = "OldPassword123!";
        const string newPassword1 = "NewPasswordOne1!";
        const string newPassword2 = "NewPasswordTwo2!";

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, initialHash, initialStamp) = await SeedUserAsync($"pwd_conc_{suffix}", oldPassword);

        var req1LockAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReq1ToProceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var context1 = _database.CreateContext();
        var context2 = _database.CreateContext();

        var spid1 = await GetSpidAsync(context1);
        var spid2 = await GetSpidAsync(context2);
        var expectedLockResource = await GetUserRowLockResourceAsync(context1, userId);

        Task<Result>? task1 = null;
        Task<Result>? task2 = null;
        Exception? primaryException = null;

        try
        {
            // 1. Configure Request 1 with pausing decorator
            var innerRepo1 = new UserRepository(context1);
            var pausingRepo1 = new PausingUserRepositoryDecorator(
                innerRepo1,
                context1,
                spid1,
                onAfterGetForPasswordChange: async ct =>
                {
                    req1LockAcquired.TrySetResult(true);
                    await allowReq1ToProceed.Task.WaitAsync(DefaultTimeout, ct);
                });

            var sessionRepo1 = new UserSessionRepository(context1);
            var uow1 = new UnitOfWork(context1);
            var service1 = new UserService(pausingRepo1, uow1, sessionRepo1, _clock);

            // 2. Start Request 1 in the background
            task1 = Task.Run(async () =>
            {
                return await service1.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = oldPassword,
                    NewPassword = newPassword1
                }, cts1.Token);
            });

            // 3. Wait until Request 1 has acquired UPDLOCK on the user row
            await req1LockAcquired.Task.WaitAsync(DefaultTimeout);

            // 4. Configure Request 2 on Context 2 and start in background
            var innerRepo2 = new UserRepository(context2);
            var verifiedRepo2 = new PausingUserRepositoryDecorator(
                innerRepo2,
                context2,
                spid2,
                onAfterGetForPasswordChange: null);

            var sessionRepo2 = new UserSessionRepository(context2);
            var uow2 = new UnitOfWork(context2);
            var service2 = new UserService(verifiedRepo2, uow2, sessionRepo2, _clock);

            task2 = Task.Run(async () =>
            {
                return await service2.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = oldPassword,
                    NewPassword = newPassword2
                }, cts2.Token);
            });

            // 5. Verify SQL Server row-lock contention: Request 2 (SPID 2) is blocked waiting for Request 1 (SPID 1)
            var evidence = await WaitForLockContentionAsync(
                expectedWaitingSpid: spid2,
                expectedBlockingSpid: spid1,
                expectedLockResource: expectedLockResource,
                expectedTableName: "Users",
                timeout: DefaultTimeout);

            _output.WriteLine(
                $"SQL Server Lock Contention Verified: Waiter SPID {evidence.WaitingSpid} blocked by Holder SPID {evidence.BlockingSpid} on {evidence.TableName} ({evidence.ResourceDescription}) for {evidence.WaitDurationMs}ms (Mode: {evidence.WaiterRequestMode}, Status: {evidence.WaiterRequestStatus}).");

            Assert.Equal(spid2, evidence.WaitingSpid);
            Assert.Equal(spid1, evidence.BlockingSpid);
            Assert.Equal("Users", evidence.TableName, ignoreCase: true);
            Assert.Equal("U", evidence.WaiterRequestMode);
            Assert.Equal("WAIT", evidence.WaiterRequestStatus);
            Assert.Equal("U", evidence.HolderRequestMode);
            Assert.Equal("GRANT", evidence.HolderRequestStatus);

            // 6. Release Request 1 to complete its password change and commit
            allowReq1ToProceed.TrySetResult(true);

            var result1 = await task1.WaitAsync(DefaultTimeout);
            Assert.True(result1.IsSuccess, $"Request 1 failed: {result1.ErrorType} - {result1.Error}");

            // 7. Await Request 2; it unblocks, reloads user row, discovers old password is now invalid, and fails
            var result2 = await task2.WaitAsync(DefaultTimeout);
            Assert.False(result2.IsSuccess);
            Assert.Equal(ErrorType.Validation, result2.ErrorType);
            Assert.Equal("Current password is incorrect.", result2.Error);

            // 8. Verify the final persisted state in SQL Server using a fresh verification context
            await using var verifyContext = _database.CreateContext();
            var finalUser = await verifyContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

            // Request 1's password is now the active password
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword1, finalUser.Password));
            Assert.False(BCrypt.Net.BCrypt.Verify(newPassword2, finalUser.Password));
            Assert.False(BCrypt.Net.BCrypt.Verify(oldPassword, finalUser.Password));

            // SecurityStamp was rotated by Request 1
            Assert.NotEqual(initialStamp, finalUser.SecurityStamp);
            Assert.False(string.IsNullOrWhiteSpace(finalUser.SecurityStamp));
        }
        catch (Exception ex)
        {
            primaryException = ex;
            throw;
        }
        finally
        {
            var cleanupErrors = new List<Exception>();

            allowReq1ToProceed.TrySetResult(true);

            await AwaitTaskWithCooperativeCancellationAsync(task1, cts1, DefaultTimeout, cleanupErrors);
            await AwaitTaskWithCooperativeCancellationAsync(task2, cts2, DefaultTimeout, cleanupErrors);

            try
            {
                await context1.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            try
            {
                await context2.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            if (cleanupErrors.Count > 0)
            {
                if (primaryException != null)
                {
                    throw new AggregateException(
                        "Primary test failure occurred and subsequent cleanup errors were encountered.",
                        new[] { primaryException }.Concat(cleanupErrors));
                }

                if (cleanupErrors.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(cleanupErrors[0]).Throw();
                }

                throw new AggregateException("Cleanup failed.", cleanupErrors);
            }
        }
    }

    /// <summary>
    /// Verifies that when the first request holding the lock fails validation (e.g. invalid current password)
    /// and rolls back, the second request waiting on the lock unblocks, verifies the correct current password,
    /// and succeeds in updating the password.
    /// </summary>
    [Fact]
    public async Task ConcurrentPasswordChanges_WhenFirstRequestFailsValidation_SecondRequestProceedsAndSucceeds()
    {
        const string oldPassword = "OldPassword123!";
        const string wrongOldPassword = "WrongPassword999!";
        const string newPassword1 = "NewPasswordOne1!";
        const string newPassword2 = "NewPasswordTwo2!";

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, initialHash, initialStamp) = await SeedUserAsync($"pwd_fail_{suffix}", oldPassword);

        var req1LockAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReq1ToProceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var context1 = _database.CreateContext();
        var context2 = _database.CreateContext();

        var spid1 = await GetSpidAsync(context1);
        var spid2 = await GetSpidAsync(context2);
        var expectedLockResource = await GetUserRowLockResourceAsync(context1, userId);

        Task<Result>? task1 = null;
        Task<Result>? task2 = null;
        Exception? primaryException = null;

        try
        {
            var innerRepo1 = new UserRepository(context1);
            var pausingRepo1 = new PausingUserRepositoryDecorator(
                innerRepo1,
                context1,
                spid1,
                onAfterGetForPasswordChange: async ct =>
                {
                    req1LockAcquired.TrySetResult(true);
                    await allowReq1ToProceed.Task.WaitAsync(DefaultTimeout, ct);
                });

            var sessionRepo1 = new UserSessionRepository(context1);
            var uow1 = new UnitOfWork(context1);
            var service1 = new UserService(pausingRepo1, uow1, sessionRepo1, _clock);

            // Request 1 starts with wrong current password
            task1 = Task.Run(async () =>
            {
                return await service1.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = wrongOldPassword,
                    NewPassword = newPassword1
                }, cts1.Token);
            });

            await req1LockAcquired.Task.WaitAsync(DefaultTimeout);

            // Request 2 starts with valid current password
            var innerRepo2 = new UserRepository(context2);
            var verifiedRepo2 = new PausingUserRepositoryDecorator(
                innerRepo2,
                context2,
                spid2,
                onAfterGetForPasswordChange: null);

            var sessionRepo2 = new UserSessionRepository(context2);
            var uow2 = new UnitOfWork(context2);
            var service2 = new UserService(verifiedRepo2, uow2, sessionRepo2, _clock);

            task2 = Task.Run(async () =>
            {
                return await service2.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = oldPassword,
                    NewPassword = newPassword2
                }, cts2.Token);
            });

            // Verify Request 2 is blocked on Request 1
            var evidence = await WaitForLockContentionAsync(
                expectedWaitingSpid: spid2,
                expectedBlockingSpid: spid1,
                expectedLockResource: expectedLockResource,
                expectedTableName: "Users",
                timeout: DefaultTimeout);

            Assert.Equal(spid2, evidence.WaitingSpid);
            Assert.Equal(spid1, evidence.BlockingSpid);

            // Allow Request 1 to proceed (it will fail validation and rollback)
            allowReq1ToProceed.TrySetResult(true);

            var result1 = await task1.WaitAsync(DefaultTimeout);
            Assert.False(result1.IsSuccess);
            Assert.Equal(ErrorType.Validation, result1.ErrorType);
            Assert.Equal("Current password is incorrect.", result1.Error);

            // Request 2 unblocks and succeeds
            var result2 = await task2.WaitAsync(DefaultTimeout);
            Assert.True(result2.IsSuccess, $"Request 2 failed: {result2.ErrorType} - {result2.Error}");

            // Verify final DB state has NewPassword2
            await using var verifyContext = _database.CreateContext();
            var finalUser = await verifyContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword2, finalUser.Password));
            Assert.False(BCrypt.Net.BCrypt.Verify(newPassword1, finalUser.Password));
            Assert.False(BCrypt.Net.BCrypt.Verify(oldPassword, finalUser.Password));
            Assert.NotEqual(initialStamp, finalUser.SecurityStamp);
        }
        catch (Exception ex)
        {
            primaryException = ex;
            throw;
        }
        finally
        {
            var cleanupErrors = new List<Exception>();

            allowReq1ToProceed.TrySetResult(true);

            await AwaitTaskWithCooperativeCancellationAsync(task1, cts1, DefaultTimeout, cleanupErrors);
            await AwaitTaskWithCooperativeCancellationAsync(task2, cts2, DefaultTimeout, cleanupErrors);

            try
            {
                await context1.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            try
            {
                await context2.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            if (cleanupErrors.Count > 0)
            {
                if (primaryException != null)
                {
                    throw new AggregateException(
                        "Primary test failure occurred and subsequent cleanup errors were encountered.",
                        new[] { primaryException }.Concat(cleanupErrors));
                }

                if (cleanupErrors.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(cleanupErrors[0]).Throw();
                }

                throw new AggregateException("Cleanup failed.", cleanupErrors);
            }
        }
    }

    private async Task<(int UserId, string PasswordHash, string SecurityStamp)> SeedUserAsync(string username, string rawPassword)
    {
        await using var context = _database.CreateContext();

        var country = await context.Countries.FirstOrDefaultAsync();
        if (country == null)
        {
            country = new Country { CountryName = $"Country_{Guid.NewGuid():N}"[..20] };
            context.Countries.Add(country);
            await context.SaveChangesAsync();
        }

        var person = new Person
        {
            FirstName = "Concurrency",
            SecondName = "Middle",
            LastName = "User",
            NationalNo = $"N_{Guid.NewGuid():N}"[..20],
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Address = "Street 1",
            Phone = "0790000000",
            Email = $"concurrency_{Guid.NewGuid():N}@test.com",
            NationalityCountryID = country.CountryId
        };
        context.People.Add(person);
        await context.SaveChangesAsync();

        var user = new User
        {
            PersonId = person.PersonId,
            UserName = username,
            Password = BCrypt.Net.BCrypt.HashPassword(rawPassword),
            IsActive = true,
            Role = UserRole.Staff,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return (user.UserId, user.Password, user.SecurityStamp);
    }

    private static async Task AwaitTaskWithCooperativeCancellationAsync(
        Task? task,
        CancellationTokenSource cts,
        TimeSpan timeout,
        List<Exception> cleanupErrors)
    {
        if (task == null) return;

        try
        {
            if (!task.IsCompleted)
            {
                cts.Cancel();
            }

            await task.WaitAsync(timeout);
        }
        catch (OperationCanceledException)
        {
            // Expected during cooperative cancellation
        }
        catch (TimeoutException tex)
        {
            cleanupErrors.Add(new TimeoutException($"Background task did not complete within {timeout.TotalSeconds}s during test cleanup.", tex));
        }
        catch (Exception ex)
        {
            cleanupErrors.Add(ex);
        }
    }

    private sealed class PausingUserRepositoryDecorator : IUserRepository
    {
        private readonly IUserRepository _inner;
        private readonly DVLDDbContext _context;
        private readonly int _expectedSpid;
        private readonly Func<CancellationToken, Task>? _onAfterGetForPasswordChange;

        public PausingUserRepositoryDecorator(
            IUserRepository inner,
            DVLDDbContext context,
            int expectedSpid,
            Func<CancellationToken, Task>? onAfterGetForPasswordChange)
        {
            _inner = inner;
            _context = context;
            _expectedSpid = expectedSpid;
            _onAfterGetForPasswordChange = onAfterGetForPasswordChange;
        }

        private void VerifySpid()
        {
            var conn = (SqlConnection)_context.Database.GetDbConnection();
            if (conn.ServerProcessId != _expectedSpid)
            {
                throw new InvalidOperationException($"SPID changed unexpectedly! Expected: {_expectedSpid}, Actual: {conn.ServerProcessId}");
            }
        }

        public async Task<User?> GetUserForPasswordChangeAsync(int id, CancellationToken cancellationToken = default)
        {
            VerifySpid();
            var user = await _inner.GetUserForPasswordChangeAsync(id, cancellationToken);
            VerifySpid();

            if (_onAfterGetForPasswordChange != null)
            {
                await _onAfterGetForPasswordChange(cancellationToken);
                VerifySpid();
            }

            return user;
        }

        public Task<User?> GetUserForUpdateAsync(int id, CancellationToken cancellationToken = default) => _inner.GetUserForUpdateAsync(id, cancellationToken);
        public Task<User?> GetUserByUserIdAsync(int id, CancellationToken cancellationToken = default) => _inner.GetUserByUserIdAsync(id, cancellationToken);
        public Task<User?> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default) => _inner.GetUserProfileAsync(userId, cancellationToken);
        public Task<UserDetailsDto?> GetUserDetailsByIdAsync(int userId, CancellationToken cancellationToken = default) => _inner.GetUserDetailsByIdAsync(userId, cancellationToken);
        public Task<User?> GetUserByPersonIdAsync(int personId, CancellationToken cancellationToken = default) => _inner.GetUserByPersonIdAsync(personId, cancellationToken);
        public Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default) => _inner.GetUserByUsernameAsync(username, cancellationToken);
        public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) => _inner.GetAllUsersAsync(cancellationToken);
        public Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken = default) => _inner.IsUsernameTakenAsync(username, cancellationToken);
        public Task<bool> IsUsernameTakenForAnotherUserAsync(string username, int userId, CancellationToken cancellationToken = default) => _inner.IsUsernameTakenForAnotherUserAsync(username, userId, cancellationToken);
        public Task<bool> IsUserExistsByPersonIdAsync(int personId, CancellationToken cancellationToken = default) => _inner.IsUserExistsByPersonIdAsync(personId, cancellationToken);
        public Task<bool> IsUserActiveAsync(int userId, CancellationToken cancellationToken = default) => _inner.IsUserActiveAsync(userId, cancellationToken);
        public Task<UserSecurityState?> GetSecurityStateAsync(int userId, CancellationToken cancellationToken = default) => _inner.GetSecurityStateAsync(userId, cancellationToken);
        public Task<UserSecurityState?> GetSecurityStateAsync(int userId, Guid? sessionId, DateTime nowUtc, CancellationToken cancellationToken = default) => _inner.GetSecurityStateAsync(userId, sessionId, nowUtc, cancellationToken);
        public Task<int> GetActiveAdminCountAsync(CancellationToken cancellationToken = default) => _inner.GetActiveAdminCountAsync(cancellationToken);
        public Task<bool> AcquireAdminMutationLockAsync(CancellationToken cancellationToken = default) => _inner.AcquireAdminMutationLockAsync(cancellationToken);
        public Task AddUserAsync(User user, CancellationToken cancellationToken = default) => _inner.AddUserAsync(user, cancellationToken);
        public void DeleteUser(User user) => _inner.DeleteUser(user);
    }
}
