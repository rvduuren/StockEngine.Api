namespace StockEngine.Api.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// When true, applies EF Core migrations on startup outside Development.
    /// Development always migrates (and seeds) regardless of this flag.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; init; }
}