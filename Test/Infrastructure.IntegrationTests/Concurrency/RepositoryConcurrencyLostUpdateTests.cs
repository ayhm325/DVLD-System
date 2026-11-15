using System.Runtime.ExceptionServices;
using Application.Common.Results;
using Application.DTOs.ApplicationDTO;
using Application.DTOs.AuthDTO;
using Application.DTOs.PersonDTO;
using Application.DTOs.UserDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.IntegrationTests.Concurrency;

/// <summary>
/// Deterministic SQL concurrency reproduction tests verifying whether read-modify-write workflows
/// in PersonService, ApplicationService, and UserService exhibit Lost Updates and State Overwrites
/// when executed against an isolated SQL Server test database.
/// </summary>
public sealed class RepositoryConcurrencyLostUpdateTests : IClassFixture<SqlServerTestDatabase>
{
    private static readonly IRefreshTokenService TokenService = new RefreshTokenService();
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly SqlServerTestDatabase _database;
    private readonly IJordanClock _clock = new JordanClock();

    public RepositoryConcurrencyLostUpdateTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Scenario A: Person Update with a Stale Client Payload
    /// 
    /// Note on Concurrency Model:
    /// This test reproduces a stale client payload overwrite (the classical web application disconnected
    /// Last-Write-Wins defect), where a client submits a complete DTO based on an earlier read while retaining
    /// unchanged properties, rather than overlapping database transactions on the same DbContext.
    /// It proves that because Person has no concurrency token (rowversion), EF Core's default tracking
    /// executes an unconstrained UPDATE that silently overwrites concurrent field updates.
    /// </summary>
    [Fact]
    public async Task ScenarioA_PersonUpdate_WithStaleClientPayload_SilentlyOverwritesConcurrentPhoneUpdate()
    {
        var nationalNo = Random.Shared.NextInt64(1000000000, 9999999999).ToString();
        var (personId, countryId) = await SeedPersonAsync(nationalNo, "0790000000", "Old Street 100");

        // 1. Client B opens the Person edit form in the web UI and captures the stale state in a DTO
        var staleClientBDto = new PersonUpdateDto
        {
            NationalNo = nationalNo,
            FirstName = "Alice",
            SecondName = "M",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = 0,
            Phone = "0790000000", // Stale original phone retained in Client B's form
            Address = "New Street 200", // Intended change by Client B
            NationalityCountryID = countryId
        };

        // 2. Client A submits an update changing Phone to "0791111111" while keeping Address as "Old Street 100"
        await using (var contextA = _database.CreateContext())
        {
            var serviceA = CreatePersonService(contextA);
            var clientADto = new PersonUpdateDto
            {
                NationalNo = nationalNo,
                FirstName = "Alice",
                SecondName = "M",
                LastName = "Smith",
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = 0,
                Phone = "0791111111", // Valid change by Client A
                Address = "Old Street 100",
                NationalityCountryID = countryId
            };

            var resultA = await serviceA.UpdatePersonAsync(personId, clientADto);
            Assert.True(resultA.IsSuccess, $"{resultA.ErrorType}: {resultA.Error}");
        }

        // 3. Verify that Client A's update was persisted to SQL Server
        await using (var verifyContextA = _database.CreateContext())
        {
            var personAfterA = await verifyContextA.People.AsNoTracking().SingleAsync(p => p.PersonId == personId);
            Assert.Equal("0791111111", personAfterA.Phone);
            Assert.Equal("Old Street 100", personAfterA.Address);
        }

        // 4. Client B submits the previously captured stale DTO (containing "New Street 200" and stale phone "0790000000")
        await using (var contextB = _database.CreateContext())
        {
            var serviceB = CreatePersonService(contextB);
            var resultB = await serviceB.UpdatePersonAsync(personId, staleClientBDto);
            Assert.True(resultB.IsSuccess, $"{resultB.ErrorType}: {resultB.Error}");
        }

        // 5. Verify the final persisted state in SQL Server using a fresh verification context
        await using (var finalVerifyContext = _database.CreateContext())
        {
            var finalPerson = await finalVerifyContext.People.AsNoTracking().SingleAsync(p => p.PersonId == personId);

            // Client B's address change was applied
            Assert.Equal("New Street 200", finalPerson.Address);

            // REPRODUCED LOST UPDATE DEFECT:
            // Client A's phone update ("0791111111") was silently overwritten and reverted back to "0790000000"
            Assert.Equal("0790000000", finalPerson.Phone);
            Assert.NotEqual("0791111111", finalPerson.Phone);
        }
    }

    /// <summary>
    /// Scenario B: Application Cancellation vs. First License Issuance
    /// Proves that when CancelApplicationAsync reads an application as New, pauses, and LicenseIssuanceService
    /// subsequently completes the application and issues a license, resuming CancelApplicationAsync allows it to
    /// overwrite Completed with Cancelled in SQL Server, leaving a live driving license referencing a Cancelled application.
    /// </summary>
    [Fact]
    public async Task ScenarioB_ApplicationCancellation_ConcurrentWithLicenseIssuance_OverwritesCompletedStatus()
    {
        await SeedRequiredMetadataAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (personId, userId, applicationId, localAppId) = await SeedApplicationAndExaminationsAsync(suffix);

        var cancelReadCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCancelToSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancelCts = new CancellationTokenSource();
        var cancelContext = _database.CreateContext();
        Task<Result>? cancelTask = null;
        Exception? primaryException = null;

        try
        {
            // 1. Start Cancellation request on Context 1, pausing immediately after its repository read
            var cancelService = CreatePausingCancelService(cancelContext, userId, cancelReadCompleted, allowCancelToSave, DefaultTimeout);

            cancelTask = Task.Run(async () =>
            {
                return await cancelService.CancelApplicationAsync(applicationId, cancelCts.Token);
            });

            // 2. Wait with bounded timeout until Cancellation has read the application as New
            await cancelReadCompleted.Task.WaitAsync(DefaultTimeout);

            // 3. Concurrently execute LicenseIssuanceService on Context 2 to issue the license and complete the application
            await using (var issuanceContext = _database.CreateContext())
            {
                var issuanceService = CreateLicenseIssuanceService(issuanceContext, userId);
                var issuanceResult = await issuanceService.IssueFirstLicenseAsync(localAppId, "Issued first time");
                Assert.True(issuanceResult.IsSuccess, $"{issuanceResult.ErrorType}: {issuanceResult.Error}");
            }

            // 4. Verify in SQL Server that the application is now Completed and the license exists and is active
            await using (var midVerifyContext = _database.CreateContext())
            {
                var midApp = await midVerifyContext.Applications.AsNoTracking().SingleAsync(a => a.ApplicationID == applicationId);
                Assert.Equal(AppStatus.Completed, midApp.ApplicationStatus);

                var midLicense = await midVerifyContext.Licenses.AsNoTracking().SingleOrDefaultAsync(l => l.ApplicationID == applicationId);
                Assert.NotNull(midLicense);
                Assert.True(midLicense.IsActive, "The issued license should be active.");
            }

            // 5. Resume Cancellation and let it attempt to save with bounded timeout
            allowCancelToSave.TrySetResult(true);
            var cancelResult = await cancelTask.WaitAsync(DefaultTimeout);

            // Cancellation returned success because its in-memory status check happened before issuance
            Assert.True(cancelResult.IsSuccess, $"{cancelResult.ErrorType}: {cancelResult.Error}");

            // 6. Verify final database state in SQL Server using a fresh verification context
            await using (var finalVerifyContext = _database.CreateContext())
            {
                var finalApp = await finalVerifyContext.Applications.AsNoTracking().SingleAsync(a => a.ApplicationID == applicationId);
                var finalLicense = await finalVerifyContext.Licenses.AsNoTracking().SingleOrDefaultAsync(l => l.ApplicationID == applicationId);

                // A live driving license was issued and committed
                Assert.NotNull(finalLicense);
                Assert.True(finalLicense.IsActive, "The issued license should remain active.");

                // REPRODUCED STATE OVERWRITE DEFECT:
                // CancelApplicationAsync overwrote the Completed status with Cancelled in SQL Server!
                Assert.Equal(AppStatus.Cancelled, finalApp.ApplicationStatus);
                Assert.NotEqual(AppStatus.Completed, finalApp.ApplicationStatus);
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

            // Unblock allowCancelToSave so the workflow is not suspended on the TaskCompletionSource
            allowCancelToSave.TrySetResult(true);

            // Await actual completion of cancelTask with cooperative cancellation before disposing cancelContext
            await AwaitTaskWithCooperativeCancellationAsync(cancelTask, cancelCts, DefaultTimeout, cleanupErrors);

            // Dispose cancelContext only after cancelTask has completely terminated
            try
            {
                await cancelContext.DisposeAsync();
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
            }

            // Preserve exceptions without masking primary test failure
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
    /// Scenario C: Concurrent Password Changes
    /// Proves that two concurrent ChangePasswordAsync requests both read the original password hash,
    /// both verify the old password, both return Success, but the second request silently overwrites
    /// the first request's password change in SQL Server (Lost Update).
    /// </summary>
    [Fact]
    public async Task ScenarioC_ConcurrentPasswordChanges_BothSucceedAndSilentlyOverwritesFirstChange()
    {
        const string oldPassword = "OriginalPassword123!";
        const string newPassword1 = "NewPasswordOne1!";
        const string newPassword2 = "NewPasswordTwo2!";

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, initialSecurityStamp) = await SeedUserWithSessionsAsync($"user_{suffix}", oldPassword);

        var req1ReadDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var req2ReadDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var req1VerifiedDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var req2VerifiedDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReq1ToSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReq2ToSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var context1 = _database.CreateContext();
        var context2 = _database.CreateContext();
        Task<Result>? task1 = null;
        Task<Result>? task2 = null;
        Exception? primaryException = null;

        try
        {
            var service1 = CreateSynchronizedUserService(
                context1,
                onAfterRead: async ct =>
                {
                    req1ReadDone.TrySetResult(true);
                    await req2ReadDone.Task.WaitAsync(DefaultTimeout, ct);
                },
                onBeforeRevokeAndSave: async ct =>
                {
                    // Reaching RevokeAllForUserAsync proves BCrypt password verification succeeded
                    req1VerifiedDone.TrySetResult(true);
                    await req2VerifiedDone.Task.WaitAsync(DefaultTimeout, ct);
                    await allowReq1ToSave.Task.WaitAsync(DefaultTimeout, ct);
                });

            var service2 = CreateSynchronizedUserService(
                context2,
                onAfterRead: async ct =>
                {
                    await req1ReadDone.Task.WaitAsync(DefaultTimeout, ct);
                    req2ReadDone.TrySetResult(true);
                },
                onBeforeRevokeAndSave: async ct =>
                {
                    // Reaching RevokeAllForUserAsync proves BCrypt password verification succeeded
                    req2VerifiedDone.TrySetResult(true);
                    await allowReq2ToSave.Task.WaitAsync(DefaultTimeout, ct);
                });

            // 1. Run both password change requests concurrently
            task1 = Task.Run(async () =>
            {
                return await service1.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = oldPassword,
                    NewPassword = newPassword1
                }, cts1.Token);
            });

            task2 = Task.Run(async () =>
            {
                return await service2.ChangePasswordAsync(userId, new ChangePasswordDto
                {
                    CurrentPassword = oldPassword,
                    NewPassword = newPassword2
                }, cts2.Token);
            });

            // 2. Wait until both requests have completed their initial read and BCrypt password verification
            await Task.WhenAll(
                req1ReadDone.Task.WaitAsync(DefaultTimeout),
                req2ReadDone.Task.WaitAsync(DefaultTimeout));

            await Task.WhenAll(
                req1VerifiedDone.Task.WaitAsync(DefaultTimeout),
                req2VerifiedDone.Task.WaitAsync(DefaultTimeout));

            // 3. Allow Request 1 to persist and commit its transaction first
            allowReq1ToSave.TrySetResult(true);
            var result1 = await task1.WaitAsync(DefaultTimeout);
            Assert.True(result1.IsSuccess, $"{result1.ErrorType}: {result1.Error}");

            // 4. Now allow Request 2 to persist and commit its transaction
            allowReq2ToSave.TrySetResult(true);
            var result2 = await task2.WaitAsync(DefaultTimeout);
            Assert.True(result2.IsSuccess, $"{result2.ErrorType}: {result2.Error}");

            // 5. Verify the final persisted state in SQL Server using a fresh verification context
            await using (var verifyContext = _database.CreateContext())
            {
                var user = await verifyContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

                // Both service calls reported Success
                Assert.True(result1.IsSuccess);
                Assert.True(result2.IsSuccess);

                // SecurityStamp was rotated
                Assert.NotEqual(initialSecurityStamp, user.SecurityStamp);
                Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));

                // All user sessions were revoked
                var sessions = await verifyContext.UserSessions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
                Assert.Equal(2, sessions.Count);
                Assert.All(sessions, s =>
                {
                    Assert.NotNull(s.RevokedAtUtc);
                    Assert.Equal(SessionRevocationReasons.PasswordChanged, s.RevocationReason);
                });

                // REPRODUCED LOST UPDATE DEFECT:
                // NewPassword1 was completely wiped out by NewPassword2
                Assert.False(BCrypt.Net.BCrypt.Verify(newPassword1, user.Password), "NewPassword1 should have been overwritten.");
                Assert.True(BCrypt.Net.BCrypt.Verify(newPassword2, user.Password), "NewPassword2 should be the active password.");
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

            // 1. Unblock all completion sources so background tasks are not left waiting
            req1ReadDone.TrySetResult(true);
            req2ReadDone.TrySetResult(true);
            req1VerifiedDone.TrySetResult(true);
            req2VerifiedDone.TrySetResult(true);
            allowReq1ToSave.TrySetResult(true);
            allowReq2ToSave.TrySetResult(true);

            // 2. Await task1 actual completion with cooperative cancellation
            await AwaitTaskWithCooperativeCancellationAsync(task1, cts1, DefaultTimeout, cleanupErrors);

            // 3. Await task2 actual completion with cooperative cancellation
            await AwaitTaskWithCooperativeCancellationAsync(task2, cts2, DefaultTimeout, cleanupErrors);

            // 4. Safe disposal of contexts only after tasks have completely terminated
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

            // 5. Preserve exceptions without masking primary test failure
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
    // SERVICE FACTORIES & TEST DECORATORS
    // =========================================================

    private PersonService CreatePersonService(DVLDDbContext context)
    {
        return new PersonService(
            new PersonRepository(context),
            new UnitOfWork(context),
            new DummyFileStorageService(),
            new DummyImageProcessingService(),
            NullLogger<PersonService>.Instance,
            _clock);
    }

    private ApplicationService CreatePausingCancelService(
        DVLDDbContext context,
        int userId,
        TaskCompletionSource<bool> readCompleted,
        TaskCompletionSource<bool> allowProceed,
        TimeSpan timeout)
    {
        var innerRepo = new ApplicationRepository(context);
        var pausingRepo = new PausingApplicationRepositoryDecorator(innerRepo, readCompleted, allowProceed, timeout);
        var uow = new UnitOfWork(context);
        var appTypeRepo = new ApplicationTypeRepository(context);
        var currentUser = new TestCurrentUser(userId);

        return new ApplicationService(pausingRepo, appTypeRepo, uow, currentUser, _clock);
    }

    private LicenseIssuanceService CreateLicenseIssuanceService(DVLDDbContext context, int userId)
    {
        var currentUser = new TestCurrentUser(userId);
        var uow = new UnitOfWork(context);
        var licenseRepo = new LicenseRepository(context);
        var appRepo = new ApplicationRepository(context);
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
        var personService = new PersonService(personRepo, uow, new DummyFileStorageService(), new DummyImageProcessingService(), NullLogger<PersonService>.Instance, _clock);
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
            NullLogger<LicenseIssuanceService>.Instance,
            _clock);
    }

    private UserService CreateSynchronizedUserService(
        DVLDDbContext context,
        Func<CancellationToken, Task> onAfterRead,
        Func<CancellationToken, Task> onBeforeRevokeAndSave)
    {
        var innerUserRepo = new UserRepository(context);
        var pausingUserRepo = new PausingUserRepositoryDecorator(innerUserRepo, onAfterRead);
        var innerSessionRepo = new UserSessionRepository(context);
        var pausingSessionRepo = new PausingUserSessionRepositoryDecorator(innerSessionRepo, onBeforeRevokeAndSave);
        var uow = new UnitOfWork(context);

        return new UserService(pausingUserRepo, uow, pausingSessionRepo, _clock);
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

    private async Task<(int PersonId, int CountryId)> SeedPersonAsync(string nationalNo, string phone, string address)
    {
        await using var context = _database.CreateContext();

        var country = new Country { CountryName = "Country-" + nationalNo };
        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var person = new Person
        {
            NationalNo = nationalNo,
            FirstName = "Alice",
            SecondName = "M",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = 0,
            Address = address,
            Phone = phone,
            NationalityCountryID = country.CountryId
        };
        context.People.Add(person);
        await context.SaveChangesAsync();

        return (person.PersonId, country.CountryId);
    }

    private async Task<(int UserId, string SecurityStamp)> SeedUserWithSessionsAsync(string userName, string password)
    {
        await using var context = _database.CreateContext();
        var nationalNo = Random.Shared.NextInt64(1000000000, 9999999999).ToString();
        var (personId, _) = await SeedPersonAsync(nationalNo, "0790000000", "Amman");

        var stamp = Guid.NewGuid().ToString("N");
        var user = new User
        {
            PersonId = personId,
            UserName = userName,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
            Role = UserRole.Staff,
            SecurityStamp = stamp
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var session1 = NewSession(user.UserId, TokenService.CreateToken());
        var session2 = NewSession(user.UserId, TokenService.CreateToken());
        context.UserSessions.AddRange(session1, session2);
        await context.SaveChangesAsync();

        return (user.UserId, stamp);
    }

    private static UserSession NewSession(int userId, string rawToken)
    {
        var now = DateTime.UtcNow;
        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = TokenService.HashToken(rawToken),
            TokenFamilyId = Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(14),
            LastUsedAtUtc = now
        };
    }

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

        // Seed passed test appointments and tests for Theory, Written, Practical
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
        private readonly TaskCompletionSource<bool> _readCompleted;
        private readonly TaskCompletionSource<bool> _allowProceed;
        private readonly TimeSpan _timeout;

        public PausingApplicationRepositoryDecorator(
            IApplicationRepository inner,
            TaskCompletionSource<bool> readCompleted,
            TaskCompletionSource<bool> allowProceed,
            TimeSpan timeout)
        {
            _inner = inner;
            _readCompleted = readCompleted;
            _allowProceed = allowProceed;
            _timeout = timeout;
        }

        public async Task<ApplicationD?> GetApplicationForUpdateAsync(int id, CancellationToken cancellationToken = default)
        {
            var result = await _inner.GetApplicationForUpdateAsync(id, cancellationToken);
            _readCompleted.TrySetResult(true);
            await _allowProceed.Task.WaitAsync(_timeout, cancellationToken);
            return result;
        }

        public Task<ApplicationD?> GetApplicationByIdAsync(int id, CancellationToken cancellationToken = default) => _inner.GetApplicationByIdAsync(id, cancellationToken);
        public Task<List<ApplicationD>> GetAllApplicationsAsync(CancellationToken cancellationToken = default) => _inner.GetAllApplicationsAsync(cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByPersonIdAsync(int personId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByPersonIdAsync(personId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByApplicationTypeIdAsync(int applicationTypeId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByApplicationTypeIdAsync(applicationTypeId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByUserIdAsync(int userId, CancellationToken cancellationToken = default) => _inner.GetApplicationsByUserIdAsync(userId, cancellationToken);
        public Task<List<ApplicationD>> GetApplicationsByStatusAsync(AppStatus status, CancellationToken cancellationToken = default) => _inner.GetApplicationsByStatusAsync(status, cancellationToken);
        public Task<ApplicationD?> GetApplicationForIssuanceAsync(int id, CancellationToken cancellationToken = default) => _inner.GetApplicationForIssuanceAsync(id, cancellationToken);
        public Task<ApplicationD?> GetApplicationForCancellationAsync(int id, CancellationToken cancellationToken = default) => _inner.GetApplicationForCancellationAsync(id, cancellationToken);
        public Task<bool> IsApplicationExistsByIdAsync(int id, CancellationToken cancellationToken = default) => _inner.IsApplicationExistsByIdAsync(id, cancellationToken);
        public Task<bool> IsPersonHasActiveApplicationAsync(int personId, CancellationToken cancellationToken = default) => _inner.IsPersonHasActiveApplicationAsync(personId, cancellationToken);
        public Task<bool> IsPersonHasActiveApplicationOfTypeAsync(int personId, int applicationTypeId, CancellationToken cancellationToken = default) => _inner.IsPersonHasActiveApplicationOfTypeAsync(personId, applicationTypeId, cancellationToken);
        public Task AddNewApplicationAsync(ApplicationD application, CancellationToken cancellationToken = default) => _inner.AddNewApplicationAsync(application, cancellationToken);
        public void DeleteApplication(ApplicationD application) => _inner.DeleteApplication(application);
    }

    private sealed class PausingUserRepositoryDecorator : IUserRepository
    {
        private readonly IUserRepository _inner;
        private readonly Func<CancellationToken, Task> _onAfterRead;

        public PausingUserRepositoryDecorator(
            IUserRepository inner,
            Func<CancellationToken, Task> onAfterRead)
        {
            _inner = inner;
            _onAfterRead = onAfterRead;
        }

        public async Task<User?> GetUserForUpdateAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await _inner.GetUserForUpdateAsync(id, cancellationToken);
            if (_onAfterRead != null) await _onAfterRead(cancellationToken);
            return user;
        }

        public Task<User?> GetUserForPasswordChangeAsync(int id, CancellationToken cancellationToken = default) => _inner.GetUserForPasswordChangeAsync(id, cancellationToken);

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
        public Task<int> GetActiveAdminCountAsync(CancellationToken cancellationToken = default) => _inner.GetActiveAdminCountAsync(cancellationToken);
        public Task<bool> AcquireAdminMutationLockAsync(CancellationToken cancellationToken = default) => _inner.AcquireAdminMutationLockAsync(cancellationToken);
        public Task AddUserAsync(User user, CancellationToken cancellationToken = default) => _inner.AddUserAsync(user, cancellationToken);
        public void DeleteUser(User user) => _inner.DeleteUser(user);
    }

    private sealed class PausingUserSessionRepositoryDecorator : IUserSessionRepository
    {
        private readonly IUserSessionRepository _inner;
        private readonly Func<CancellationToken, Task> _onBeforeRevokeAndSave;

        public PausingUserSessionRepositoryDecorator(
            IUserSessionRepository inner,
            Func<CancellationToken, Task> onBeforeRevokeAndSave)
        {
            _inner = inner;
            _onBeforeRevokeAndSave = onBeforeRevokeAndSave;
        }

        public async Task<int> RevokeAllForUserAsync(int userId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default)
        {
            if (_onBeforeRevokeAndSave != null)
            {
                await _onBeforeRevokeAndSave(cancellationToken);
            }
            return await _inner.RevokeAllForUserAsync(userId, nowUtc, reason, cancellationToken);
        }

        public Task<UserSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => _inner.GetByTokenHashAsync(tokenHash, cancellationToken);
        public Task<bool> TryConsumeAsync(Guid sessionId, DateTime nowUtc, Guid replacedBySessionId, CancellationToken cancellationToken = default) => _inner.TryConsumeAsync(sessionId, nowUtc, replacedBySessionId, cancellationToken);
        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default) => _inner.AddAsync(session, cancellationToken);
        public Task<int> RevokeFamilyAsync(Guid tokenFamilyId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default) => _inner.RevokeFamilyAsync(tokenFamilyId, nowUtc, reason, cancellationToken);
        public Task<bool> HasActiveSessionInFamilyAsync(Guid tokenFamilyId, DateTime nowUtc, CancellationToken cancellationToken = default) => _inner.HasActiveSessionInFamilyAsync(tokenFamilyId, nowUtc, cancellationToken);
        public Task<int> RevokeByTokenHashAsync(string tokenHash, DateTime nowUtc, string reason, CancellationToken cancellationToken = default) => _inner.RevokeByTokenHashAsync(tokenHash, nowUtc, reason, cancellationToken);
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
