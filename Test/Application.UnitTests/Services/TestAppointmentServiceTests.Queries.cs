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
    // =========================================================
    // GET BY ID
    // =========================================================

    [Fact]
    public async Task GetByIdAsync_WhenIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid test appointment ID.",
            result.Error);

        _repository.Verify(
            x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _repository
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestAppointment?)null);

        var result = await service.GetByIdAsync(99);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Appointment not found.",
            result.Error);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsDto()
    {
        var service = CreateService();

        var entity = ValidAppointment(5);

        _repository
            .Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await service.GetByIdAsync(5);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value!.TestAppointmentID);
        Assert.Equal(
            entity.TestTypeID,
            result.Value.TestTypeID);
        Assert.Equal(
            entity.LocalDrivingLicenseApplicationID,
            result.Value.LocalDrivingLicenseApplicationID);
        Assert.Equal(
            entity.PaidFees,
            result.Value.PaidFees);
        Assert.Equal(
            entity.CreatedByUserID,
            result.Value.CreatedByUserID);
    }

    // =========================================================
    // GET ALL
    // =========================================================

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var service = CreateService();

        _repository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestAppointment>());

        var result = await service.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetAllAsync_WhenAppointmentsExist_ReturnsAll()
    {
        var service = CreateService();

        _repository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                ValidAppointment(1),
                ValidAppointment(2)
            ]);

        var result = await service.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.Count);
    }

    // =========================================================
    // GET BY LOCAL APPLICATION
    // =========================================================

    [Fact]
    public async Task GetByLocalApplicationAsync_WhenIdInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service
                .GetByLocalDrivingLicenseApplicationIdAsync(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task GetByLocalApplicationAsync_WhenValid_ReturnsAppointments()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetByLocalDrivingLicenseApplicationIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                ValidAppointment(1),
                ValidAppointment(2)
            ]);

        var result =
            await service
                .GetByLocalDrivingLicenseApplicationIdAsync(100);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.Count);
    }

    // =========================================================
    // GET BY TEST TYPE
    // =========================================================

    [Fact]
    public async Task GetByTestTypeIdAsync_WhenInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.GetByTestTypeIdAsync(
                (int)999);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid test type.",
            result.Error);
    }

    [Fact]
    public async Task GetByTestTypeIdAsync_WhenValid_ReturnsAppointments()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetByTestTypeIdAsync(
                    TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                ValidAppointment(1)
            ]);

        var result = await service.GetByTestTypeIdAsync((int)TestTypeEnum.Theory);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    // =========================================================
    // GET BY USER
    // =========================================================

    [Fact]
    public async Task GetByCreatedUserIdAsync_WhenInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.GetByCreatedUserIdAsync(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "Invalid user ID.",
            result.Error);
    }

    [Fact]
    public async Task GetByCreatedUserIdAsync_WhenValid_ReturnsAppointments()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetByCreatedUserIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                ValidAppointment(1, 10),
                ValidAppointment(2, 10)
            ]);

        var result =
            await service.GetByCreatedUserIdAsync(10);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    // =========================================================
    // SCHEDULE INFO
    // =========================================================

    [Fact]
    public async Task GetScheduleInfoAsync_WhenInvalid_ReturnsValidation()
    {
        var service = CreateService();

        var result =
            await service.GetScheduleInfoAsync(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task GetScheduleInfoAsync_WhenNotFound_ReturnsNotFound()
    {
        var service = CreateService();

        _repository
            .Setup(x => x.GetScheduleInfoAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestAppointment?)null);

        var result =
            await service.GetScheduleInfoAsync(100);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(
            "Appointment data not found.",
            result.Error);
    }

    [Fact]
    public async Task GetScheduleInfoAsync_WhenFound_ReturnsScheduleDto()
    {
        var service = CreateService();

        var entity = ValidAppointment(5);
        entity.Status = AppointmentStatus.Completed;

        _repository
            .Setup(x => x.GetScheduleInfoAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(
                    100,
                    (int)TestTypeEnum.Theory,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result =
            await service.GetScheduleInfoAsync(5);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value!.AppointmentID);
        Assert.Equal(2, result.Value.Trial);
    }

    // =========================================================
    // TEST TYPE FEES
    // =========================================================

    [Fact]
    public async Task GetTestTypeFeesAsync_WhenInvalid_ReturnsZero()
    {
        var service = CreateService();

        var result =
            await service.GetTestTypeFeesAsync(999);

        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task GetTestTypeFeesAsync_WhenNotFound_ReturnsZero()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestType?)null);

        var result =
            await service.GetTestTypeFeesAsync(1);

        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task GetTestTypeFeesAsync_WhenFound_ReturnsFees()
    {
        var service = CreateService();

        _testTypeRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ValidTestType(
                    TestTypeEnum.Theory,
                    40m));

        var result =
            await service.GetTestTypeFeesAsync(1);

        Assert.Equal(40m, result);
    }

    // =========================================================
    // TRIAL COUNT
    // =========================================================

    [Fact]
    public async Task GetTrialCountAsync_WhenInvalidApplication_ReturnsZero()
    {
        var service = CreateService();

        var result =
            await service.GetTrialCountAsync(
                0,
                1);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetTrialCountAsync_WhenInvalidTestType_ReturnsZero()
    {
        var service = CreateService();

        var result =
            await service.GetTrialCountAsync(
                100,
                999);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetTrialCountAsync_WhenValid_ReturnsRepositoryValue()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result =
            await service.GetTrialCountAsync(100, 1, It.IsAny<CancellationToken>());

        Assert.Equal(3, result);
    }

    // =========================================================
    // ALREADY SCHEDULED
    // =========================================================

    [Fact]
    public async Task IsAppointmentAlreadyScheduledAsync_DelegatesToRepository()
    {
        var service = CreateService();

        _repository
            .Setup(x =>
                x.IsAppointmentAlreadyScheduledAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result =
            await service.IsAppointmentAlreadyScheduledAsync(
                100,
                1);

        Assert.True(result);

        _repository.Verify(
            x =>
                x.IsAppointmentAlreadyScheduledAsync(100, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
