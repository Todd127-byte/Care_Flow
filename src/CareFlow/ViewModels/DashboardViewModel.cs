using System.Collections.ObjectModel;
using CareFlow.Helpers;
using CareFlow.Services;

namespace CareFlow.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly AvailabilityService _availabilityService;
    private DateTimeOffset? _selectedDate = DateTimeOffset.Now.Date;
    private int _patientCount;
    private int _serviceCount;
    private int _bookingCount;
    private bool _isBusy;
    private string? _errorMessage;
    private bool _isInitialized;

    public ObservableCollection<AvailabilityCardViewModel> Services { get; } = [];
    public AsyncCommand RefreshCommand { get; }

    public DashboardViewModel(AvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public int PatientCount { get => _patientCount; private set => SetProperty(ref _patientCount, value); }
    public int ServiceCount { get => _serviceCount; private set => SetProperty(ref _serviceCount, value); }
    public int BookingCount { get => _bookingCount; private set => SetProperty(ref _bookingCount, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RefreshCommand.NotifyCanExecuteChanged();
        }
    }
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

    public DateTimeOffset? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value) && _isInitialized)
                _ = RefreshAsync();
        }
    }

    public async Task InitializeAsync()
    {
        _isInitialized = true;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (SelectedDate is null)
        {
            ErrorMessage = "Please select a date.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summary = await _availabilityService.GetSummaryAsync();
            PatientCount = summary.PatientCount;
            ServiceCount = summary.ServiceCount;
            BookingCount = summary.BookingCount;

            var date = DateOnly.FromDateTime(SelectedDate.Value.LocalDateTime);
            var services = await _availabilityService.GetAvailabilityAsync(date);
            Services.Clear();
            foreach (var service in services)
                Services.Add(new AvailabilityCardViewModel(service));
        }
        catch
        {
            ErrorMessage = "Unable to load dashboard information. Check the local database and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
