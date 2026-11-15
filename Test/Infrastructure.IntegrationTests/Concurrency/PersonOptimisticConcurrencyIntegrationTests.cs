using Application.Common.Results;
using Application.DTOs.PersonDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Migrations;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.IntegrationTests.Concurrency;

/// <summary>
/// Integration tests verifying optimistic concurrency control on SQL Server for Person records.
/// Proves that:
/// 1. SQL Server generates and automatically advances the 8-byte rowversion concurrency token on each insert and update.
/// 2. Stale updates detected via RowVersion cause EF Core to throw DbUpdateConcurrencyException.
/// 3. PersonService handles stale tokens by returning Result.Conflict (HTTP 409) and preserving existing database state.
/// 4. No-op updates with stale RowVersion tokens are detected and rejected with Conflict.
/// 5. Two concurrent competing updates using the same initial RowVersion produce exactly one winner and one conflict.
/// </summary>
public sealed class PersonOptimisticConcurrencyIntegrationTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;
    private readonly ITestOutputHelper _output;
    private readonly IJordanClock _clock = new JordanClock();

    public PersonOptimisticConcurrencyIntegrationTests(SqlServerTestDatabase database, ITestOutputHelper output)
    {
        _database = database;
        _output = output;
    }

    [Fact]
    public void Migration_AddPersonRowVersion_GeneratesCorrectSql_WithoutDefaultConstraint()
    {
        using var context = _database.CreateContext();
        var generator = context.Database.GetService<IMigrationsSqlGenerator>();
        var migration = new AddPersonRowVersion();

        var upCommands = generator.Generate(migration.UpOperations, context.Model);
        Assert.Single(upCommands);
        var upSql = upCommands[0].CommandText;
        _output.WriteLine($"Generated Up SQL:\n{upSql}");
        Assert.Contains("ALTER TABLE [People] ADD [RowVersion] rowversion NOT NULL", upSql);
        Assert.DoesNotContain("DEFAULT", upSql, StringComparison.OrdinalIgnoreCase);

        var downCommands = generator.Generate(migration.DownOperations, context.Model);
        Assert.Single(downCommands);
        var downSql = downCommands[0].CommandText;
        _output.WriteLine($"Generated Down SQL:\n{downSql}");
        Assert.Contains("ALTER TABLE [People] DROP COLUMN [RowVersion]", downSql);
    }

    [Fact]
    public async Task Database_SchemaInspection_VerifiesRowVersionHasNoDefaultConstraint()
    {
        await using var context = _database.CreateContext();
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                c.name AS ColumnName,
                t.name AS DataTypeName,
                c.is_nullable AS IsNullable,
                c.default_object_id AS DefaultObjectId
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.People') AND c.name = 'RowVersion';";

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Column 'RowVersion' must exist in dbo.People.");

        string columnName = reader.GetString(0);
        string typeName = reader.GetString(1);
        bool isNullable = reader.GetBoolean(2);
        int defaultObjectId = reader.GetInt32(3);

        _output.WriteLine($"Schema: Column={columnName}, Type={typeName}, Nullable={isNullable}, DefaultObjectId={defaultObjectId}");

        Assert.Equal("RowVersion", columnName);
        Assert.True(typeName is "rowversion" or "timestamp", $"Expected type rowversion or timestamp, got {typeName}");
        Assert.False(isNullable, "RowVersion column in SQL Server must be NOT NULL.");
        Assert.Equal(0, defaultObjectId);
    }

    [Fact]
    public async Task Database_AutomaticallyGeneratesAndAdvancesRowVersion_OnInsertAndUpdate()
    {
        await using var context = _database.CreateContext();
        var country = await GetOrCreateCountryAsync(context);

        var person = new Person
        {
            NationalNo = "9991112221",
            FirstName = "Test",
            SecondName = "Concurrency",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Address = "Amman Street 1",
            Phone = "0791112221",
            NationalityCountryID = country.CountryId
        };

        context.People.Add(person);
        await context.SaveChangesAsync();

        Assert.NotNull(person.RowVersion);
        Assert.Equal(8, person.RowVersion.Length);
        Assert.False(person.RowVersion.All(b => b == 0), "RowVersion should not be all zeros after insert.");

        var initialRowVersion = (byte[])person.RowVersion.Clone();

        person.Address = "Amman Street 2";
        await context.SaveChangesAsync();

        Assert.NotNull(person.RowVersion);
        Assert.Equal(8, person.RowVersion.Length);
        Assert.False(
            initialRowVersion.SequenceEqual(person.RowVersion),
            "RowVersion must advance after database update.");
    }

    [Fact]
    public async Task Repository_SetOriginalRowVersion_ThrowsDbUpdateConcurrencyException_WhenRowVersionStale()
    {
        int personId;
        byte[] staleRowVersion;

        await using (var seedContext = _database.CreateContext())
        {
            var country = await GetOrCreateCountryAsync(seedContext);
            var person = new Person
            {
                NationalNo = "9992223331",
                FirstName = "Stale",
                SecondName = "Test",
                LastName = "Person",
                DateOfBirth = new DateTime(1992, 2, 2),
                Gender = Gender.Male,
                Address = "Initial Address",
                Phone = "0792223331",
                NationalityCountryID = country.CountryId
            };
            seedContext.People.Add(person);
            await seedContext.SaveChangesAsync();
            personId = person.PersonId;
            staleRowVersion = (byte[])person.RowVersion.Clone();
        }

        // Context B performs an update first, advancing the database rowversion
        await using (var contextB = _database.CreateContext())
        {
            var personB = await contextB.People.FindAsync(personId);
            Assert.NotNull(personB);
            personB.Phone = "0789998888";
            await contextB.SaveChangesAsync();
        }

        // Context A attempts to update using staleRowVersion
        await using (var contextA = _database.CreateContext())
        {
            var repoA = new PersonRepository(contextA);
            var personA = await repoA.GetPersonForUpdateAsync(personId);
            Assert.NotNull(personA);

            // Set the original RowVersion to the stale value
            repoA.SetOriginalRowVersion(personA, staleRowVersion);

            personA.Address = "Attempted Stale Address Update";

            var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => contextA.SaveChangesAsync());

            _output.WriteLine($"Expected concurrency exception caught: {exception.Message}");
        }

        // Verify that Context B's update remains intact and Context A's update was not saved
        await using (var verifyContext = _database.CreateContext())
        {
            var currentPerson = await verifyContext.People.FindAsync(personId);
            Assert.NotNull(currentPerson);
            Assert.Equal("0789998888", currentPerson.Phone);
            Assert.Equal("Initial Address", currentPerson.Address);
        }
    }

    [Fact]
    public async Task PersonService_UpdatePersonAsync_StaleRowVersion_ReturnsConflictAndPreventsOverwriting()
    {
        int personId;
        string initialRowVersionBase64;

        await using (var seedContext = _database.CreateContext())
        {
            var country = await GetOrCreateCountryAsync(seedContext);
            var person = new Person
            {
                NationalNo = "9993334441",
                FirstName = "Service",
                SecondName = "Concurrency",
                LastName = "User",
                DateOfBirth = new DateTime(1991, 3, 3),
                Gender = Gender.Male,
                Address = "Original Address",
                Phone = "0793334441",
                NationalityCountryID = country.CountryId
            };
            seedContext.People.Add(person);
            await seedContext.SaveChangesAsync();
            personId = person.PersonId;
            initialRowVersionBase64 = Convert.ToBase64String(person.RowVersion);
        }

        // User 2 successfully updates phone number with current rowversion
        await using (var context2 = _database.CreateContext())
        {
            var service2 = CreatePersonService(context2);
            var updateDto2 = new PersonUpdateDto
            {
                NationalNo = "9993334441",
                FirstName = "Service",
                SecondName = "Concurrency",
                LastName = "User",
                DateOfBirth = new DateTime(1991, 3, 3),
                Gender = (int)Gender.Male,
                Address = "Original Address",
                Phone = "0770001111", // updated
                NationalityCountryID = 1,
                RowVersion = initialRowVersionBase64
            };

            var result2 = await service2.UpdatePersonAsync(personId, updateDto2);
            Assert.True(result2.IsSuccess);
        }

        // User 1 attempts to update address using the initial (now stale) rowversion
        await using (var context1 = _database.CreateContext())
        {
            var service1 = CreatePersonService(context1);
            var updateDto1 = new PersonUpdateDto
            {
                NationalNo = "9993334441",
                FirstName = "Service",
                SecondName = "Concurrency",
                LastName = "User",
                DateOfBirth = new DateTime(1991, 3, 3),
                Gender = (int)Gender.Male,
                Address = "Stale Address From User 1", // attempted update
                Phone = "0793334441",
                NationalityCountryID = 1,
                RowVersion = initialRowVersionBase64 // Stale!
            };

            var result1 = await service1.UpdatePersonAsync(personId, updateDto1);

            Assert.False(result1.IsSuccess);
            Assert.Equal(ErrorType.Conflict, result1.ErrorType);
            Assert.Equal(
                "The person record was modified by another user. Please reload and try again.",
                result1.Error);
        }

        // Verify database: User 2's phone was preserved, User 1's address was rejected
        await using (var verifyContext = _database.CreateContext())
        {
            var personInDb = await verifyContext.People.FindAsync(personId);
            Assert.NotNull(personInDb);
            Assert.Equal("0770001111", personInDb.Phone);
            Assert.Equal("Original Address", personInDb.Address);
        }
    }

    [Fact]
    public async Task PersonService_UpdatePersonAsync_NoOpWithStaleRowVersion_ReturnsConflict()
    {
        int personId;
        string initialRowVersionBase64;

        await using (var seedContext = _database.CreateContext())
        {
            var country = await GetOrCreateCountryAsync(seedContext);
            var person = new Person
            {
                NationalNo = "9994445551",
                FirstName = "NoOp",
                SecondName = "Concurrency",
                LastName = "User",
                DateOfBirth = new DateTime(1989, 4, 4),
                Gender = Gender.Male,
                Address = "Amman Street A",
                Phone = "0794445551",
                NationalityCountryID = country.CountryId
            };
            seedContext.People.Add(person);
            await seedContext.SaveChangesAsync();
            personId = person.PersonId;
            initialRowVersionBase64 = Convert.ToBase64String(person.RowVersion);
        }

        // Advance database rowversion by updating phone in context B
        await using (var contextB = _database.CreateContext())
        {
            var p = await contextB.People.FindAsync(personId);
            Assert.NotNull(p);
            p.Phone = "0785556666";
            await contextB.SaveChangesAsync();
        }

        // Now attempt no-op update with identical data to current DB but stale rowversion
        await using (var contextA = _database.CreateContext())
        {
            var service = CreatePersonService(contextA);
            var noOpDto = new PersonUpdateDto
            {
                NationalNo = "9994445551",
                FirstName = "NoOp",
                SecondName = "Concurrency",
                LastName = "User",
                DateOfBirth = new DateTime(1989, 4, 4),
                Gender = (int)Gender.Male,
                Address = "Amman Street A",
                Phone = "0785556666", // matches DB
                NationalityCountryID = 1,
                RowVersion = initialRowVersionBase64 // Stale!
            };

            var result = await service.UpdatePersonAsync(personId, noOpDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Equal(
                "The person record was modified by another user. Please reload and try again.",
                result.Error);
        }
    }

    [Fact]
    public async Task PersonService_UpdatePersonAsync_NoOpWithCurrentRowVersion_ReturnsSuccess()
    {
        int personId;
        string currentRowVersionBase64;

        await using (var seedContext = _database.CreateContext())
        {
            var country = await GetOrCreateCountryAsync(seedContext);
            var person = new Person
            {
                NationalNo = "9995556661",
                FirstName = "Current",
                SecondName = "NoOp",
                LastName = "User",
                DateOfBirth = new DateTime(1988, 5, 5),
                Gender = Gender.Male,
                Address = "Amman Street B",
                Phone = "0795556661",
                NationalityCountryID = country.CountryId
            };
            seedContext.People.Add(person);
            await seedContext.SaveChangesAsync();
            personId = person.PersonId;
            currentRowVersionBase64 = Convert.ToBase64String(person.RowVersion);
        }

        await using (var context = _database.CreateContext())
        {
            var service = CreatePersonService(context);
            var noOpDto = new PersonUpdateDto
            {
                NationalNo = "9995556661",
                FirstName = "Current",
                SecondName = "NoOp",
                LastName = "User",
                DateOfBirth = new DateTime(1988, 5, 5),
                Gender = (int)Gender.Male,
                Address = "Amman Street B",
                Phone = "0795556661",
                NationalityCountryID = 1,
                RowVersion = currentRowVersionBase64 // Current!
            };

            var result = await service.UpdatePersonAsync(personId, noOpDto);

            Assert.True(result.IsSuccess);
        }
    }

    [Fact]
    public async Task PersonService_ConcurrentCompetingUpdates_ExactlyOneSucceedsAndOneFails()
    {
        int personId;
        string initialRowVersionBase64;

        await using (var seedContext = _database.CreateContext())
        {
            var country = await GetOrCreateCountryAsync(seedContext);
            var person = new Person
            {
                NationalNo = "9996667771",
                FirstName = "Race",
                SecondName = "Condition",
                LastName = "User",
                DateOfBirth = new DateTime(1994, 6, 6),
                Gender = Gender.Male,
                Address = "Race Address Initial",
                Phone = "0796667771",
                NationalityCountryID = country.CountryId
            };
            seedContext.People.Add(person);
            await seedContext.SaveChangesAsync();
            personId = person.PersonId;
            initialRowVersionBase64 = Convert.ToBase64String(person.RowVersion);
        }

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            await using var context = _database.CreateContext();
            var service = CreatePersonService(context);
            var dto = new PersonUpdateDto
            {
                NationalNo = "9996667771",
                FirstName = "Race",
                SecondName = "Condition",
                LastName = "User",
                DateOfBirth = new DateTime(1994, 6, 6),
                Gender = (int)Gender.Male,
                Address = "Updated by Task 1",
                Phone = "0796667771",
                NationalityCountryID = 1,
                RowVersion = initialRowVersionBase64
            };

            barrier.SignalAndWait();
            return await service.UpdatePersonAsync(personId, dto);
        });

        var task2 = Task.Run(async () =>
        {
            await using var context = _database.CreateContext();
            var service = CreatePersonService(context);
            var dto = new PersonUpdateDto
            {
                NationalNo = "9996667771",
                FirstName = "Race",
                SecondName = "Condition",
                LastName = "User",
                DateOfBirth = new DateTime(1994, 6, 6),
                Gender = (int)Gender.Male,
                Address = "Updated by Task 2",
                Phone = "0796667771",
                NationalityCountryID = 1,
                RowVersion = initialRowVersionBase64
            };

            barrier.SignalAndWait();
            return await service.UpdatePersonAsync(personId, dto);
        });

        var results = await Task.WhenAll(task1, task2);

        int successes = results.Count(r => r.IsSuccess);
        int conflicts = results.Count(r => !r.IsSuccess && r.ErrorType == ErrorType.Conflict);

        _output.WriteLine($"Race results: Successes={successes}, Conflicts={conflicts}");

        Assert.Equal(1, successes);
        Assert.Equal(1, conflicts);
    }

    private PersonService CreatePersonService(DVLDDbContext context)
    {
        var repo = new PersonRepository(context);
        var uow = new UnitOfWork(context);
        return new PersonService(
            repo,
            uow,
            new DummyFileStorageService(),
            new DummyImageProcessingService(),
            NullLogger<PersonService>.Instance,
            _clock);
    }

    private static async Task<Country> GetOrCreateCountryAsync(DVLDDbContext context)
    {
        var country = await context.Countries.FirstOrDefaultAsync();
        if (country is not null)
            return country;

        country = new Country { CountryName = "Jordan" };
        context.Countries.Add(country);
        await context.SaveChangesAsync();
        return country;
    }

    private sealed class DummyFileStorageService : IFileStorageService
    {
        public Task<string> SaveAsync(Stream stream, string extension, string folder = "people", CancellationToken cancellationToken = default)
            => Task.FromResult($"{folder}/test{extension}");

        public Task<Stream?> GetAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class DummyImageProcessingService : IImageProcessingService
    {
        public Result<ProcessedImageResult> ProcessPersonPhoto(Stream stream, long length, string? contentType)
            => Result<ProcessedImageResult>.Success(new ProcessedImageResult(new MemoryStream([1, 2, 3]), "image/webp", ".webp", 100, 100, 3));
    }
}
