using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
namespace StockEngine.Api.Data.Migrations;

/// <summary>
/// MariaDB-compatible migration history repository. Oracle's <c>MySQLHistoryRepository</c> uses
/// <c>GET_LOCK(..., -1)</c>, which MariaDB rejects and returns NULL; the provider then throws
/// <see cref="InvalidCastException"/> when casting that result to <see cref="long"/>.
/// </summary>
#pragma warning disable EF1001
public sealed class MariaDbMySqlHistoryRepository(HistoryRepositoryDependencies dependencies)
    : HistoryRepository(dependencies)
{
    private const int MigrationLockTimeoutSeconds = 60 * 60 * 24 * 3;

    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    protected override string ExistsSql
    {
        get
        {
            var mapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));
            var builder = new StringBuilder();
            builder.Append("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE ");
            builder
                .Append("TABLE_SCHEMA=")
                .Append(mapping.GenerateSqlLiteral(TableSchema ?? Dependencies.Connection.DbConnection.Database))
                .Append(" AND TABLE_NAME=")
                .Append(mapping.GenerateSqlLiteral(TableName))
                .Append(';');
            return builder.ToString();
        }
    }

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        Dependencies.MigrationsLogger.AcquiringMigrationLock();
        var dbLock = CreateMigrationDatabaseLock();
        try
        {
            EnsureLockAcquired(CreateGetLockCommand().ExecuteScalar(CreateRelationalCommandParameters()));
        }
        catch
        {
            try
            {
                dbLock.Dispose();
            }
            catch
            {
                // ignored
            }

            throw;
        }

        return dbLock;
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(
        CancellationToken cancellationToken = default)
    {
        Dependencies.MigrationsLogger.AcquiringMigrationLock();
        var dbLock = CreateMigrationDatabaseLock();
        try
        {
            var value = await CreateGetLockCommand()
                .ExecuteScalarAsync(CreateRelationalCommandParameters(), cancellationToken)
                .ConfigureAwait(false);
            EnsureLockAcquired(value);
        }
        catch
        {
            try
            {
                await dbLock.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            throw;
        }

        return dbLock;
    }

    private static void EnsureLockAcquired(object? value)
    {
        if (value is null or DBNull)
        {
            throw new TimeoutException("Could not acquire the EF Core migrations database lock.");
        }

        if (Convert.ToInt64(value) < 0)
        {
            throw new TimeoutException("Could not acquire the EF Core migrations database lock.");
        }
    }

    private IRelationalCommand CreateGetLockCommand()
        => Dependencies.RawSqlCommandBuilder
            .Build($"SELECT GET_LOCK('__EFMigrationsLock', {MigrationLockTimeoutSeconds});");

    private MariaDbMySqlMigrationDatabaseLock CreateMigrationDatabaseLock()
    {
        var releaseLockCommand = Dependencies.RawSqlCommandBuilder
            .Build("SELECT RELEASE_LOCK('__EFMigrationsLock');");
        return new MariaDbMySqlMigrationDatabaseLock(releaseLockCommand, CreateRelationalCommandParameters(), this);
    }

    private RelationalCommandParameterObject CreateRelationalCommandParameters()
        => new(
            Dependencies.Connection,
            null,
            null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger,
            CommandSource.Migrations);

    protected override bool InterpretExistsResult(object? value) => value != null;

    public override string GetBeginIfExistsScript(string migrationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(migrationId);
        var mapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));
        return new StringBuilder()
            .Append("IF EXISTS(SELECT * FROM ")
            .Append(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append(" WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ")
            .Append(mapping.GenerateSqlLiteral(migrationId))
            .AppendLine(")")
            .Append("BEGIN")
            .ToString();
    }

    public override string GetBeginIfNotExistsScript(string migrationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(migrationId);
        var mapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));
        return new StringBuilder()
            .Append("IF NOT EXISTS(SELECT * FROM ")
            .Append(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append(" WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ")
            .Append(mapping.GenerateSqlLiteral(migrationId))
            .AppendLine(")")
            .Append("BEGIN")
            .ToString();
    }

    public override string GetCreateIfNotExistsScript()
    {
        var createScript = GetCreateScript();
        return createScript.Insert(
            createScript.IndexOf("CREATE TABLE", StringComparison.Ordinal) + 12,
            " IF NOT EXISTS");
    }

    public override string GetEndIfScript() => "END;" + Environment.NewLine;

    private sealed class MariaDbMySqlMigrationDatabaseLock(
        IRelationalCommand releaseLockCommand,
        RelationalCommandParameterObject relationalCommandParameters,
        IHistoryRepository historyRepository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository => historyRepository;

        public void Dispose() => releaseLockCommand.ExecuteScalar(relationalCommandParameters);

        public async ValueTask DisposeAsync()
        {
            await releaseLockCommand.ExecuteScalarAsync(relationalCommandParameters);
        }
    }
}
#pragma warning restore EF1001