using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vole_Papillon_Damour.Infrastructure.Persistence;

public sealed class ProjectDbContextFactory : IDesignTimeDbContextFactory<ProjectDbContext>
{
    public ProjectDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PROJECT_DATABASE")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=vole-papillon-damour-db;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ProjectDbContext(options);
    }
}
