using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentTracker.Data;
using StudentTracker.Services;
using StudentTracker.Wpf.ViewModels;
using System.IO;
using System.Windows;

namespace StudentTracker.Wpf;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var location = _serviceProvider.GetRequiredService<DataLocationService>();
        location.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(location.LogsPath, "student-tracker-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var bootstrap = _serviceProvider.GetRequiredService<DatabaseBootstrap>();
        using var context = bootstrap.CreateContext();
        bootstrap.EnsureMigrated(context);
        var settings = bootstrap.GetOrCreateSettings(context);

        var seed = new SeedService(context);
        seed.SeedOutcomeReasonsAsync().GetAwaiter().GetResult();
        seed.SeedSampleDataAsync(new DisplayIdGenerator(context)).GetAwaiter().GetResult();

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        MainWindow = new MainWindow { DataContext = mainVm };
        MainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var settings = new Core.Models.AppSettings();
        var dataLocation = new DataLocationService(settings);

        services.AddSingleton(settings);
        services.AddSingleton(dataLocation);
        services.AddSingleton<StudentTracker.Wpf.Services.IDialogService, StudentTracker.Wpf.Services.DialogService>();
        services.AddSingleton<DatabaseBootstrap>();
        services.AddScoped<StudentTrackerDbContext>(provider =>
        {
            var bootstrap = provider.GetRequiredService<DatabaseBootstrap>();
            return bootstrap.CreateContext();
        });
        services.AddScoped<DisplayIdGenerator>();
        services.AddScoped<AuditService>();
        services.AddScoped<StudentService>();
        services.AddScoped<CourseService>();
        services.AddScoped<AllocationService>();
        services.AddScoped<CreditService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<CertificateService>();
        services.AddScoped<SignOffService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<PdfService>();
        services.AddScoped<ReportService>();
        services.AddScoped<InvoicerService>();
        services.AddScoped<BackupService>();
        services.AddScoped<ImportService>(provider => new ImportService(
            provider.GetRequiredService<StudentTrackerDbContext>(),
            provider.GetRequiredService<DisplayIdGenerator>(),
            provider.GetRequiredService<AuditService>()));
        services.AddScoped<MainViewModel>();
        services.AddScoped<DashboardViewModel>();
        services.AddScoped<StudentsViewModel>();
        services.AddScoped<CoursesViewModel>();
        services.AddScoped<DeliveriesViewModel>();
        services.AddScoped<AllocationsViewModel>();
        services.AddScoped<CertificatesViewModel>();
        services.AddScoped<CreditsBudgetsViewModel>();
        services.AddScoped<DocumentsViewModel>();
        services.AddScoped<ReportsViewModel>();
        services.AddScoped<ImportExportViewModel>();
        services.AddScoped<SettingsViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

