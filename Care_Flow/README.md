# CareFlow – Diagnostic Booking Management

CareFlow is a small desktop application for viewing diagnostic service capacity, creating multi-service bookings for existing patients, and reviewing confirmed booking history.

## Technology

- C# and .NET 10
- Avalonia UI 12.1.2 with XAML and MVVM
- Entity Framework Core 10.0.12 and SQLite
- Microsoft.Extensions.DependencyInjection 10.0.12
- xUnit 2.9.3

## Prerequisites

- .NET 10 SDK
- Windows, macOS, or Linux desktop environment supported by Avalonia
- Internet access for the first NuGet restore unless packages are already cached

## Restore, migrate, and run

From the repository root:

```powershell
dotnet restore CareFlow.sln
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef database update --project src/CareFlow/CareFlow.csproj --startup-project src/CareFlow/CareFlow.csproj
dotnet run --project src/CareFlow/CareFlow.csproj
```

The app also calls `Database.MigrateAsync()` on startup, so a separate migration command is optional for normal use. On first startup, it idempotently adds fictional patients and the four diagnostic services when those tables are empty. No patient registration or service administration screens are included.

The default database is stored under the operating system's local-application-data directory in `CareFlow/careflow.db` (on Windows: `%LOCALAPPDATA%\CareFlow\careflow.db`). To point both the app and EF tooling to a different database, set `CARE_FLOW_DB_PATH` before running either command:

```powershell
$env:CARE_FLOW_DB_PATH = "$PWD\careflow.local.db"
dotnet ef database update --project src/CareFlow/CareFlow.csproj --startup-project src/CareFlow/CareFlow.csproj
dotnet run --project src/CareFlow/CareFlow.csproj
```

The migration files are in `src/CareFlow/Data/Migrations`. The EF design-time factory uses the same database path as the desktop application.

## Build and tests

```powershell
dotnet build CareFlow.sln
dotnet test CareFlow.sln
```

The five automated tests use temporary SQLite files. They cover migration and idempotent sample seeding, multi-service persistence, validation, date and duplicate rejection, stale availability, and rollback after a database trigger rejects a booking detail insert.

## Basic use

1. Open **Dashboard** and choose a date to see service capacities and remaining slots.
2. Open **New booking**, select an existing patient and a date, and choose one to three different available services.
3. Confirm the booking. CareFlow rechecks capacity in the database and displays the booking ID only after the transaction commits.
4. Open **Booking history** and select a confirmed booking to view its services.

## Database and transaction behavior

The database contains `Patients`, `DiagnosticServices`, `Bookings`, and `BookingDetails`. See [the relational design diagram](docs/RELATIONAL_DESIGN.md).

Booking creation validates the patient, local date, service count, and duplicates. It then begins an immediate serializable SQLite transaction before reading persisted capacity. The write lock prevents a competing confirmation from consuming a slot between the availability check and write. The booking and all details are saved together and committed together; any failure rolls the transaction back. SQLite allows one active writer, and CareFlow uses a 30-second command timeout while waiting for a competing writer.

Bookings are permanent. Cancellation, rescheduling, registration, service administration, authentication, billing, and time slots are outside this assignment's scope.

## Verification

See [VERIFICATION.md](VERIFICATION.md) for the six acceptance scenarios, their procedures, and the results recorded from the executed test suite.
