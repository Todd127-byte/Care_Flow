using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CareFlow.Data;
using CareFlow.Services;
using CareFlow.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareFlow;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _serviceProvider = ConfigureServices();
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _serviceProvider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var configuredPath = Environment.GetEnvironmentVariable("CARE_FLOW_DB_PATH");
        var databaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CareFlow");
        var databasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(databaseDirectory, "careflow.db")
            : Path.GetFullPath(configuredPath);
        databaseDirectory = Path.GetDirectoryName(databasePath) ?? databaseDirectory;
        Directory.CreateDirectory(databaseDirectory);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<CareFlowDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Default Timeout=30"));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<AvailabilityService>();
        services.AddSingleton<PatientService>();
        services.AddSingleton<BookingService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<BookingViewModel>();
        services.AddSingleton<BookingHistoryViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }
}
