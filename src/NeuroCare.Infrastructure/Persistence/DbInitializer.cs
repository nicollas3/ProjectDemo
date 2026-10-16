using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public static class DbInitializer
{
    /// <summary>
    /// Cria/migra o banco e (opcionalmente) popula dados fictícios. Repete algumas vezes
    /// para tolerar o SQL Server ainda iniciando (docker-compose).
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NeuroCareDbContext>();

                if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
                else await db.Database.EnsureCreatedAsync(ct); // sem migrations geradas ainda (ver README)

                await EnsureRolesAsync(scope.ServiceProvider);
                if (config.GetValue<bool>("Seed:Enabled"))
                    await DemoSeeder.SeedAsync(scope.ServiceProvider, config, logger, ct);
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Banco ainda indisponível (tentativa {Attempt}/10). Nova tentativa em 5s.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private static async Task EnsureRolesAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
    }
}
