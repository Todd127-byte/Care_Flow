using System.Data;
using CareFlow.Data;
using CareFlow.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareFlow.Services;

public sealed record BookingConfirmation(int BookingId, string PatientName, DateOnly BookingDate, IReadOnlyList<string> ServiceNames);
public sealed record BookingResult(bool Succeeded, BookingConfirmation? Confirmation, string? ErrorMessage)
{
    public static BookingResult Success(BookingConfirmation confirmation) => new(true, confirmation, null);
    public static BookingResult Failure(string message) => new(false, null, message);
}

public sealed record BookingSummary(int BookingId, string PatientName, DateOnly BookingDate, int ServiceCount);

public sealed class BookingService(
    IDbContextFactory<CareFlowDbContext> contextFactory,
    ILogger<BookingService> logger)
{
    public async Task<BookingResult> CreateBookingAsync(
        int patientId,
        DateOnly bookingDate,
        IReadOnlyCollection<int> serviceIds,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
            return BookingResult.Failure("Please select a patient.");

        if (bookingDate < DateOnly.FromDateTime(DateTime.Now))
            return BookingResult.Failure("Booking date cannot be earlier than today.");

        if (serviceIds is null || serviceIds.Count == 0)
            return BookingResult.Failure("Please select at least one diagnostic service.");

        if (serviceIds.Count > 3)
            return BookingResult.Failure("You can select a maximum of three services.");

        if (serviceIds.Distinct().Count() != serviceIds.Count)
            return BookingResult.Failure("Duplicate diagnostic services are not allowed.");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var patient = await context.Patients.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PatientId == patientId, cancellationToken);
        if (patient is null)
            return BookingResult.Failure("The selected patient could not be found.");

        SqliteTransaction? sqliteTransaction = null;
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? efTransaction = null;
        try
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = (SqliteConnection)context.Database.GetDbConnection();

            // An immediate (non-deferred) SQLite transaction takes the writer lock before
            // the capacity read, preventing two confirmations from consuming the last slot.
            sqliteTransaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
            var activeTransaction = await context.Database.UseTransactionAsync(sqliteTransaction, cancellationToken)
                ?? throw new InvalidOperationException("EF Core could not enlist in the SQLite transaction.");
            efTransaction = activeTransaction;

            var services = await context.DiagnosticServices
                .Where(item => serviceIds.Contains(item.ServiceId))
                .OrderBy(item => item.ServiceId)
                .ToListAsync(cancellationToken);
            if (services.Count != serviceIds.Count)
            {
                await activeTransaction.RollbackAsync(cancellationToken);
                return BookingResult.Failure("One or more selected diagnostic services could not be found.");
            }

            var confirmedCounts = await context.BookingDetails
                .Where(detail => serviceIds.Contains(detail.ServiceId) && detail.Booking.BookingDate == bookingDate)
                .GroupBy(detail => detail.ServiceId)
                .Select(group => new { ServiceId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ServiceId, item => item.Count, cancellationToken);

            var unavailable = services.FirstOrDefault(service =>
                confirmedCounts.GetValueOrDefault(service.ServiceId) >= service.DailyCapacity);
            if (unavailable is not null)
            {
                await activeTransaction.RollbackAsync(cancellationToken);
                return BookingResult.Failure($"{unavailable.ServiceName} has reached its daily capacity. One or more selected services are no longer available.");
            }

            var booking = new Booking
            {
                PatientId = patientId,
                BookingDate = bookingDate,
                CreatedAt = DateTime.UtcNow,
                Details = services.Select(service => new BookingDetail { ServiceId = service.ServiceId }).ToList()
            };

            context.Bookings.Add(booking);
            await context.SaveChangesAsync(cancellationToken);
            await activeTransaction.CommitAsync(cancellationToken);

            return BookingResult.Success(new BookingConfirmation(
                booking.BookingId,
                patient.FullName,
                bookingDate,
                services.Select(service => service.ServiceName).ToArray()));
        }
        catch (OperationCanceledException)
        {
            await TryRollbackAsync(efTransaction, sqliteTransaction);
            throw;
        }
        catch (Exception exception)
        {
            await TryRollbackAsync(efTransaction, sqliteTransaction);
            logger.LogError(exception, "Could not create a CareFlow booking for patient {PatientId} on {BookingDate}.", patientId, bookingDate);
            return BookingResult.Failure("Unable to save booking. Please try again.");
        }
        finally
        {
            if (efTransaction is not null)
                await efTransaction.DisposeAsync();
            if (sqliteTransaction is not null)
                await sqliteTransaction.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<BookingSummary>> GetBookingHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Bookings.AsNoTracking()
            .OrderByDescending(booking => booking.BookingDate)
            .ThenByDescending(booking => booking.BookingId)
            .Select(booking => new BookingSummary(
                booking.BookingId,
                booking.Patient.FullName,
                booking.BookingDate,
                booking.Details.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetBookingServiceNamesAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BookingDetails.AsNoTracking()
            .Where(detail => detail.BookingId == bookingId)
            .OrderBy(detail => detail.Service.ServiceName)
            .Select(detail => detail.Service.ServiceName)
            .ToListAsync(cancellationToken);
    }

    private async Task TryRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? efTransaction,
        SqliteTransaction? sqliteTransaction)
    {
        try
        {
            if (efTransaction is not null)
                await efTransaction.RollbackAsync(CancellationToken.None);
            else if (sqliteTransaction is not null)
                await sqliteTransaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The booking transaction could not be rolled back cleanly.");
        }
    }
}
