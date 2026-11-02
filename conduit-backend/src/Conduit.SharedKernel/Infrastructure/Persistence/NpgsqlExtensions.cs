using Npgsql;
using NpgsqlTypes;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public static class NpgsqlExtensions
{
    public static NpgsqlParameter AddParameter(
        this NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        var parameter = command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }
}
