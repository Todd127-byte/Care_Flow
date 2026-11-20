using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareFlow.Data;

public sealed class CareFlowDesignTimeFactory : IDesignTimeDbContextFactory<CareFlowDbContext>
{
    public CareFlowDbContext CreateDbContext(string[] args)
    {
        var path = Environment.GetEnvironmentVariable("CARE_FLOW_DB_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CareFlow", "careflow.db");
        }

        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=30")
            .Options;
        return new CareFlowDbContext(options);
    }
}
