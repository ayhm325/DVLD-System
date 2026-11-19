using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public sealed partial class UnitOfWork
{
    public static void TranslateAndRethrowIfKnownConstraint(DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException concurrencyEx)
        {
            throw new ConcurrencyConflictException(
                "The record was modified by another user. Please reload and try again.",
                concurrencyEx);
        }

        if (IsSpecificConstraintViolation(ex, "IX_Users_UserName"))
        {
            throw new DuplicateUsernameException(
                "Username is already in use.",
                ex);
        }

        if (IsSpecificConstraintViolation(ex, "IX_Users_PersonId"))
        {
            throw new DuplicatePersonUserException(
                "This person is already associated with a user account.",
                ex);
        }
    }

    private static bool IsSpecificConstraintViolation(
        DbUpdateException ex,
        string indexOrConstraintName)
    {
        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            if (sqlEx.Number == 2601 || sqlEx.Number == 2627)
            {
                return sqlEx.Message.Contains(
                    indexOrConstraintName,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
