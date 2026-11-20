using Avalonia.Media;
using CareFlow.Helpers;
using CareFlow.Services;

namespace CareFlow.ViewModels;

public sealed class ServiceSelectionViewModel(ServiceAvailability availability) : ObservableObject
{
    private bool _isSelected;

    public int ServiceId => availability.ServiceId;
    public string ServiceName => availability.ServiceName;
    public string? Description => availability.Description;
    public int DailyCapacity => availability.DailyCapacity;
    public int ConfirmedBookings => availability.ConfirmedBookings;
    public int RemainingCapacity => availability.RemainingCapacity;
    public string Status => availability.Status;
    public bool IsAvailable => RemainingCapacity > 0;
    public IBrush StatusBrush => RemainingCapacity == 0
        ? Brushes.IndianRed
        : RemainingCapacity <= 2 ? Brushes.DarkOrange : Brushes.SeaGreen;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
