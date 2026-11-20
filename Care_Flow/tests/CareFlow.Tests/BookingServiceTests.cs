using CareFlow.Data;
using CareFlow.Models;
using CareFlow.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareFlow.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_SavesOneParentAndEverySelectedService()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = database.CreateBookingService();
        var result = await service.CreateBookingAsync(database.PatientId, DateOnly.FromDateTime(DateTime.Today.AddDays(2)), [database.CbcId, database.UrinalysisId]);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Confirmation);
        Assert.Equal(2, result.Confirmation.ServiceNames.Count);

        var reopenedFactory = new TestContextFactory(database.Path);
        await using var context = reopenedFactory.CreateDbContext();
        Assert.Equal(1, await context.Bookings.CountAsync());
        Assert.Equal(2, await context.BookingDetails.CountAsync());

        var availability = await new AvailabilityService(reopenedFactory)
            .GetAvailabilityAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        Assert.Equal(1, availability.Single(item => item.ServiceId == database.CbcId).ConfirmedBookings);
        Assert.Equal(2, availability.Single(item => item.ServiceId == database.CbcId).RemainingCapacity);
    }

    [Fact]
    public async Task CreateBooking_RejectsDuplicateAndInvalidRequestsWithoutWriting()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = database.CreateBookingService();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var duplicate = await service.CreateBookingAsync(database.PatientId, date, [database.CbcId, database.CbcId]);
        var tooMany = await service.CreateBookingAsync(database.PatientId, date, [database.CbcId, database.UrinalysisId, database.EcgId, database.XrayId]);
        var missingPatient = await service.CreateBookingAsync(int.MaxValue, date, [database.CbcId]);
        var pastDate = await service.CreateBookingAsync(database.PatientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), [database.CbcId]);

        Assert.False(duplicate.Succeeded);
        Assert.Contains("Duplicate", duplicate.ErrorMessage);
        Assert.False(tooMany.Succeeded);
        Assert.Contains("maximum", tooMany.ErrorMessage);
        Assert.False(missingPatient.Succeeded);
        Assert.Contains("patient", missingPatient.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(pastDate.Succeeded);
        Assert.Contains("earlier", pastDate.ErrorMessage);

        await using var context = database.Factory.CreateDbContext();
        Assert.Empty(await context.Bookings.ToListAsync());
        Assert.Empty(await context.BookingDetails.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_RechecksAvailabilityAndRejectsStaleSelection()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = database.CreateBookingService();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var availabilityService = new AvailabilityService(database.Factory);

        var displayedAvailability = await availabilityService.GetAvailabilityAsync(date);
        Assert.Equal(1, displayedAvailability.Single(item => item.ServiceId == database.EcgId).RemainingCapacity);

        var firstBooking = await service.CreateBookingAsync(database.PatientId, date, [database.EcgId]);
        var staleConfirmation = await service.CreateBookingAsync(database.PatientId, date, [database.EcgId]);

        Assert.True(firstBooking.Succeeded, firstBooking.ErrorMessage);
        Assert.False(staleConfirmation.Succeeded);
        Assert.Contains("capacity", staleConfirmation.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        await using var context = database.Factory.CreateDbContext();
        Assert.Equal(1, await context.Bookings.CountAsync());
        Assert.Equal(1, await context.BookingDetails.CountAsync());
    }

    [Fact]
    public async Task CreateBooking_RollsBackParentWhenDetailInsertFails()
    {
        using var database = await TestDatabase.CreateAsync();
        await using (var context = database.Factory.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailBookingDetail BEFORE INSERT ON BookingDetails BEGIN SELECT RAISE(FAIL, 'controlled test failure'); END;");
        }

        var result = await database.CreateBookingService().CreateBookingAsync(
            database.PatientId,
            DateOnly.FromDateTime(DateTime.Today.AddDays(4)),
            [database.CbcId, database.UrinalysisId]);

        Assert.False(result.Succeeded);
        Assert.Equal("Unable to save booking. Please try again.", result.ErrorMessage);
        await using var verification = database.Factory.CreateDbContext();
        Assert.Empty(await verification.Bookings.ToListAsync());
        Assert.Empty(await verification.BookingDetails.ToListAsync());
    }

    [Fact]
    public async Task DatabaseInitializer_MigratesAndSeedsSampleInformationIdempotently()
    {
        using var database = new TestDatabase(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"careflow-seed-{Guid.NewGuid():N}.db"));
        var initializer = new DatabaseInitializer(database.Factory);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var context = database.Factory.CreateDbContext();
        Assert.Equal(5, await context.Patients.CountAsync());
        Assert.Equal(4, await context.DiagnosticServices.CountAsync());
        Assert.Empty(await context.Bookings.ToListAsync());
    }

    private sealed class TestDatabase(string path) : IDisposable
    {
        public string Path { get; } = path;
        public TestContextFactory Factory { get; } = new(path);
        public int PatientId { get; private set; }
        public int CbcId { get; private set; }
        public int UrinalysisId { get; private set; }
        public int EcgId { get; private set; }
        public int XrayId { get; private set; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"careflow-{Guid.NewGuid():N}.db"));
            await using var context = database.Factory.CreateDbContext();
            await context.Database.MigrateAsync();
            var patient = new Patient { FullName = "Test Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
            var cbc = new DiagnosticService { ServiceName = "CBC", DailyCapacity = 3 };
            var urinalysis = new DiagnosticService { ServiceName = "Urinalysis", DailyCapacity = 3 };
            var ecg = new DiagnosticService { ServiceName = "ECG", DailyCapacity = 1 };
            var xray = new DiagnosticService { ServiceName = "X-Ray", DailyCapacity = 2 };
            context.AddRange(patient, cbc, urinalysis, ecg, xray);
            await context.SaveChangesAsync();
            database.PatientId = patient.PatientId;
            database.CbcId = cbc.ServiceId;
            database.UrinalysisId = urinalysis.ServiceId;
            database.EcgId = ecg.ServiceId;
            database.XrayId = xray.ServiceId;
            return database;
        }

        public BookingService CreateBookingService() => new(Factory, NullLogger<BookingService>.Instance);

        public void Dispose()
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                try { File.Delete(Path + suffix); }
                catch (IOException) { }
            }
        }
    }

    private sealed class TestContextFactory(string path) : IDbContextFactory<CareFlowDbContext>
    {
        private readonly DbContextOptions<CareFlowDbContext> _options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=10")
            .Options;

        public CareFlowDbContext CreateDbContext() => new(_options);
        public Task<CareFlowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
