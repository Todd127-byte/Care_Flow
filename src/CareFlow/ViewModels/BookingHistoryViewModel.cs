using System.Collections.ObjectModel;
using CareFlow.Helpers;
using CareFlow.Services;

namespace CareFlow.ViewModels;

public sealed class BookingHistoryViewModel : ObservableObject
{
    private readonly BookingService _bookingService;
    private BookingSummary? _selectedBooking;
    private bool _isBusy;
    private string? _errorMessage;

    public BookingHistoryViewModel(BookingService bookingService)
    {
        _bookingService = bookingService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public ObservableCollection<BookingSummary> Bookings { get; } = [];
    public ObservableCollection<string> ServiceNames { get; } = [];
    public AsyncCommand RefreshCommand { get; }
    public bool HasBookings => Bookings.Count > 0;
    public bool HasNoBookings => !HasBookings;
    public bool HasSelectedBooking => SelectedBooking is not null;
    public bool HasNoSelectedBooking => !HasSelectedBooking;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasErrorMessage));
        }
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public BookingSummary? SelectedBooking
    {
        get => _selectedBooking;
        set
        {
            if (SetProperty(ref _selectedBooking, value))
            {
                OnPropertyChanged(nameof(HasSelectedBooking));
                OnPropertyChanged(nameof(HasNoSelectedBooking));
                _ = LoadSelectedDetailsAsync(value);
            }
        }
    }

    public async Task InitializeAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var previouslySelected = SelectedBooking?.BookingId;
            var records = await _bookingService.GetBookingHistoryAsync();
            Bookings.Clear();
            foreach (var record in records)
                Bookings.Add(record);
            OnPropertyChanged(nameof(HasBookings));
            OnPropertyChanged(nameof(HasNoBookings));
            SelectedBooking = Bookings.FirstOrDefault(record => record.BookingId == previouslySelected)
                ?? Bookings.FirstOrDefault();
            if (SelectedBooking is null)
                ServiceNames.Clear();
        }
        catch
        {
            ErrorMessage = "Unable to load booking history from the local database.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectedDetailsAsync(BookingSummary? booking)
    {
        ServiceNames.Clear();
        if (booking is null)
            return;

        try
        {
            var services = await _bookingService.GetBookingServiceNamesAsync(booking.BookingId);
            if (SelectedBooking?.BookingId != booking.BookingId)
                return;
            foreach (var name in services)
                ServiceNames.Add(name);
        }
        catch
        {
            ErrorMessage = "Unable to load the selected booking details.";
        }
    }
}
