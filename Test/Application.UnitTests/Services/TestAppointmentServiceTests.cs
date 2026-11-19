using Application.Common.Results;
using Application.DTOs.ApplicationDTO;
using Application.DTOs.LocalDrivingLicenseApplicationDTO;
using Application.DTOs.TestAppointmentDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;

namespace Application.UnitTests.Services;

public sealed partial class TestAppointmentServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUnitOfWorkTransaction> _transaction = new();

    private readonly Mock<ITestAppointmentRepository> _repository = new();
    private readonly Mock<ITestTypeRepository> _testTypeRepository = new();

    private readonly Mock<ILocalDrivingLicenseApplicationService>
        _localApplicationService = new();

    private readonly Mock<IApplicationTypeService>
        _applicationTypeService = new();

    private readonly Mock<IApplicationService>
        _applicationService = new();

    private readonly Mock<ICurrentUserService>
        _currentUserService = new();

    private readonly Mock<ITestWorkflowService>
        _workflowService = new();

    private readonly Mock<ILogger<TestAppointmentService>>
        _logger = new();

    private readonly Common.FakeJordanClock _clock = new();

    // =========================================================
    // FACTORY
    // =========================================================

    private TestAppointmentService CreateService()
    {
        _unitOfWork
            .Setup(x =>
                x.ExecuteInTransactionAsync(
                    It.IsAny<Func<IUnitOfWorkTransaction, Task<Result>>>(),
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<CancellationToken>()))
            .Returns(async (
                Func<IUnitOfWorkTransaction, Task<Result>> operation,
                IsolationLevel _,
                CancellationToken __) =>
            {
                try
                {
                    var result = await operation(_transaction.Object);

                    if (result.IsFailure)
                        await _transaction.Object.RollbackAsync();

                    return result;
                }
                catch
                {
                    await _transaction.Object.RollbackAsync();
                    throw;
                }
            });

        _unitOfWork
            .Setup(x =>
                x.ExecuteInTransactionAsync(
                    It.IsAny<Func<IUnitOfWorkTransaction, Task<Result<int>>>>(),
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<CancellationToken>()))
            .Returns(async (
                Func<IUnitOfWorkTransaction, Task<Result<int>>> operation,
                IsolationLevel _,
                CancellationToken __) =>
            {
                try
                {
                    var result = await operation(_transaction.Object);

                    if (result.IsFailure)
                        await _transaction.Object.RollbackAsync();

                    return result;
                }
                catch
                {
                    await _transaction.Object.RollbackAsync();
                    throw;
                }
            });

        _unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _transaction
            .Setup(x =>
                x.CommitAsync(
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transaction
            .Setup(x =>
                x.RollbackAsync(
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _currentUserService
            .SetupGet(x => x.IsLoggedIn)
            .Returns(true);

        _currentUserService
            .SetupGet(x => x.UserId)
            .Returns(10);

        _repository
            .Setup(x =>
                x.LockLocalApplicationForSchedulingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new TestAppointmentService(
            _unitOfWork.Object,
            _repository.Object,
            _testTypeRepository.Object,
            _localApplicationService.Object,
            _applicationTypeService.Object,
            _currentUserService.Object,
            _workflowService.Object,
            _applicationService.Object,
            _logger.Object,
            _clock);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static DateTime ValidFutureSlot(int daysAhead = 2, int hour = 8, int minute = 0)
    {
        var date = Common.FakeJordanClock.DefaultJordanNow.Date.AddDays(daysAhead);
        while (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
        {
            date = date.AddDays(1);
        }
        return new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static CreateTestAppointmentDto ValidCreateDto(
        TestTypeEnum type = TestTypeEnum.Theory)
    {
        return new CreateTestAppointmentDto
        {
            TestTypeID = (int)type,
            LocalDrivingLicenseApplicationID = 100,
            AppointmentDate = ValidFutureSlot(2)
        };
    }

    private static UpdateTestAppointmentDto ValidUpdateDto(
        int id = 1)
    {
        return new UpdateTestAppointmentDto
        {
            TestAppointmentID = id,
            AppointmentDate = ValidFutureSlot(5, 10, 0)
        };
    }

    private static TestType ValidTestType(
        TestTypeEnum type = TestTypeEnum.Theory,
        decimal fees = 25m)
    {
        return new TestType
        {
            TestTypeId = (int)type,
            TestTypeTitle = type.ToString(),
            TestTypeDescription = $"{type} test",
            TestTypeFees = fees
        };
    }

    private static TestAppointment ValidAppointment(
        int id = 1,
        int userId = 10,
        bool locked = false,
        DateTime? date = null)
    {
        return new TestAppointment
        {
            TestAppointmentID = id,
            TestTypeID = (int)TestTypeEnum.Theory,
            LocalDrivingLicenseApplicationID = 100,
            AppointmentDate =
                date ?? ValidFutureSlot(4),
            PaidFees = 25m,
            CreatedByUserID = userId,
            IsLocked = locked,
            Status = AppointmentStatus.Scheduled,

            TestType = ValidTestType(),

            User = new User
            {
                UserId = userId,
                UserName = "tester"
            }
        };
    }

    private void SetupBasicAddFlow()
    {
        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.LockLocalApplicationForSchedulingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void SetupBasicScheduleFlow()
    {
        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestType());

        _workflowService
            .Setup(x =>
                x.CanScheduleTestAsync(
                    100,
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _repository
            .Setup(x =>
                x.GetApplicantPersonIdByLocalAppIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _repository
            .Setup(x =>
                x.AcquireSchedulingLocksAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(
                    100,
                    1,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.IsSlotOccupiedAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.HasApplicantSameDayConflictAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _repository
            .Setup(x =>
                x.LockLocalApplicationForSchedulingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [Fact]
    public void Constructor_WhenUnitOfWorkIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                null!,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                null!,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenTestTypeRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                null!,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenLocalApplicationServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                null!,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenApplicationTypeServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                null!,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenCurrentUserServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                null!,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenWorkflowServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                null!,
                _applicationService.Object,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenApplicationServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                null!,
                _logger.Object,
                _clock));
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                null!,
                _clock));
    }

    [Fact]
    public void Constructor_WhenClockIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestAppointmentService(
                _unitOfWork.Object,
                _repository.Object,
                _testTypeRepository.Object,
                _localApplicationService.Object,
                _applicationTypeService.Object,
                _currentUserService.Object,
                _workflowService.Object,
                _applicationService.Object,
                _logger.Object,
                null!));
    }

}
