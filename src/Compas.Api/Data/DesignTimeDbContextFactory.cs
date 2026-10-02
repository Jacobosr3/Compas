using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Compas.Api.Data;

/// <summary>
/// Permite ejecutar `dotnet ef migrations add ...` / `dotnet ef database update`
/// leyendo appsettings.json directamente, sin depender de Program.cs.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CompasDbContext>
{
    public CompasDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var optionsBuilder = new DbContextOptionsBuilder<CompasDbContext>();

        if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("Postgres"));
        }
        else
        {
            optionsBuilder.UseSqlite(configuration.GetConnectionString("Sqlite"));
        }

        return new CompasDbContext(optionsBuilder.Options);
    }
}
