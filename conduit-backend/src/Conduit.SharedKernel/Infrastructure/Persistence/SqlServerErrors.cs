using Microsoft.Data.SqlClient;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public static class SqlServerErrors
{
    public const int UniqueIndexViolation = 2601;

    public const int UniqueConstraintViolation = 2627;

    public const int ConstraintConflict = 547;

    public static bool IsUniqueViolation(Exception error) =>
        error is SqlException sql && sql.Errors.Cast<SqlError>().Any(IsUniqueViolationNumber);

    public static bool IsForeignKeyViolation(Exception error) =>
        error is SqlException sql && sql.Errors.Cast<SqlError>().Any(item => item.Number == ConstraintConflict);

    public static bool ViolatesConstraint(Exception error, string constraint) =>
        IsUniqueViolation(error)
        && error.Message.Contains($"'{constraint}'", StringComparison.OrdinalIgnoreCase);

    private static bool IsUniqueViolationNumber(SqlError error) =>
        error.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
