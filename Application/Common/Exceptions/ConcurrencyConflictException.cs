namespace Application.Common.Exceptions;

/// <summary>
/// Domain exception thrown when an optimistic concurrency conflict is detected at the persistence boundary.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified by another user. Please reload and try again.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
