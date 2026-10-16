using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

/// <summary>Usuário "de sistema" (sem organização): usado por migrations e seed.</summary>
public class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public Guid? OrganizationId => null;
    public string? IpAddress => null;
    public bool IsInRole(string role) => false;
}

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NeuroCareDbContext>
{
    public NeuroCareDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("NEUROCARE_CONNECTION")
                 ?? "Server=(localdb)\\MSSQLLocalDB;Database=NeuroCareDev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<NeuroCareDbContext>().UseSqlServer(cs).Options;
        return new NeuroCareDbContext(options, new SystemCurrentUser());
    }
}
