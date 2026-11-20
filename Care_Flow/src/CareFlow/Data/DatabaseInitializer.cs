using CareFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Data;

public sealed class DatabaseInitializer(IDbContextFactory<CareFlowDbContext> contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        if (!await context.Patients.AnyAsync(cancellationToken))
        {
            context.Patients.AddRange(
                new Patient { FullName = "Amelia Reyes", DateOfBirth = new DateOnly(1988, 4, 12) },
                new Patient { FullName = "Benjamin Santos", DateOfBirth = new DateOnly(1975, 9, 3) },
                new Patient { FullName = "Clara Mendoza", DateOfBirth = new DateOnly(1996, 1, 27) },
                new Patient { FullName = "Daniel Cruz", DateOfBirth = new DateOnly(1969, 7, 18) },
                new Patient { FullName = "Elena Garcia", DateOfBirth = new DateOnly(2001, 11, 6) });
        }

        if (!await context.DiagnosticServices.AnyAsync(cancellationToken))
        {
            context.DiagnosticServices.AddRange(
                new DiagnosticService { ServiceName = "Complete Blood Count (CBC)", DailyCapacity = 30, Description = "Routine blood cell count test." },
                new DiagnosticService { ServiceName = "Urinalysis", DailyCapacity = 24, Description = "Routine urine screening test." },
                new DiagnosticService { ServiceName = "Chest X-Ray", DailyCapacity = 16, Description = "Diagnostic chest radiography." },
                new DiagnosticService { ServiceName = "ECG", DailyCapacity = 12, Description = "Resting electrocardiogram." });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
