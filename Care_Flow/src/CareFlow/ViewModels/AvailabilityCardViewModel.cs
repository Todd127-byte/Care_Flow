using Avalonia.Media;
using CareFlow.Services;

namespace CareFlow.ViewModels;

public sealed class AvailabilityCardViewModel(ServiceAvailability availability)
{
    public int ServiceId => availability.ServiceId;
    public string ServiceName => availability.ServiceName;
    public string? Description => availability.Description;
    public int DailyCapacity => availability.DailyCapacity;
    public int ConfirmedBookings => availability.ConfirmedBookings;
    public int RemainingCapacity => availability.RemainingCapacity;
    public string Status => availability.Status;
    public IBrush StatusBrush => RemainingCapacity == 0
        ? Brushes.IndianRed
        : RemainingCapacity <= 2 ? Brushes.DarkOrange : Brushes.SeaGreen;
}
