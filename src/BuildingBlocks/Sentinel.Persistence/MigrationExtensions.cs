using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sentinel.Persistence;

public static class MigrationExtensions
{
    public static async Task MigrateSafelyAsync(this SentinelDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await DatabaseSeeder.SeedAsync(db, cancellationToken).ConfigureAwait(false);
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SentinelDbContext>>();
        try
        {
            await db.MigrateSafelyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            throw;
        }
    }
}
