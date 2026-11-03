using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public static class SqlServerExtensions
{
    private const byte TimestampScale = 3;

    private const int InlineTextLimit = 4000;

    public static SqlCommand CreateCommand(
        this SqlConnection connection,
        string sql,
        SqlTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    public static SqlParameter AddParameter(
        this SqlCommand command,
        string name,
        SqlDbType type,
        object? value)
    {
        var parameter = command.Parameters.Add(name.StartsWith('@') ? name : "@" + name, type);
        switch (type)
        {
            case SqlDbType.NVarChar:
                parameter.Size = value is string { Length: > InlineTextLimit } ? -1 : InlineTextLimit;
                break;
            case SqlDbType.DateTimeOffset:
                parameter.Scale = TimestampScale;
                break;
        }

        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    public static SqlParameter AddGuidList(this SqlCommand command, string name, IEnumerable<Guid> ids) =>
        command.AddParameter(name, SqlDbType.NVarChar, JsonSerializer.Serialize(ids.ToArray()));

    public static string GuidListQuery(string parameter) =>
        $"SELECT id FROM OPENJSON({parameter}) WITH (id UNIQUEIDENTIFIER '$')";
}
