using CareFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Data;

public sealed class CareFlowDbContext(DbContextOptions<CareFlowDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<DiagnosticService> DiagnosticServices => Set<DiagnosticService>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patients");
            entity.HasKey(patient => patient.PatientId);
            entity.Property(patient => patient.FullName).HasMaxLength(160).IsRequired();
            entity.Property(patient => patient.DateOfBirth).IsRequired();
            entity.HasIndex(patient => patient.FullName);
        });

        modelBuilder.Entity<DiagnosticService>(entity =>
        {
            entity.ToTable("DiagnosticServices", table => table.HasCheckConstraint("CK_DiagnosticServices_DailyCapacity", "DailyCapacity > 0"));
            entity.HasKey(service => service.ServiceId);
            entity.Property(service => service.ServiceName).HasMaxLength(120).IsRequired();
            entity.Property(service => service.Description).HasMaxLength(500);
            entity.HasIndex(service => service.ServiceName).IsUnique();
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings");
            entity.HasKey(booking => booking.BookingId);
            entity.Property(booking => booking.BookingDate).IsRequired();
            entity.Property(booking => booking.CreatedAt).IsRequired();
            entity.HasIndex(booking => booking.BookingDate);
            entity.HasIndex(booking => booking.PatientId);
            entity.HasOne(booking => booking.Patient)
                .WithMany(patient => patient.Bookings)
                .HasForeignKey(booking => booking.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingDetail>(entity =>
        {
            entity.ToTable("BookingDetails");
            entity.HasKey(detail => detail.BookingDetailId);
            entity.HasIndex(detail => new { detail.BookingId, detail.ServiceId }).IsUnique();
            entity.HasIndex(detail => detail.ServiceId);
            entity.HasOne(detail => detail.Booking)
                .WithMany(booking => booking.Details)
                .HasForeignKey(detail => detail.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(detail => detail.Service)
                .WithMany(service => service.BookingDetails)
                .HasForeignKey(detail => detail.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
