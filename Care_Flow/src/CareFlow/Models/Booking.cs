namespace CareFlow.Models;

public sealed class Booking
{
    public int BookingId { get; set; }
    public int PatientId { get; set; }
    public DateOnly BookingDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public Patient Patient { get; set; } = null!;
    public ICollection<BookingDetail> Details { get; set; } = new List<BookingDetail>();
}
