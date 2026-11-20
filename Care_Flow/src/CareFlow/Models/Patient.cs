namespace CareFlow.Models;

public sealed class Patient
{
    public int PatientId { get; set; }
    public required string FullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
