# CareFlow verification checklist

Acceptance scenarios are verified with `dotnet test CareFlow.sln`. The manual desktop review is not represented as executed unless a procedure was actually performed.

## Execution summary

- `dotnet build CareFlow.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test CareFlow.sln --no-build --no-restore`: passed, 5 tests, 0 failures.
- `dotnet ef database update --project src/CareFlow/CareFlow.csproj --startup-project src/CareFlow/CareFlow.csproj`: passed against a temporary SQLite file; migration `20261120135620_InitialCreate` applied.
- Desktop review: passed for booking confirmation and history persistence after closing and reopening CareFlow with the same configured database path.

| Scenario | Test procedure | Expected result | Actual result | Status |
|---|---|---|---|---|
| AC-01 | View the dashboard for November 20, 2026 after confirming a booking; compare counts against the history view. | Confirmed counts and remaining capacity reflect persisted booking details for that date. | After restart, dashboard showed 1 confirmed booking, CBC 1/30 with 29 remaining, and urinalysis 1/24 with 23 remaining. | Pass |
| AC-02 | In the desktop UI, book CBC and urinalysis for Amelia Reyes, then inspect confirmation and history. | One booking is confirmed with both service details. | UI showed booking #1 and both services; automated test also verified one parent row and two detail rows from a fresh context. | Pass |
| AC-03 | Submit duplicate IDs, four services, an unknown patient, and a past date in the automated test. | Each request is rejected with a useful validation error and no booking rows. | All four invalid requests were rejected; booking and detail tables remained empty. | Pass |
| AC-04 | Read an available service, confirm into its last slot, then submit again using the previously displayed selection. | Confirmation rechecks persisted capacity and rejects the stale request without overbooking. | Second confirmation was rejected after the one-slot ECG service filled; only the first booking and detail remained. | Pass |
| AC-05 | Install a temporary SQLite trigger that rejects detail insertion after booking insertion begins, then attempt a multi-service booking. | The booking transaction rolls back; neither parent nor details remain. | The operation returned an error and both tables were empty after rollback. | Pass |
| AC-06 | Confirm a booking in the desktop UI, close CareFlow, relaunch with the same `CARE_FLOW_DB_PATH`, and inspect dashboard and history. | The confirmed booking and associated services remain available after restart. | Relaunched app showed 1 confirmed booking; history displayed booking #1 with CBC and urinalysis. | Pass |

## Manual desktop check

Desktop review executed: dashboard, patient picker, service selection, confirmation feedback, navigation, booking history details, app close/restart, and persisted dashboard counts were exercised. The Clear form button and changing the date picker to a different date were not manually exercised; service date filtering is covered at the service layer by the automated tests.
