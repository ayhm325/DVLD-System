using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Common.Results;
using Application.DTOs.PersonDTO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.UnitTests.Services;

public partial class PersonServiceTests
{
    private readonly Mock<IPersonRepository> _personRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IImageProcessingService> _imageProcessingServiceMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<PersonService>> _loggerMock;
    private readonly Common.FakeJordanClock _clock = new();
    private readonly PersonService _sut;

    public PersonServiceTests()
    {
        _personRepositoryMock = new Mock<IPersonRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _imageProcessingServiceMock = new Mock<IImageProcessingService>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PersonService>>();

        _sut = new PersonService(
            _personRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _fileStorageServiceMock.Object,
            _imageProcessingServiceMock.Object,
            _loggerMock.Object,
            _clock);
    }

    // Test Data Helpers
    // =========================================================

    private static readonly byte[] DefaultRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string DefaultRowVersionBase64 = Convert.ToBase64String(DefaultRowVersion);

    private static Person CreatePerson(
        int personId = 10,
        string nationalNo = "1234567890")
    {
        return new Person
        {
            PersonId = personId,
            NationalNo = nationalNo,
            FirstName = "Ayhm",
            SecondName = "Mohammed",
            ThirdName = null,
            LastName = "Obeidat",
            DateOfBirth = new DateTime(1993, 1, 1),
            Gender = Gender.Male,
            Address = "Amman",
            Phone = "0791234567",
            Email = "ayhm@example.com",
            NationalityCountryID = 1,
            ImagePath = null,
            RowVersion = (byte[])DefaultRowVersion.Clone()
        };
    }

    private static PersonCreateDto CreateValidCreateDto()
    {
        return new PersonCreateDto
        {
            NationalNo = "1234567890",
            FirstName = "Ayhm",
            SecondName = "Mohammed",
            ThirdName = null,
            LastName = "Obeidat",
            DateOfBirth = new DateTime(1993, 1, 1),
            Gender = (int)Gender.Male,
            Address = "Amman",
            Phone = "0791234567",
            Email = "ayhm@example.com",
            NationalityCountryID = 1,
            ImagePath = null
        };
    }

    private static PersonUpdateDto CreateValidUpdateDto()
    {
        return new PersonUpdateDto
        {
            NationalNo = "9876543210",
            FirstName = "Updated",
            SecondName = "Person",
            ThirdName = "Test",
            LastName = "Obeidat",
            DateOfBirth = new DateTime(1992, 5, 10),
            Gender = (int)Gender.Male,
            Address = "Irbid",
            Phone = "0781234567",
            Email = "updated@example.com",
            NationalityCountryID = 1,
            ImagePath = null,
            RowVersion = DefaultRowVersionBase64
        };
    }

}
