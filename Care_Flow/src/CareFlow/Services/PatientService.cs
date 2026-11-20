using CareFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public sealed record PatientOption(int PatientId, string FullName, DateOnly DateOfBirth);

public sealed class PatientService(IDbContextFactory<CareFlowDbContext> contextFactory)
{
    public async Task<IReadOnlyList<PatientOption>> GetPatientsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Patients.AsNoTracking()
            .OrderBy(patient => patient.FullName)
            .Select(patient => new PatientOption(patient.PatientId, patient.FullName, patient.DateOfBirth))
            .ToListAsync(cancellationToken);
    }
}
