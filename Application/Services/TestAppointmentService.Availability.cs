using Application.Common.Results;
using Application.DTOs.TestAppointmentDTO;
using Application.Validators;
using Domain.Enums;

namespace Application.Services;

public sealed partial class TestAppointmentService
{
    public async Task<Result<AppointmentAvailabilityDto>> GetAvailabilityAsync(
        int localAppId,
        int testTypeId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var validation = TestAppointmentValidator.ValidateAvailabilityRequest(
            localAppId,
            testTypeId,
            startDate,
            endDate);

        if (validation.IsFailure)
            return Result<AppointmentAvailabilityDto>.FromValidationFailure(validation.Error);

        var workflow = await _workflowService.CanScheduleTestAsync(
            localAppId,
            (TestTypeEnum)testTypeId,
            cancellationToken);

        if (workflow.IsFailure)
            return Result<AppointmentAvailabilityDto>.FromResult(workflow);

        if (await _repository.IsAppointmentAlreadyScheduledAsync(
                localAppId,
                testTypeId,
                cancellationToken))
            return Result<AppointmentAvailabilityDto>.FromConflict(
                "An appointment already exists for this test.");

        var applicantResult = await ResolveApplicantPersonIdAsync(
            localAppId,
            cancellationToken);
        if (applicantResult.IsFailure)
            return Result<AppointmentAvailabilityDto>.FromResult(applicantResult);

        var currentJordanTime = _clock.Now;
        var currentJordanDate = _clock.Today.ToDateTime(TimeOnly.MinValue);

        var startFacilityDate = TestAppointmentValidator.ToFacilityTime(startDate).Date;
        var endFacilityDate = TestAppointmentValidator.ToFacilityTime(endDate).Date;

        var minimumBookingDate = currentJordanDate.AddDays(2);
        if (endFacilityDate < minimumBookingDate)
        {
            return Result<AppointmentAvailabilityDto>.Success(new AppointmentAvailabilityDto
            {
                LocalAppId = localAppId,
                TestTypeId = testTypeId,
                AvailableDays = []
            });
        }

        var effectiveStartDate = startFacilityDate < minimumBookingDate ? minimumBookingDate : startFacilityDate;
        var rangeStart = effectiveStartDate;
        var rangeEnd = endFacilityDate.AddDays(1);

        var (occupiedSlots, applicantDates, lastTestDate) = await _repository.GetAvailabilityDataAsync(
            testTypeId,
            applicantResult.Value,
            localAppId,
            rangeStart,
            rangeEnd,
            cancellationToken);

        DateTime? earliestEligibleDate = null;
        if (lastTestDate.HasValue)
        {
            earliestEligibleDate = GetFirstWorkingDayAfter(lastTestDate.Value);
        }

        var availableDays = new List<AppointmentDayAvailabilityDto>();

        for (var date = effectiveStartDate; date <= endFacilityDate; date = date.AddDays(1))
        {
            if (date < minimumBookingDate)
                continue;

            if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
                continue;

            if (earliestEligibleDate.HasValue && date < earliestEligibleDate.Value)
                continue;

            if (applicantDates.Contains(date))
                continue;

            var daySlots = new List<AppointmentSlotDto>();

            foreach (var slotTime in TestAppointmentValidator.ValidSlotTimes)
            {
                var slotDateTime = date.Add(slotTime);

                if (slotDateTime <= currentJordanTime)
                    continue;

                if (occupiedSlots.Contains(slotDateTime))
                    continue;

                daySlots.Add(new AppointmentSlotDto
                {
                    StartTime = slotTime.ToString(@"hh\:mm"),
                    EndTime = (slotTime + TimeSpan.FromMinutes(40)).ToString(@"hh\:mm")
                });
            }

            if (daySlots.Count > 0)
            {
                availableDays.Add(new AppointmentDayAvailabilityDto
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Slots = daySlots
                });
            }
        }

        return Result<AppointmentAvailabilityDto>.Success(new AppointmentAvailabilityDto
        {
            LocalAppId = localAppId,
            TestTypeId = testTypeId,
            AvailableDays = availableDays
        });
    }

    private static DateTime GetFirstWorkingDayAfter(DateTime date)
    {
        var next = date.Date.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
        {
            next = next.AddDays(1);
        }
        return next;
    }
}
