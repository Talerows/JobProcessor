using JobProcessor.Worker.Domain;
using Npgsql;
using NpgsqlTypes;

namespace JobProcessor.Worker.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of <see cref="IJobRepository"/>
/// </summary>
internal sealed class PostgresJobRepository : IJobRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresJobRepository> _logger;

    public PostgresJobRepository(string connectionString, ILogger<PostgresJobRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Order>> ClaimOpenJobsAsync(int batchSize, CancellationToken cancellationToken)
    {

        const string sql = """
            WITH cte AS (
                SELECT id
                FROM   orders
                WHERE  status = 'Open'
                ORDER  BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT  @batchSize
            )
            UPDATE orders
            SET    status     = 'InProgress',
                   started_at = NOW() AT TIME ZONE 'UTC'
            FROM   cte
            WHERE  orders.id = cte.id
            RETURNING orders.id, orders.status, orders.created_at, orders.started_at;
            """;

        var claimed = new List<Order>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        {
            try
            {
                    await using var command = new NpgsqlCommand(sql, connection, transaction);
                    {
                        command.Parameters.AddWithValue("@batchSize", NpgsqlDbType.Integer, batchSize);
                        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                        {
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                claimed.Add(MapJob(reader));
                            }
                        }
                    }
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        return claimed;
    }

    public async Task MarkCompletedAsync(long jobId, DateTime completedAt, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE orders
            SET  status       = 'Completed',
                 finished_at = @finishedAt
            WHERE id = @id;
            """;

        await ExecuteNonQueryAsync(sql, command =>
        {
            command.Parameters.AddWithValue("@id", NpgsqlDbType.Bigint, jobId);
            command.Parameters.AddWithValue("@finishedAt", NpgsqlDbType.TimestampTz, completedAt);
        }, cancellationToken);
    }

    public async Task MarkTimedOutAsync(long jobId, DateTime timedOutAt, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE orders
            SET  status       = 'Timeout',
                 timeout_at = @timedOutAt
            WHERE id = @id;
            """;

        await ExecuteNonQueryAsync(sql, command =>
        {
            command.Parameters.AddWithValue("@id", NpgsqlDbType.Bigint, jobId);
            command.Parameters.AddWithValue("@timedOutAt", NpgsqlDbType.TimestampTz, timedOutAt);
        }, cancellationToken);
    }

    private async Task ExecuteNonQueryAsync(
        string sql,
        Action<NpgsqlCommand> parameterizer,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        parameterizer(command);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            _logger.LogWarning("ExecuteNonQuery affected 0 rows. SQL: {Sql}", sql);
        }
    } 

    private static Order MapJob(NpgsqlDataReader reader) => new()
    {
        Id          = reader.GetInt64(reader.GetOrdinal("id")),
        Status      = Enum.Parse<Domain.OrderStatus>(reader.GetString(reader.GetOrdinal("status"))),
        CreatedAt   = reader.GetDateTime(reader.GetOrdinal("created_at")),
        StartedAt   = reader.IsDBNull(reader.GetOrdinal("started_at"))
                          ? null
                          : reader.GetDateTime(reader.GetOrdinal("started_at")),
    };
}
