using Npgsql;

namespace JobProcessor.Worker.Infrastructure.Database;

/// <summary>
/// Thin factory that builds configured <see cref="NpgsqlDataSource"/> instances.
/// Using a data source (instead of bare connection strings) enables connection pooling
/// and prepared-statement caching at the driver level.
/// </summary>
internal static class NpgsqlConnectionFactory
{
    /// <summary>
    /// Creates and opens a new <see cref="NpgsqlDataSource"/> from <paramref name="connectionString"/>.
    /// The caller is responsible for disposing the data source when the application shuts down.
    /// </summary>
    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        // Map the custom enum so Npgsql can read/write it without manual casting.
        // (The DB column is TEXT here, so enum mapping is handled in the repository.)
        return builder.Build();
    }
}
