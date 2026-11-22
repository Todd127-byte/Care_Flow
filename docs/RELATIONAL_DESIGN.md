# CareFlow relational design

```mermaid
erDiagram
    PATIENTS ||--o{ BOOKINGS : "places"
    BOOKINGS ||--|{ BOOKING_DETAILS : "contains"
    DIAGNOSTIC_SERVICES ||--o{ BOOKING_DETAILS : "is selected in"

    PATIENTS {
        int PatientId PK
        string FullName
        date DateOfBirth
    }
    BOOKINGS {
        int BookingId PK
        int PatientId FK
        date BookingDate
        datetime CreatedAt
    }
    BOOKING_DETAILS {
        int BookingDetailId PK
        int BookingId FK
        int ServiceId FK
    }
    DIAGNOSTIC_SERVICES {
        int ServiceId PK
        string ServiceName UK
        int DailyCapacity
        string Description
    }
```

`BookingDetails` has a unique index on `(BookingId, ServiceId)` so one service cannot be stored twice on a booking. The booking service validates the required one-to-three detail count. Foreign keys preserve patient, booking, and service references. Remaining daily capacity is calculated by counting `BookingDetails` joined to `Bookings` for the selected date.
