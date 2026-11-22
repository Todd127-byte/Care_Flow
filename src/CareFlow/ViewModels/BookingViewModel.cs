using System.Collections.ObjectModel;
using System.ComponentModel;
using CareFlow.Helpers;
using CareFlow.Services;

namespace CareFlow.ViewModels;

public sealed class BookingViewModel : ObservableObject
{
    private readonly PatientService _patientService;
    private readonly AvailabilityService _availabilityService;
    private readonly BookingService _bookingService;
    private PatientOption? _selectedPatient;
    private DateTimeOffset? _selectedDate = DateTimeOffset.Now.Date;
    private bool _isBusy;
    private int _selectedServiceCount;
    private string? _errorMessage;
    private string? _confirmationMessage;
    private bool _isInitialized;

    public ObservableCollection<PatientOption> Patients { get; } = [];
    public ObservableCollection<ServiceSelectionViewModel> Services { get; } = [];
    public AsyncCommand ConfirmBookingCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public event EventHandler? BookingCompleted;

    public PatientOption? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (SetProperty(ref _selectedPatient, value))
                OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public DateTimeOffset? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value) && _isInitialized)
            {
                foreach (var service in Services)
                    service.IsSelected = false;
                ErrorMessage = null;
                ConfirmationMessage = null;
                _ = LoadAvailabilityAsync();
            }
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public int SelectedServiceCount
    {
        get => _selectedServiceCount;
        private set
        {
            if (SetProperty(ref _selectedServiceCount, value))
                OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanConfirm));
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
    public string? ConfirmationMessage
    {
        get => _confirmationMessage;
        private set
        {
            if (SetProperty(ref _confirmationMessage, value))
                OnPropertyChanged(nameof(HasConfirmationMessage));
        }
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasConfirmationMessage => !string.IsNullOrWhiteSpace(ConfirmationMessage);
    public bool CanConfirm => !IsBusy && SelectedPatient is not null && SelectedDate is not null && SelectedServiceCount is >= 1 and <= 3;

    public BookingViewModel(PatientService patientService, AvailabilityService availabilityService, BookingService bookingService)
    {
        _patientService = patientService;
        _availabilityService = availabilityService;
        _bookingService = bookingService;
        ConfirmBookingCommand = new AsyncCommand(ConfirmAsync);
        ResetCommand = new AsyncCommand(ResetAsync);
    }

    public async Task InitializeAsync()
    {
        _isInitialized = true;
        try
        {
            var patients = await _patientService.GetPatientsAsync();
            Patients.Clear();
            foreach (var patient in patients)
                Patients.Add(patient);
            await LoadAvailabilityAsync();
        }
        catch
        {
            ErrorMessage = "Unable to load patients and diagnostic services. Check the local database and try again.";
        }
    }

    private async Task LoadAvailabilityAsync()
    {
        if (SelectedDate is null)
        {
            Services.Clear();
            SelectedServiceCount = 0;
            return;
        }

        IsBusy = true;
        try
        {
            var date = DateOnly.FromDateTime(SelectedDate.Value.LocalDateTime);
            var availability = await _availabilityService.GetAvailabilityAsync(date);
            Services.Clear();
            foreach (var item in availability)
            {
                var option = new ServiceSelectionViewModel(item);
                option.PropertyChanged += OnServicePropertyChanged;
                Services.Add(option);
            }
            UpdateSelectionCount();
        }
        catch
        {
            ErrorMessage = "Unable to load service availability for this date.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ServiceSelectionViewModel.IsSelected) || sender is not ServiceSelectionViewModel service)
            return;

        if (service.IsSelected && !service.IsAvailable)
        {
            service.IsSelected = false;
            ErrorMessage = "The selected service has reached its daily capacity.";
            return;
        }

        if (service.IsSelected && Services.Count(item => item.IsSelected) > 3)
        {
            service.IsSelected = false;
            ErrorMessage = "You can select a maximum of three services.";
            return;
        }

        ErrorMessage = null;
        ConfirmationMessage = null;
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount() => SelectedServiceCount = Services.Count(item => item.IsSelected);

    private async Task ConfirmAsync()
    {
        ErrorMessage = null;
        ConfirmationMessage = null;
        if (SelectedPatient is null)
        {
            ErrorMessage = "Please select a patient.";
            return;
        }
        if (SelectedDate is null)
        {
            ErrorMessage = "Please select a booking date.";
            return;
        }
        if (SelectedServiceCount == 0)
        {
            ErrorMessage = "Please select at least one diagnostic service.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _bookingService.CreateBookingAsync(
                SelectedPatient.PatientId,
                DateOnly.FromDateTime(SelectedDate.Value.LocalDateTime),
                Services.Where(item => item.IsSelected).Select(item => item.ServiceId).ToArray());

            if (!result.Succeeded || result.Confirmation is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to save booking. Please try again.";
                return;
            }

            var confirmation = result.Confirmation;
            ConfirmationMessage = $"Booking #{confirmation.BookingId} confirmed for {confirmation.PatientName} on {confirmation.BookingDate:MMMM d, yyyy}: {string.Join(", ", confirmation.ServiceNames)}.";
            BookingCompleted?.Invoke(this, EventArgs.Empty);
            await LoadAvailabilityAsync();
        }
        catch
        {
            ErrorMessage = "Unable to save booking. Please try again.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private Task ResetAsync()
    {
        SelectedPatient = null;
        SelectedDate = DateTimeOffset.Now.Date;
        foreach (var service in Services)
            service.IsSelected = false;
        ErrorMessage = null;
        ConfirmationMessage = null;
        UpdateSelectionCount();
        return Task.CompletedTask;
    }
}
