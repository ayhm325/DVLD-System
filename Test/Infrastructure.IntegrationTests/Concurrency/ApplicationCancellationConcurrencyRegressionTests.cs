using System.Runtime.ExceptionServices;
using Application.Common.Results;
using Application.DTOs.ApplicationDTO;
using Application.DTOs.DriverDTO;
using Application.DTOs.LicenseDTO;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Mappers;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.IntegrationTests.Concurrency;

/// <summary>
/// Regression tests verifying concurrency protection between Application Cancellation and License Issuance.
/// Proves that because CancelApplicationAsync executes inside a transaction acquiring an UPDLOCK on the Application row,
/// concurrent execution is serialized at the database level and the application state remains consistent regardless of execution order:
/// 1. Issuance commits first -> Cancellation is blocked on the row lock, re-reads status as Completed, and fails with Conflict.
/// 2. Cancellation commits first -> Issuance is blocked on the row lock, re-reads status as Cancelled, and fails with Conflict.
/// </summary>
public sealed class ApplicationCancellationConcurrencyRegressionTests : IClassFixture<SqlServerTestDatabase>
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly SqlServerTestDatabase _database;
    private readonly ITestOutputHelper _output;
    private readonly IJordanClock _clock = new JordanClock();

    public ApplicationCancellationConcurrencyRegressionTests(SqlServerTestDatabase database, ITestOutputHelper output)
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
        // Explicitly open the underlying database connection so EF Core pins it and keeps it open across all subsequent operations.
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

    private static async Task<string> GetApplicationRowLockResourceAsync(
        DVLDDbContext context,
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        var conn = (SqlConnection)context.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT %%lockres%% FROM [Applications] WHERE [ApplicationID] = @id;";
        cmd.Parameters.Add(new SqlParameter("@id", applicationId));
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            throw new InvalidOperationException($"Could not determine %%lockres%% for Application {applicationId}.");
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
    /// Order 1: Issuance commits first.
    /// LicenseIssuanceService acquires UPDLOCK on the Application row.
    /// CancelApplicationAsync attempts to acquire UPDLOCK and is blocked by SQL Server.
    /// Issuance completes the application and commits.
    /// Cancellation unblocks, observes the Completed status, and fails with Conflict.
    /// Persisted database state: Application is Completed and active license exists.
    /// </summary>
    [Fact]
    public async Task Order1_IssuanceCommitsFirst_CancellationFailsWithConflictAndStatusRemainsCompleted()
    {
        await SeedRequiredMetadataAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (personId, userId, applicationId, localAppId) = await SeedApplicationAndExaminationsAsync(suffix);

        var issuanceLockAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowIssuanceToProceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var issuanceCts = new CancellationTokenSource();
        using var cancelCts = new CancellationTokenSource();

        var issuanceContext = _database.CreateContext();
        var cancelContext = _database.CreateContext();

        var issuanceSpid = await GetSpidAsync(issuanceContext);
        var cancelSpid = await GetSpidAsync(cancelContext);
        var expectedLockResource = await GetApplicationRowLockResourceAsync(issuanceContext, applicationId);

        Task<Result<int>>? issuanceTask = null;
        Task<Result>? cancelTask = null;
        Exception? primaryException = null;

        try
        {
            // 1. Configure Issuance on Context 1 with a decorator that signals lock acquisition, verifies SPID stability, and pauses before completing
            var innerIssuanceRepo = new ApplicationRepository(issuanceContext);
            var pausingIssuanceRepo = new PausingApplicationRepositoryDecorator(
                innerIssuanceRepo,
                issuanceContext,
                issuanceSpid,
                onAfterGetForIssuance: async ct =>
                {
                    issuanceLockAcquired.TrySetResult(true);
                    await allowIssuanceToProceed.Task.WaitAsync(DefaultTimeout, ct);
                },
                onAfterGetForCancellation: null);

            var issuanceService = CreateLicenseIssuanceServiceWithRepo(issuanceContext, userId, pausingIssuanceRepo);

            // Start Issuance in the background
            issuanceTask = Task.Run(async () =>
            {
                return await issuanceService.IssueFirstLicenseAsync(localAppId, "Issued first time", issuanceCts.Token);
            });

            // 2. Wait until Issuance has acquired the UPDLOCK on the application row
            await issuanceLockAcquired.Task.WaitAsync(DefaultTimeout);

            // 3. Start Cancellation in the background on Context 2 with SPID stability verification
            var innerCancelRepo = new ApplicationRepository(cancelContext);
            var verifiedCancelRepo = new PausingApplicationRepositoryDecorator(
                innerCancelRepo,
                cancelContext,
                cancelSpid,
                onAfterGetForIssuance: null,
                onAfterGetForCancellation: null);

            var cancelService = CreateApplicationServiceWithRepo(cancelContext, userId, verifiedCancelRepo);
            cancelTask = Task.Run(async () =>
            {
                return await cancelService.CancelApplicationAsync(applicationId, cancelCts.Token);
            });

            // 4. Deterministically verify via SQL Server DMVs that Cancellation (cancelSpid) is actively blocked
            // by Issuance (issuanceSpid) on the exact row lock resource of Applications (applicationId).
            var lockEvidence = await WaitForLockContentionAsync(
                cancelSpid,
                issuanceSpid,
                expectedLockResource,
                "Applications",
                DefaultTimeout,
                cancelCts.Token);

            _output.WriteLine($"[Order 1 Captured LockEvidence] WaitingSpid={lockEvidence.WaitingSpid}, BlockingSpid={lockEvidence.BlockingSpid}, WaitType={lockEvidence.WaitType}, ResourceType={lockEvidence.ResourceType}, ResourceDescription={lockEvidence.ResourceDescription}, WaiterMode={lockEvidence.WaiterRequestMode}/{lockEvidence.WaiterRequestStatus}, HolderMode={lockEvidence.HolderRequestMode}/{lockEvidence.HolderRequestStatus}, TableName={lockEvidence.TableName}, WaitDurationMs={lockEvidence.WaitDurationMs}");

            Assert.Equal(cancelSpid, lockEvidence.WaitingSpid);
            Assert.Equal(issuanceSpid, lockEvidence.BlockingSpid);
            Assert.Equal(expectedLockResource, lockEvidence.ResourceDescription);
            Assert.Equal("Applications", lockEvidence.TableName, ignoreCase: true);
            Assert.Equal("KEY", lockEvidence.ResourceType);
            Assert.Equal("U", lockEvidence.WaiterRequestMode);
            Assert.Equal("WAIT", lockEvidence.WaiterRequestStatus);
            Assert.Equal("U", lockEvidence.HolderRequestMode);
            Assert.Equal("GRANT", lockEvidence.HolderRequestStatus);
            Assert.StartsWith("LCK_M_", lockEvidence.WaitType, StringComparison.OrdinalIgnoreCase);
            Assert.True(lockEvidence.WaitDurationMs >= 0);

            // 5. Release Issuance to proceed, save license, mark application Completed, and commit
            allowIssuanceToProceed.TrySetResult(true);

            var issuanceResult = await issuanceTask.WaitAsync(DefaultTimeout);
            Assert.True(issuanceResult.IsSuccess, $"{issuanceResult.ErrorType}: {issuanceResult.Error}");

            // 6. Cancellation unblocks in SQL Server, reads Completed status, and fails with Conflict
            var cancelResult = await cancelTask.WaitAsync(DefaultTimeout);
            Assert.False(cancelResult.IsSuccess, "Cancellation must fail when application is already Completed.");
            Assert.Equal(ErrorType.Conflict, cancelResult.ErrorType);
            Assert.Equal("Cannot cancel completed application.", cancelResult.Error);

            // 7. Verify persisted database state using a fresh independent DbContext
            await using (var verifyContext = _database.CreateContext())
            {
                var finalApp = await verifyContext.Applications.AsNoTracking().SingleAsync(a => a.ApplicationID == applicationId);
                var finalLicense = await verifyContext.Licenses.AsNoTracking().SingleOrDefaultAsync(l => l.ApplicationID == applicationId);

                Assert.Equal(AppStatus.Completed, finalApp.ApplicationStatus);
                Assert.NotEqual(AppStatus.Cancelled, finalApp.ApplicationStatus);

                Assert.NotNull(finalLicense);
                Assert.True(finalLicense.IsActive, "Issued license must remain active.");
            }
        }
        catch (Exception ex)
        {
            primaryException = ex;
            throw;
        }
        finally
        {
            var cleanupErrors = new List<Exception>();

            // Guarantee completion sources are unblocked
            issuanceLockAcquired.TrySetResult(true);
            allowIssuanceToProceed.TrySetResult(true);

            // Await background tasks with cooperative cancellation before disposing DbContexts
            await AwaitTaskWithCooperativeCancellationAsync(issuanceTask, issuanceCts, DefaultTimeout, cleanupErrors);
            await AwaitTaskWithCooperativeCancellationAsync(cancelTask, cancelCts, DefaultTimeout, cleanupErrors);

            try
            {
                await issuanceContext.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            try
            {
                await cancelContext.DisposeAsync();
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
    /// Order 2: Cancellation commits first.
    /// CancelApplicationAsync acquires UPDLOCK on the Application row.
    /// LicenseIssuanceService attempts to acquire UPDLOCK and is blocked by SQL Server.
    /// Cancellation marks the application Cancelled and commits.
    /// Issuance unblocks, observes the Cancelled status, and fails with Conflict.
    /// Persisted database state: Application is Cancelled and NO license is created.
    /// </summary>
    [Fact]
    public async Task Order2_CancellationCommitsFirst_IssuanceFailsWithConflictAndStatusRemainsCancelled()
    {
        await SeedRequiredMetadataAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (personId, userId, applicationId, localAppId) = await SeedApplicationAndExaminationsAsync(suffix);

        var cancelLockAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCancelToProceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancelCts = new CancellationTokenSource();
        using var issuanceCts = new CancellationTokenSource();

        var cancelContext = _database.CreateContext();
        var issuanceContext = _database.CreateContext();

        var cancelSpid = await GetSpidAsync(cancelContext);
        var issuanceSpid = await GetSpidAsync(issuanceContext);
        var expectedLockResource = await GetApplicationRowLockResourceAsync(cancelContext, applicationId);

        Task<Result>? cancelTask = null;
        Task<Result<int>>? issuanceTask = null;
        Exception? primaryException = null;

        try
        {
            // 1. Configure Cancellation on Context 1 with a decorator that signals lock acquisition, verifies SPID stability, and pauses before saving
            var innerCancelRepo = new ApplicationRepository(cancelContext);
            var pausingCancelRepo = new PausingApplicationRepositoryDecorator(
                innerCancelRepo,
                cancelContext,
                cancelSpid,
                onAfterGetForIssuance: null,
                onAfterGetForCancellation: async ct =>
                {
                    cancelLockAcquired.TrySetResult(true);
                    await allowCancelToProceed.Task.WaitAsync(DefaultTimeout, ct);
                });

            var cancelService = CreateApplicationServiceWithRepo(cancelContext, userId, pausingCancelRepo);

            // Start Cancellation in the background
            cancelTask = Task.Run(async () =>
            {
                return await cancelService.CancelApplicationAsync(applicationId, cancelCts.Token);
            });

            // 2. Wait until Cancellation has acquired the UPDLOCK on the application row
            await cancelLockAcquired.Task.WaitAsync(DefaultTimeout);

            // 3. Start Issuance in the background on Context 2 with SPID stability verification
            var innerIssuanceRepo = new ApplicationRepository(issuanceContext);
            var verifiedIssuanceRepo = new PausingApplicationRepositoryDecorator(
                innerIssuanceRepo,
                issuanceContext,
                issuanceSpid,
                onAfterGetForIssuance: null,
                onAfterGetForCancellation: null);

            var issuanceService = CreateLicenseIssuanceServiceWithRepo(issuanceContext, userId, verifiedIssuanceRepo);
            issuanceTask = Task.Run(async () =>
            {
                return await issuanceService.IssueFirstLicenseAsync(localAppId, "First license", issuanceCts.Token);
            });

            // 4. Deterministically verify via SQL Server DMVs that Issuance (issuanceSpid) is actively blocked
            // by Cancellation (cancelSpid) on the exact row lock resource of Applications (applicationId).
            var lockEvidence = await WaitForLockContentionAsync(
                issuanceSpid,
                cancelSpid,
                expectedLockResource,
                "Applications",
                DefaultTimeout,
                issuanceCts.Token);

            _output.WriteLine($"[Order 2 Captured LockEvidence] WaitingSpid={lockEvidence.WaitingSpid}, BlockingSpid={lockEvidence.BlockingSpid}, WaitType={lockEvidence.WaitType}, ResourceType={lockEvidence.ResourceType}, ResourceDescription={lockEvidence.ResourceDescription}, WaiterMode={lockEvidence.WaiterRequestMode}/{lockEvidence.WaiterRequestStatus}, HolderMode={lockEvidence.HolderRequestMode}/{lockEvidence.HolderRequestStatus}, TableName={lockEvidence.TableName}, WaitDurationMs={lockEvidence.WaitDurationMs}");

            Assert.Equal(issuanceSpid, lockEvidence.WaitingSpid);
            Assert.Equal(cancelSpid, lockEvidence.BlockingSpid);
            Assert.Equal(expectedLockResource, lockEvidence.ResourceDescription);
            Assert.Equal("Applications", lockEvidence.TableName, ignoreCase: true);
            Assert.Equal("KEY", lockEvidence.ResourceType);
            Assert.Equal("U", lockEvidence.WaiterRequestMode);
            Assert.Equal("WAIT", lockEvidence.WaiterRequestStatus);
            Assert.Equal("U", lockEvidence.HolderRequestMode);
            Assert.Equal("GRANT", lockEvidence.HolderRequestStatus);
            Assert.StartsWith("LCK_M_", lockEvidence.WaitType, StringComparison.OrdinalIgnoreCase);
            Assert.True(lockEvidence.WaitDurationMs >= 0);

            // 5. Release Cancellation to proceed, mark Cancelled, save, and commit
            allowCancelToProceed.TrySetResult(true);

            var cancelResult = await cancelTask.WaitAsync(DefaultTimeout);
            Assert.True(cancelResult.IsSuccess, $"{cancelResult.ErrorType}: {cancelResult.Error}");

            // 6. Issuance unblocks in SQL Server, reads Cancelled status, and fails with Conflict
            var issuanceResult = await issuanceTask.WaitAsync(DefaultTimeout);
            Assert.False(issuanceResult.IsSuccess, "Issuance must fail when application is already Cancelled.");
            Assert.Equal(ErrorType.Conflict, issuanceResult.ErrorType);
            Assert.Equal("The application is not in a valid state for license issuance.", issuanceResult.Error);

            // 7. Verify persisted database state using a fresh independent DbContext
            await using (var verifyContext = _database.CreateContext())
            {
                var finalApp = await verifyContext.Applications.AsNoTracking().SingleAsync(a => a.ApplicationID == applicationId);
                var finalLicense = await verifyContext.Licenses.AsNoTracking().SingleOrDefaultAsync(l => l.ApplicationID == applicationId);

                Assert.Equal(AppStatus.Cancelled, finalApp.ApplicationStatus);
                Assert.NotEqual(AppStatus.Completed, finalApp.ApplicationStatus);

                Assert.Null(finalLicense);
            }
        }
        catch (Exception ex)
        {
            primaryException = ex;
            throw;
        }
        finally
        {
            var cleanupErrors = new List<Exception>();

            // Guarantee completion sources are unblocked
            cancelLockAcquired.TrySetResult(true);
            allowCancelToProceed.TrySetResult(true);

            // Await background tasks with cooperative cancellation before disposing DbContexts
            await AwaitTaskWithCooperativeCancellationAsync(cancelTask, cancelCts, DefaultTimeout, cleanupErrors);
            await AwaitTaskWithCooperativeCancellationAsync(issuanceTask, issuanceCts, DefaultTimeout, cleanupErrors);

            try
            {
                await cancelContext.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            try
            {
                await issuanceContext.DisposeAsync();
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

    // =========================================================
    // SERVICE FACTORIES
    // =========================================================

    private ApplicationService CreateApplicationService(DVLDDbContext context, int userId)
    {
        var repo = new ApplicationRepository(context);
        return CreateApplicationServiceWithRepo(context, userId, repo);
    }

    private ApplicationService CreateApplicationServiceWithRepo(DVLDDbContext context, int userId, IApplicationRepository repo)
    {
        var uow = new UnitOfWork(context);
        var appTypeRepo = new ApplicationTypeRepository(context);
        var currentUser = new TestCurrentUser(userId);

        return new ApplicationService(repo, appTypeRepo, uow, currentUser, _clock);
    }

    private LicenseIssuanceService CreateLicenseIssuanceService(DVLDDbContext context, int userId)
    {
        var appRepo = new ApplicationRepository(context);
        return CreateLicenseIssuanceServiceWithRepo(context, userId, appRepo);
    }

    private LicenseIssuanceService CreateLicenseIssuanceServiceWithRepo(DVLDDbContext context, int userId, IApplicationRepository appRepo)
    {
        var currentUser = new TestCurrentUser(userId);
        var uow = new UnitOfWork(context);
        var licenseRepo = new LicenseRepository(context);
        var appTypeRepo = new ApplicationTypeRepository(context);
        var localAppRepo = new LocalDrivingLicenseApplicationRepository(context);
        var licenseClassRepo = new LicenseClassRepository(context);
        var personRepo = new PersonRepository(context);
        var driverRepo = new DriverRepository(context);
        var testApptRepo = new TestAppointmentRepository(context);

        var appService = new ApplicationService(appRepo, appTypeRepo, uow, currentUser, _clock);
        var licenseClassService = new LicenseClassService(licenseClassRepo, uow);
        var localAppService = new LocalDrivingLicenseApplicationService(
            localAppRepo, licenseRepo, uow, appRepo, appTypeRepo, licenseClassService, currentUser, appService, _clock, testApptRepo, personRepo);
        var personService = new PersonService(
            personRepo, uow, new DummyFileStorageService(), new DummyImageProcessingService(), NullLogger<PersonService>.Instance, _clock);
        var driverService = new DriverService(driverRepo, personRepo, uow, currentUser, _clock);
        var testWorkflowService = new TestWorkflowService(testApptRepo, _clock);

        return new LicenseIssuanceService(
            uow,
            licenseRepo,
            localAppService,
            appService,
            driverService,
            personService,
            currentUser,
            licenseClassService,
            testWorkflowService,
            _clock);
    }

    private static async Task AwaitTaskWithCooperativeCancellationAsync(
        Task? task,
        CancellationTokenSource cts,
        TimeSpan timeout,
        List<Exception> cleanupErrors)
    {
        if (task == null) return;

        var timedOut = false;
        using (var timeoutCts = new CancellationTokenSource(timeout))
        {
            try
            {
                await task.WaitAsync(timeoutCts.Token);
                return;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                timedOut = true;
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
                return;
            }
        }

        if (timedOut)
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(new InvalidOperationException("Failed to request cooperative cancellation during cleanup.", ex));
            }

            try
            {
                await task;
            }
            catch (OperationCanceledException oce) when (cts.IsCancellationRequested && oce.CancellationToken == cts.Token)
            {
                // Expected cooperative cancellation triggered by cleanup token
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }
        }
    }

    // =========================================================
    // SEEDING HELPERS
    // =========================================================

    private async Task SeedRequiredMetadataAsync()
    {
        await using var context = _database.CreateContext();

        var testTypes = new[]
        {
            new TestType { TestTypeId = (int)TestTypeEnum.Theory, TestTypeTitle = "Vision Test", TestTypeDescription = "Vision Exam", TestTypeFees = 10m },
            new TestType { TestTypeId = (int)TestTypeEnum.Written, TestTypeTitle = "Written Test", TestTypeDescription = "Written Exam", TestTypeFees = 15m },
            new TestType { TestTypeId = (int)TestTypeEnum.Practical, TestTypeTitle = "Practical Test", TestTypeDescription = "Practical Exam", TestTypeFees = 20m }
        };

        var existingTypeIds = await context.TestTypes.Select(t => t.TestTypeId).ToListAsync();
        var typesToAdd = testTypes.Where(t => !existingTypeIds.Contains(t.TestTypeId)).ToList();
        if (typesToAdd.Count > 0)
        {
            await context.Database.OpenConnectionAsync();
            try
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[TestTypes] ON;");
                context.TestTypes.AddRange(typesToAdd);
                await context.SaveChangesAsync();
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[TestTypes] OFF;");
                await context.Database.CloseConnectionAsync();
            }
        }

        var appTypes = new[]
        {
            new ApplicationType { ApplicationTypeId = 1, ApplicationTypeTitle = "New Local Driving License Service", ApplicationFees = 15m },
            new ApplicationType { ApplicationTypeId = 7, ApplicationTypeTitle = "Retake Test", ApplicationFees = 5m }
        };

        var existingAppTypeIds = await context.ApplicationTypes.Select(a => a.ApplicationTypeId).ToListAsync();
        var appTypesToAdd = appTypes.Where(a => !existingAppTypeIds.Contains(a.ApplicationTypeId)).ToList();
        if (appTypesToAdd.Count > 0)
        {
            await context.Database.OpenConnectionAsync();
            try
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[ApplicationTypes] ON;");
                context.ApplicationTypes.AddRange(appTypesToAdd);
                await context.SaveChangesAsync();
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[ApplicationTypes] OFF;");
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<(int PersonId, int UserId, int ApplicationId, int LocalAppId)> SeedApplicationAndExaminationsAsync(string suffix)
    {
        await using var context = _database.CreateContext();

        var country = new Country { CountryName = "Country-" + suffix };
        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var person = new Person
        {
            NationalNo = Random.Shared.NextInt64(1000000000, 9999999999).ToString(),
            FirstName = "Candidate",
            SecondName = "Test",
            LastName = "Exam",
            DateOfBirth = new DateTime(1995, 5, 10),
            Gender = 0,
            Address = "Amman, Jordan",
            Phone = "0795556677",
            NationalityCountryID = country.CountryId
        };
        context.People.Add(person);
        await context.SaveChangesAsync();

        var user = new User
        {
            PersonId = person.PersonId,
            UserName = $"staff_{suffix}",
            Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = true,
            Role = UserRole.Staff,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var licenseClass = new LicenseClass
        {
            ClassName = $"Class_{suffix}",
            ClassDescription = "Standard Test Class",
            MinimumAllowedAge = 18,
            DefaultValidityLength = 10,
            ClassFees = 20m
        };
        context.LicenseClasses.Add(licenseClass);
        await context.SaveChangesAsync();

        var application = new ApplicationD
        {
            ApplicantPersonID = person.PersonId,
            CreatedByUserID = user.UserId,
            ApplicationTypeID = 1,
            ApplicationDate = DateTime.UtcNow,
            ApplicationStatus = AppStatus.New,
            LastStatusDate = DateTime.UtcNow,
            PaidFees = 15m
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var localApp = new LocalDrivingLicenseApplication
        {
            ApplicationID = application.ApplicationID,
            LicenseClassID = licenseClass.LicenseClassID
        };
        context.LocalDrivingLicenseApplications.Add(localApp);
        await context.SaveChangesAsync();

        foreach (var testTypeId in new[] { (int)TestTypeEnum.Theory, (int)TestTypeEnum.Written, (int)TestTypeEnum.Practical })
        {
            var appt = new TestAppointment
            {
                LocalDrivingLicenseApplicationID = localApp.LocalDrivingLicenseApplicationID,
                TestTypeID = testTypeId,
                AppointmentDate = DateTime.UtcNow,
                PaidFees = 10m,
                CreatedByUserID = user.UserId,
                IsLocked = true
            };
            context.TestAppointments.Add(appt);
            await context.SaveChangesAsync();

            var test = new Test
            {
                TestAppointmentID = appt.TestAppointmentID,
                TestResult = true,
                Notes = "Passed",
                CreatedByUserID = user.UserId
            };
            context.Tests.Add(test);
            await context.SaveChangesAsync();
        }

        return (person.PersonId, user.UserId, application.ApplicationID, localApp.LocalDrivingLicenseApplicationID);
    }

    // =========================================================
    // TEST DECORATORS & FAKES
    // =========================================================

    private sealed class PausingApplicationRepositoryDecorator : IApplicationRepository
    {
        private readonly IApplicationRepository _inner;
        private readonly DVLDDbContext _context;
        private readonly int? _expectedSpid;
        private readonly Func<CancellationToken, Task>? _onAfterGetForIssuance;
        private readonly Func<CancellationToken, Task>? _onAfterGetForCancellation;

        public PausingApplicationRepositoryDecorator(
            IApplicationRepository inner,
            DVLDDbContext context,
            int? expectedSpid,
            Func<CancellationToken, Task>? onAfterGetForIssuance,
            Func<CancellationToken, Task>? onAfterGetForCancellation)
        {
            _inner = inner;
            _context = context;
            _expectedSpid = expectedSpid;
            _onAfterGetForIssuance = onAfterGetForIssuance;
            _onAfterGetForCancellation = onAfterGetForCancellation;
        }

        private void VerifySpid()
        {
            if (_expectedSpid.HasValue)
            {
                var conn = (SqlConnection)_context.Database.GetDbConnection();
                if (conn.ServerProcessId != _expectedSpid.Value)
                {
                    throw new InvalidOperationException(
                        $"Transaction SPID mismatch! Expected {_expectedSpid.Value}, but current transaction is executing on SPID {conn.ServerProcessId}.");
                }
            }
        }

        public async Task<ApplicationD?> GetApplicationForIssuanceAsync(int id, CancellationToken cancellationToken = default)
        {
            VerifySpid();
            var result = await _inner.GetApplicationForIssuanceAsync(id, cancellationToken);
            VerifySpid();
            if (_onAfterGetForIssuance != null)
            {
                await _onAfterGetForIssuance(cancellationToken);
            }
            return result;
        }

        public async Task<ApplicationD?> GetApplicationForCancellationAsync(int id, CancellationToken cancellationToken = default)
        {
            VerifySpid();
            var result = await _inner.GetApplicationForCancellationAsync(id, cancellationToken);
            VerifySpid();
            if (_onAfterGetForCancellation != null)
            {
                await _onAfterGetForCancellation(cancellationToken);
            }
            return result;
        }

        public Task<ApplicationD?> GetApplicationForUpdateAsync(int id, CancellationToken cancellationToken = default) => _inner.GetApplicationForUpdateAsync(id, cancellationToken);
        public Task<ApplicationD?> GetApplicationByIdAsync(int id, CancellationToken cancellationToken = default) => _inner.GetApplicationByIdAsync(id, cancellationToken);
        public Task<List<ApplicationD>> GetAllApplicationsAsync(CancellationToken cancellationToken = default) => _inner.GetAllApplicationsAsync(cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByPersonIdAsync(int personId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByPersonIdAsync(personId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByApplicationTypeIdAsync(int applicationTypeId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByApplicationTypeIdAsync(applicationTypeId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByUserIdAsync(int userId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByUserIdAsync(userId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByStatusAsync(AppStatus status, CancellationToken cancellationToken = default) => _inner.GetApplicationsByStatusAsync(status, cancellationToken);
        public Task<bool> IsApplicationExistsByIdAsync(int id, CancellationToken cancellationToken = default) => _inner.IsApplicationExistsByIdAsync(id, cancellationToken);
        public Task<bool> IsPersonHasActiveApplicationAsync(int personId, CancellationToken cancellationToken = default) => _inner.IsPersonHasActiveApplicationAsync(personId, cancellationToken);
        public Task<bool> IsPersonHasActiveApplicationOfTypeAsync(int personId, int applicationTypeId, CancellationToken cancellationToken = default) => _inner.IsPersonHasActiveApplicationOfTypeAsync(personId, applicationTypeId, cancellationToken);
        public Task AddNewApplicationAsync(ApplicationD application, CancellationToken cancellationToken = default) => _inner.AddNewApplicationAsync(application, cancellationToken);
        public void DeleteApplication(ApplicationD application) => _inner.DeleteApplication(application);
    }

    private sealed class TestCurrentUser(int userId) : ICurrentUserService
    {
        public int UserId => userId;
        public string Username => "actor" + userId;
        public string FullName => "Test Actor";
        public UserRole Role => UserRole.Staff;
        public string AccessToken => string.Empty;
        public bool IsLoggedIn => true;
        public void SetSession(int uId, string un, string fn, UserRole r, string at) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }

    private sealed class DummyFileStorageService : IFileStorageService
    {
        public Task<string> SaveAsync(Stream contentStream, string fileExtension, string subDirectory = "people", CancellationToken cancellationToken = default) =>
            Task.FromResult("people/dummy.jpg");

        public Task<Stream?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(null);

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class DummyImageProcessingService : IImageProcessingService
    {
        public Result<ProcessedImageResult> ProcessPersonPhoto(Stream inputStream, long length, string? declaredContentType = null) =>
            Result<ProcessedImageResult>.Success(new ProcessedImageResult(new MemoryStream(), "image/jpeg", ".jpg", 100, 100, length));
    }
}
