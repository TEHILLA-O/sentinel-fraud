using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sentinel.Persistence;

public sealed class SentinelDbContextFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    public SentinelDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=sentinel;Username=sentinel;Password=sentinel")
            .Options;
        return new SentinelDbContext(options);
    }
}
