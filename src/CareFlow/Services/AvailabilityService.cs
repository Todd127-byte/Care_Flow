using CareFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public sealed record ServiceAvailability(
    int ServiceId,
    string ServiceName,
    string? Description,
    int DailyCapacity,
    int ConfirmedBookings,
    int RemainingCapacity)
{
    public string Status => RemainingCapacity == 0
        ? "Fully booked"
        : RemainingCapacity <= 2 ? "Limited availability" : "Available";
}

public sealed record DepartmentSummary(int PatientCount, int ServiceCount, int BookingCount);

public sealed class AvailabilityService(IDbContextFactory<CareFlowDbContext> contextFactory)
{
    public async Task<IReadOnlyList<ServiceAvailability>> GetAvailabilityAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var services = await context.DiagnosticServices
            .AsNoTracking()
            .OrderBy(service => service.ServiceName)
            .ToListAsync(cancellationToken);

        var bookingCounts = await context.BookingDetails
            .AsNoTracking()
            .Where(detail => detail.Booking.BookingDate == date)
            .GroupBy(detail => detail.ServiceId)
            .Select(group => new { ServiceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ServiceId, row => row.Count, cancellationToken);

        return services.Select(service =>
        {
            var confirmed = bookingCounts.GetValueOrDefault(service.ServiceId);
            return new ServiceAvailability(
                service.ServiceId,
                service.ServiceName,
                service.Description,
                service.DailyCapacity,
                confirmed,
                Math.Max(0, service.DailyCapacity - confirmed));
        }).ToArray();
    }

    public async Task<DepartmentSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new DepartmentSummary(
            await context.Patients.CountAsync(cancellationToken),
            await context.DiagnosticServices.CountAsync(cancellationToken),
            await context.Bookings.CountAsync(cancellationToken));
    }
}
