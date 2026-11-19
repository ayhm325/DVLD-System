using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed partial class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<PersonService> _logger;
    private readonly IJordanClock _clock;

    public PersonService(
        IPersonRepository personRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IImageProcessingService imageProcessingService,
        ILogger<PersonService> logger,
        IJordanClock clock)
    {
        _personRepository =
            personRepository
            ?? throw new ArgumentNullException(
                nameof(personRepository));

        _unitOfWork =
            unitOfWork
            ?? throw new ArgumentNullException(
                nameof(unitOfWork));

        _fileStorageService =
            fileStorageService
            ?? throw new ArgumentNullException(
                nameof(fileStorageService));

        _imageProcessingService =
            imageProcessingService
            ?? throw new ArgumentNullException(
                nameof(imageProcessingService));

        _logger =
            logger
            ?? throw new ArgumentNullException(
                nameof(logger));

        _clock =
            clock
            ?? throw new ArgumentNullException(
                nameof(clock));
    }
}
