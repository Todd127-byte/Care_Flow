using CareFlow.Data;
using CareFlow.Helpers;

namespace CareFlow.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseInitializer _databaseInitializer;
    private object _currentPage;
    private bool _isReady;
    private string? _startupError;

    public DashboardViewModel Dashboard { get; }
    public BookingViewModel Booking { get; }
    public BookingHistoryViewModel BookingHistory { get; }
    public AsyncCommand ShowDashboardCommand { get; }
    public AsyncCommand ShowBookingCommand { get; }
    public AsyncCommand ShowHistoryCommand { get; }

    public object CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public bool IsReady { get => _isReady; private set => SetProperty(ref _isReady, value); }
    public string? StartupError { get => _startupError; private set => SetProperty(ref _startupError, value); }
    public bool HasStartupError => !string.IsNullOrWhiteSpace(StartupError);

    public MainWindowViewModel(
        DatabaseInitializer databaseInitializer,
        DashboardViewModel dashboard,
        BookingViewModel booking,
        BookingHistoryViewModel bookingHistory)
    {
        _databaseInitializer = databaseInitializer;
        Dashboard = dashboard;
        Booking = booking;
        BookingHistory = bookingHistory;
        _currentPage = dashboard;
        ShowDashboardCommand = new AsyncCommand(() => NavigateAsync(Dashboard));
        ShowBookingCommand = new AsyncCommand(() => NavigateAsync(Booking));
        ShowHistoryCommand = new AsyncCommand(() => NavigateAsync(BookingHistory));
        Booking.BookingCompleted += (_, _) => _ = RefreshAfterBookingAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _databaseInitializer.InitializeAsync();
            await Task.WhenAll(Dashboard.InitializeAsync(), Booking.InitializeAsync(), BookingHistory.InitializeAsync());
            IsReady = true;
        }
        catch (Exception)
        {
            StartupError = "CareFlow could not initialize its local database. Close the app and check the database file permissions.";
            OnPropertyChanged(nameof(HasStartupError));
        }
    }

    private async Task NavigateAsync(object page)
    {
        CurrentPage = page;
        if (page == Dashboard)
            await Dashboard.RefreshAsync();
        else if (page == Booking)
            await Booking.InitializeAsync();
        else if (page == BookingHistory)
            await BookingHistory.RefreshAsync();
    }

    private async Task RefreshAfterBookingAsync()
    {
        await Task.WhenAll(Dashboard.RefreshAsync(), BookingHistory.RefreshAsync());
    }
}
