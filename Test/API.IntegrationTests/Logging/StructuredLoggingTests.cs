using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using API.IntegrationTests.Infrastructure;

using Application.Common.Exceptions;
using Application.Common.Results;
using Application.DTOs.CountryDTO;
using Application.DTOs.PersonDTO;

using DVLD.Api.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace API.IntegrationTests.Logging;

public sealed class StructuredLoggingTests
{
    private sealed class InMemoryLogSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private static HttpClient CreateAuthenticatedClient(
        ApiWebApplicationFactory factory,
        InMemoryLogSink sink,
        Action<IWebHostBuilder>? configureHost = null)
    {
        var webFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILogEventSink>(sink);
            });

            configureHost?.Invoke(builder);
        });

        var client = webFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "1");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    [Fact]
    public async Task Api_StartsAndServesRequests_WhenSeqIsNotConfigured()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([new() { CountryId = 1, CountryName = "Jordan" }]));

        using var client = CreateAuthenticatedClient(
            factory,
            sink,
            b => b.UseSetting("Seq:ServerUrl", ""));

        var response = await client.GetAsync("/api/Countries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
    }

    [Fact]
    public async Task Api_StartsAndServesRequests_WhenSeqUrlIsInvalidOrUnreachable()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([]));

        using var clientInvalid = CreateAuthenticatedClient(
            factory,
            sink,
            b => b.UseSetting("Seq:ServerUrl", "not-a-valid-url"));

        var invalidResponse = await clientInvalid.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);

        using var clientUnreachable = CreateAuthenticatedClient(
            factory,
            sink,
            b => b.UseSetting("Seq:ServerUrl", "http://127.0.0.1:59999"));

        var unreachableResponse = await clientUnreachable.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.OK, unreachableResponse.StatusCode);
    }

    [Fact]
    public async Task RequestLogging_EmitsSingleStructuredEvent_WithCorrelationAndRoute()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([]));

        using var client = CreateAuthenticatedClient(factory, sink);

        var response = await client.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var requestEvents = sink.Events
            .Where(e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate)
            .ToList();

        var completionEvent = Assert.Single(requestEvents);
        Assert.Equal(LogEventLevel.Information, completionEvent.Level);
        Assert.True(completionEvent.Properties.ContainsKey("TraceId"));
        Assert.True(completionEvent.Properties.ContainsKey("RequestId"));
        Assert.True(completionEvent.Properties.ContainsKey("StatusCode"));
        Assert.True(completionEvent.Properties.ContainsKey("Elapsed"));
        Assert.Equal("\"DVLD\"", completionEvent.Properties["Application"].ToString());
        Assert.Equal("\"Testing\"", completionEvent.Properties["Environment"].ToString());
        Assert.Equal("\"/api/Countries\"", completionEvent.Properties["Route"].ToString());
    }

    [Fact]
    public async Task RequestLogging_Expected4xx_LogsAtInformation_NotWarningOrError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        var webFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));

        using var client = webFactory.CreateClient();
        var response = await client.GetAsync("/api/Countries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var completionEvent = Assert.Single(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);

        Assert.Equal(LogEventLevel.Information, completionEvent.Level);
        Assert.Equal("401", completionEvent.Properties["StatusCode"].ToString());
    }

    [Fact]
    public async Task RouteWithNationalNo_LogsSafeRouteTemplate_NeverRawNationalNumber()
    {
        const string syntheticNationalNo = "SYNTH-NAT-99887766";

        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.PersonServiceMock
            .Setup(x => x.GetPersonByNationalNoAsync(syntheticNationalNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PersonDto>.FromNotFound("Person not found."));

        using var client = CreateAuthenticatedClient(factory, sink);

        var response = await client.GetAsync($"/api/People/national/{syntheticNationalNo}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.NotEmpty(sink.Events);
        foreach (var logEvent in sink.Events)
        {
            Assert.DoesNotContain(syntheticNationalNo, logEvent.RenderMessage(), StringComparison.Ordinal);
            foreach (var prop in logEvent.Properties.Values)
            {
                Assert.DoesNotContain(syntheticNationalNo, prop.ToString(), StringComparison.Ordinal);
            }
        }

        var completionEvent = Assert.Single(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
        Assert.Contains("{nationalNo}", completionEvent.Properties["Route"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRequest_NeverLogsSyntheticPasswordBearerOrCookie()
    {
        const string syntheticPassword = "Synthetic!Password#2026";
        const string syntheticBearer = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.synthetic.payload";
        const string syntheticCookie = "synthetic-refresh-cookie-value-98765";

        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        var webFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));

        using var client = webFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/login")
        {
            Content = JsonContent.Create(new
            {
                userName = "synthetic.user",
                password = syntheticPassword
            })
        };
        request.Headers.Add("Authorization", $"Bearer {syntheticBearer}");
        request.Headers.Add("Cookie", $"dvld_rt={syntheticCookie}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.NotEmpty(sink.Events);
        foreach (var logEvent in sink.Events)
        {
            var rendered = logEvent.RenderMessage();
            Assert.DoesNotContain(syntheticPassword, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(syntheticBearer, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(syntheticCookie, rendered, StringComparison.Ordinal);

            foreach (var prop in logEvent.Properties.Values)
            {
                var propText = prop.ToString();
                Assert.DoesNotContain(syntheticPassword, propText, StringComparison.Ordinal);
                Assert.DoesNotContain(syntheticBearer, propText, StringComparison.Ordinal);
                Assert.DoesNotContain(syntheticCookie, propText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task UnhandledException_LogsOnceAtError_WithoutDuplicatingExceptionOnRequestEvent()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic unhandled failure"));

        using var client = CreateAuthenticatedClient(
            factory,
            sink,
            b => b.UseEnvironment("Production"));

        var response = await client.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Synthetic unhandled failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);

        var exceptionEvents = sink.Events
            .Where(e => e.Exception is not null &&
                        e.Properties.TryGetValue("SourceContext", out var src) &&
                        src.ToString() == "\"DVLD.Api.Security.GlobalExceptionHandler\"")
            .ToList();
        var loggedExceptionEvent = Assert.Single(exceptionEvents);
        Assert.Equal(LogEventLevel.Error, loggedExceptionEvent.Level);
        Assert.Equal("\"/api/Countries\"", loggedExceptionEvent.Properties["Route"].ToString());

        var requestEvent = Assert.Single(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
        Assert.Equal(LogEventLevel.Error, requestEvent.Level);
        Assert.Null(requestEvent.Exception);
    }

    [Fact]
    public async Task ExpectedBusinessConflictException_LogsAtWarning_NotError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Concurrent update conflict."));

        using var client = CreateAuthenticatedClient(factory, sink);

        var response = await client.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var exceptionEvent = Assert.Single(
            sink.Events,
            e => e.Exception is not null &&
                 e.Properties.TryGetValue("SourceContext", out var src) &&
                 src.ToString() == "\"DVLD.Api.Security.GlobalExceptionHandler\"");
        Assert.Equal(LogEventLevel.Warning, exceptionEvent.Level);

        var requestEvent = Assert.Single(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
        Assert.Equal(LogEventLevel.Information, requestEvent.Level);
    }

    [Fact]
    public async Task OptionalRollingFileSink_WritesBoundedLogFile_WhenEnabled()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "dvld-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var logPattern = Path.Combine(tempDir, "dvld-.log");
            await using var factory = new ApiWebApplicationFactory();
            var sink = new InMemoryLogSink();

            factory.CountryServiceMock
                .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<CountryDto>>.Success([]));

            using var client = CreateAuthenticatedClient(
                factory,
                sink,
                b =>
                {
                    b.UseSetting("DiagnosticLogging:File:Enabled", "true");
                    b.UseSetting("DiagnosticLogging:File:Path", logPattern);
                    b.UseSetting("DiagnosticLogging:File:RetainedFileCountLimit", "2");
                    b.UseSetting("DiagnosticLogging:File:FileSizeLimitBytes", "1048576");
                });

            var response = await client.GetAsync("/api/Countries");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var files = Directory.GetFiles(tempDir, "dvld-*.log");
            Assert.NotEmpty(files);
        }
        finally
        {
            Log.CloseAndFlush();
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup in test teardown.
                }
            }
        }
    }

    [Fact]
    public void SensitiveDataRedactingEnricher_RedactsSensitiveProperties_PreservesSafeIdentifiers()
    {
        var sink = new InMemoryLogSink();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(
            "Diagnostic check {Password} {AccessToken} {RefreshToken} {ApiKey} {ConnectionString} {NationalNo} {Phone} {Address} {AccountId} {TokenFamilyId} {RequestId} {TraceId}",
            "synth-pass",
            "synth-jwt",
            "synth-rt",
            "synth-apikey",
            "Server=.;Password=synth;",
            "9990001112",
            "0790000000",
            "123 Main St",
            42,
            "family-guid-123",
            "req-123",
            "trace-123");

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["Password"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["AccessToken"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["RefreshToken"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["ApiKey"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["ConnectionString"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["NationalNo"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["Phone"].ToString());
        Assert.Equal("\"[REDACTED]\"", logEvent.Properties["Address"].ToString());

        Assert.Equal("42", logEvent.Properties["AccountId"].ToString());
        Assert.Equal("\"family-guid-123\"", logEvent.Properties["TokenFamilyId"].ToString());
        Assert.Equal("\"req-123\"", logEvent.Properties["RequestId"].ToString());
        Assert.Equal("\"trace-123\"", logEvent.Properties["TraceId"].ToString());
    }

    [Fact]
    public async Task RouteWithUsername_LogsSafeRouteTemplate_NeverRawUsername()
    {
        const string syntheticUsername = "synth.secret.username";

        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.UserServiceMock
            .Setup(x => x.GetUserByUsernameAsync(syntheticUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Application.DTOs.UserDTO.UserDto>.FromNotFound("User not found."));

        using var client = CreateAuthenticatedClient(factory, sink);

        var response = await client.GetAsync($"/api/Users/username/{syntheticUsername}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.NotEmpty(sink.Events);
        foreach (var logEvent in sink.Events)
        {
            Assert.DoesNotContain(syntheticUsername, logEvent.RenderMessage(), StringComparison.Ordinal);
            foreach (var prop in logEvent.Properties.Values)
            {
                Assert.DoesNotContain(syntheticUsername, prop.ToString(), StringComparison.Ordinal);
            }
        }

        var completionEvent = Assert.Single(
            sink.Events,
            e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
        Assert.Contains("{username}", completionEvent.Properties["Route"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryStringSecrets_AreNotLoggedInRequestCompletionEvents()
    {
        const string syntheticQuerySecret = "synth-query-secret-9911";

        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([]));

        using var client = CreateAuthenticatedClient(factory, sink);

        var response = await client.GetAsync($"/api/Countries?apiKey={syntheticQuerySecret}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        foreach (var logEvent in sink.Events)
        {
            Assert.DoesNotContain(syntheticQuerySecret, logEvent.RenderMessage(), StringComparison.Ordinal);
            foreach (var prop in logEvent.Properties.Values)
            {
                Assert.DoesNotContain(syntheticQuerySecret, prop.ToString(), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void SensitiveDataRedactingEnricher_RedactsNestedDestructuredObjects()
    {
        var sink = new InMemoryLogSink();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var payload = new
        {
            AccountId = 77,
            BearerToken = "synth-nested-bearer",
            UserPassword = "synth-nested-password",
            NationalNo = "9998887776"
        };

        logger.Information("Nested payload {@Payload}", payload);

        var logEvent = Assert.Single(sink.Events);
        var rendered = logEvent.Properties["Payload"].ToString();
        Assert.DoesNotContain("synth-nested-bearer", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("synth-nested-password", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("9998887776", rendered, StringComparison.Ordinal);
        Assert.Contains("77", rendered, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentWebApplicationFactories_IsolateSinksAndDoNotFreezeLogger()
    {
        await using var factory1 = new ApiWebApplicationFactory();
        await using var factory2 = new ApiWebApplicationFactory();
        var sink1 = new InMemoryLogSink();
        var sink2 = new InMemoryLogSink();

        factory1.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([]));
        factory2.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CountryDto>>.Success([]));

        using var client1 = CreateAuthenticatedClient(factory1, sink1);
        using var client2 = CreateAuthenticatedClient(factory2, sink2);

        var responses = await Task.WhenAll(
            client1.GetAsync("/api/Countries"),
            client2.GetAsync("/api/Countries"));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(sink1.Events, e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
        Assert.Single(sink2.Events, e => e.MessageTemplate.Text == LoggingExtensions.DefaultRequestMessageTemplate);
    }

    [Fact]
    public void SensitiveDataRedactingEnricher_RedactsNestedDictionaries_PersonalNames_AndPhotoPaths()
    {
        var sink = new InMemoryLogSink();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var nestedDict = new Dictionary<string, object>
        {
            ["Password"] = "synth-dict-pass",
            ["BearerToken"] = "synth-dict-bearer",
            ["NationalNumber"] = "9991112223",
            ["PhoneNumber"] = "0791112223",
            ["FullName"] = "Synthetic Full Name",
            ["ImagePath"] = "people/synth-photo.webp",
            ["StorageKey"] = "synth-storage-key",
            ["AccountId"] = 99,
            ["Nested"] = new Dictionary<string, object>
            {
                ["RefreshToken"] = "synth-deep-rt",
                ["TokenFamilyId"] = "fam-guid-99"
            }
        };

        logger.Information("Dictionary diagnostic {@Data}", nestedDict);

        var logEvent = Assert.Single(sink.Events);
        var rendered = logEvent.Properties["Data"].ToString();
        Assert.DoesNotContain("synth-dict-pass", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("synth-dict-bearer", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("9991112223", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("0791112223", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Full Name", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("people/synth-photo.webp", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("synth-storage-key", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("synth-deep-rt", rendered, StringComparison.Ordinal);
        Assert.Contains("99", rendered, StringComparison.Ordinal);
        Assert.Contains("fam-guid-99", rendered, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfLog_CapturesInvalidSeqUrl_WithoutExposingSecrets()
    {
        const string syntheticApiKey = "synth-seq-apikey-secret-9988";
        var originalError = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);

        try
        {
            LoggingExtensions.EnsureSelfLogEnabled();

            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seq:ServerUrl"] = "not-a-valid-url",
                    ["Seq:ApiKey"] = syntheticApiKey
                })
                .Build();

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(x => x.EnvironmentName).Returns("Testing");

            var loggerConfig = new LoggerConfiguration();
            LoggingExtensions.ConfigureDvldSerilog(loggerConfig, config, env.Object);

            var selfLogOutput = sw.ToString();
            Assert.Contains("Invalid Seq:ServerUrl", selfLogOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(syntheticApiKey, selfLogOutput, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task GlobalExceptionHandler_ScrubsSyntheticSecretsInExceptionMessage_PreservesDiagnostics()
    {
        const string syntheticDbPassword = "SyntheticDbPassword!99";
        const string syntheticBearer = "eyJsynthetic.bearer.secret";

        await using var factory = new ApiWebApplicationFactory();
        var sink = new InMemoryLogSink();

        factory.CountryServiceMock
            .Setup(x => x.GetAllCountriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"Failure connecting: Password={syntheticDbPassword}; Header=Bearer {syntheticBearer}"));

        using var client = CreateAuthenticatedClient(
            factory,
            sink,
            b => b.UseEnvironment("Production"));

        var response = await client.GetAsync("/api/Countries");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var loggedExceptionEvent = Assert.Single(
            sink.Events,
            e => e.Exception is not null &&
                 e.Properties.TryGetValue("SourceContext", out var src) &&
                 src.ToString() == "\"DVLD.Api.Security.GlobalExceptionHandler\"");

        var exceptionText = loggedExceptionEvent.Exception!.ToString();
        Assert.DoesNotContain(syntheticDbPassword, exceptionText, StringComparison.Ordinal);
        Assert.DoesNotContain(syntheticBearer, exceptionText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", exceptionText, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", exceptionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthService_FailedLogin_DoesNotLogAccountKnownOrUsername()
    {
        const string syntheticUsername = "synth.private.user";
        var sink = new InMemoryLogSink();
        using var serilogLogger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using Microsoft.Extensions.Logging.ILoggerFactory loggerFactory =
            new Serilog.Extensions.Logging.SerilogLoggerFactory(serilogLogger);
        var logger = loggerFactory.CreateLogger<Application.Services.AuthService>();

        var userRepo = new Mock<Application.Interfaces.IUserRepository>();
        userRepo.Setup(x => x.GetUserByUsernameAsync(syntheticUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.User?)null);

        var authService = new Application.Services.AuthService(
            userRepo.Object,
            Mock.Of<Application.Interfaces.IUserSessionRepository>(),
            Mock.Of<Application.Interfaces.IJwtTokenService>(),
            Mock.Of<Application.Interfaces.IRefreshTokenService>(),
            Mock.Of<Application.Interfaces.IUnitOfWork>(),
            Mock.Of<Application.Interfaces.IJordanClock>(),
            Microsoft.Extensions.Options.Options.Create(new Application.Options.AuthSessionOptions()),
            logger);

        var result = await authService.LoginAsync(new Application.DTOs.UserDTO.LoginRequestDto
        {
            UserName = syntheticUsername,
            Password = "WrongPassword!123"
        });

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, logEvent.Level);
        Assert.False(logEvent.Properties.ContainsKey("AccountKnown"));
        Assert.False(logEvent.Properties.ContainsKey("UserName"));
        Assert.DoesNotContain(syntheticUsername, logEvent.RenderMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestingEnvironment_DoesNotInheritDevelopmentSeqUrl()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        Assert.Equal("Testing", env.EnvironmentName);
        Assert.True(string.IsNullOrWhiteSpace(config["Seq:ServerUrl"]));
    }
}
