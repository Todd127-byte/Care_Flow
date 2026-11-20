namespace CareFlow.Models;

public sealed class DiagnosticService
{
    public int ServiceId { get; set; }
    public required string ServiceName { get; set; }
    public int DailyCapacity { get; set; }
    public string? Description { get; set; }
    public ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
}
