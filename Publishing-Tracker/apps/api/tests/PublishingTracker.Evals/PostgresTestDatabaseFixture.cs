using Microsoft.EntityFrameworkCore;
using Npgsql;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Evals;

public static class PostgresTestDatabaseFixture
{
    public const string DefaultLocalConnectionString = "Host=localhost;Port=5435;Database=publishingtracker_dev;Username=postgres;Password=postgres";

    public static string GetConnectionStringOrThrow()
    {
        var conn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = DefaultLocalConnectionString;
        }

        try
        {
            EnsureDatabaseCreated(conn);

            using var connection = new NpgsqlConnection(conn);
            connection.Open();
            return conn;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"TEST_POSTGRES_CONNECTION is required for Phase 5 Layer 2 pgvector evaluation tests, but connecting to '{conn}' failed: {ex.Message}. " +
                "Please ensure PostgreSQL with pgvector is running (e.g. docker compose up -d postgres).", ex);
        }
    }

    private static void EnsureDatabaseCreated(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = builder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase) || string.Equals(targetDatabase, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
        adminConn.Open();

        using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{targetDatabase.Replace("'", "''")}';", adminConn);
        var exists = checkCmd.ExecuteScalar() != null;
        if (!exists)
        {
            using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{targetDatabase.Replace("\"", "\"\"")}\";", adminConn);
            createCmd.ExecuteNonQuery();
        }

        // Ensure pgvector extension exists in the target database
        using var targetConn = new NpgsqlConnection(connectionString);
        targetConn.Open();
        using var extCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", targetConn);
        extCmd.ExecuteNonQuery();
    }

    public static PublishingTrackerDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PublishingTrackerDbContext).Assembly.GetName().Name!);
            })
            .Options;

        return new PublishingTrackerDbContext(options);
    }

    public static async Task<PublishingTrackerDbContext> CreateMigratedDbContextAsync(string connectionString)
    {
        var db = CreateDbContext(connectionString);
        await db.Database.MigrateAsync();
        return db;
    }
}
