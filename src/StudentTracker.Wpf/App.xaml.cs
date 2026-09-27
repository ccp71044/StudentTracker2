using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using StudentTracker.Data;
using StudentTracker.Services;
using StudentTracker.Wpf.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StudentTracker.Wpf;

public partial class App : Application
{
    private const string SampleDataSwitch = "--sample-data";
    private const string DataRootSwitch = "--data-root";

    private ServiceProvider? _serviceProvider;
    private Core.Models.AppSettings _settings = new();
    private string _logsPath = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        _settings = BuildSettings(e.Args);

        // Logging is configured before anything else runs: in release 1 it was set up after the
        // service provider had been built, so any failure during start-up was written to Serilog's
        // silent default logger and lost.
        ConfigureLogging();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled application exception");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved background task exception");
            args.SetObserved();
        };

        try
        {
            Start(e.Args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Student Tracker failed to start");
            Log.CloseAndFlush();
            ShowFatalError("Student Tracker could not start.", ex);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Reads "--data-root &lt;path&gt;", which moves the database, documents, backups, exports and logs
    /// somewhere other than %LOCALAPPDATA%. The UI tests use it so a run cannot touch real data.
    /// </summary>
    private static Core.Models.AppSettings BuildSettings(string[] args)
    {
        var settings = new Core.Models.AppSettings();

        var index = Array.FindIndex(args, a => a.Equals(DataRootSwitch, StringComparison.OrdinalIgnoreCase));
        var dataRoot = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : args.FirstOrDefault(a => a.StartsWith(DataRootSwitch + "=", StringComparison.OrdinalIgnoreCase))?[(DataRootSwitch.Length + 1)..];

        if (string.IsNullOrWhiteSpace(dataRoot)) return settings;

        settings.DataRootPath = Path.GetFullPath(dataRoot);
        settings.BackupLocation = Path.Combine(settings.DataRootPath, "Backups");
        settings.InvoicerExchangeLocation = Path.Combine(settings.DataRootPath, "Integration", "InvoicerExport");
        return settings;
    }

    private void ConfigureLogging()
    {
        var location = new DataLocationService(_settings);
        location.EnsureDirectories();
        _logsPath = location.LogsPath;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(location.LogsPath, "student-tracker-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(location.LogsPath, "error-.log"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Starting Student Tracker {Version}", AppVersion.Current);
    }

    private void Start(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services, _settings);
        _serviceProvider = services.BuildServiceProvider();

        _serviceProvider.GetRequiredService<DataLocationService>().EnsureDirectories();

        var bootstrap = _serviceProvider.GetRequiredService<DatabaseBootstrap>();
        using var context = bootstrap.CreateContext();
        bootstrap.EnsureMigrated(context);
        bootstrap.GetOrCreateSettings(context);

        var seed = new SeedService(context);
        seed.SeedOutcomeReasonsAsync().GetAwaiter().GetResult();

        if (args.Contains(SampleDataSwitch, StringComparer.OrdinalIgnoreCase))
        {
            Log.Information("Seeding demonstration data ({Switch} supplied)", SampleDataSwitch);
            seed.SeedSampleDataAsync(new DisplayIdGenerator(context)).GetAwaiter().GetResult();
        }

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        MainWindow = new MainWindow { DataContext = mainVm };
        MainWindow.Show();

        Dispatcher.InvokeAsync(async () => await mainVm.EnsureInitialisedAsync());
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        ShowFatalError("Something went wrong and the last action was cancelled.", e.Exception);
        e.Handled = true;
    }

    private void ShowFatalError(string summary, Exception exception)
    {
        var logHint = string.IsNullOrEmpty(_logsPath) ? string.Empty : $"\n\nDetails were written to:\n{_logsPath}";
        MessageBox.Show(
            $"{summary}\n\n{exception.Message}{logHint}",
            "Student Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void ConfigureServices(IServiceCollection services, Core.Models.AppSettings settings)
    {
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
        services.AddScoped<DisplayIdReconciliationService>();
        services.AddScoped<AuditService>();
        services.AddScoped<StudentService>();
        services.AddScoped<CourseService>();
        services.AddScoped<AllocationService>();
        services.AddScoped<CreditService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<PricingService>();
        services.AddScoped<BudgetSummaryService>();
        services.AddScoped<ClientPrepaidEntitlementService>();
        services.AddScoped<InvoicerReferenceImportService>();
        services.AddScoped<CompletionPricingImporter>();
        services.AddScoped<ProviderCreditHistoryImporter>();
        services.AddScoped<CertificateService>();
        services.AddScoped<SignOffService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<IDocumentService>(provider => provider.GetRequiredService<DocumentService>());
        services.AddScoped<PdfService>();
        services.AddScoped<ReportService>();
        services.AddScoped<InvoicerService>();
        services.AddScoped<InvoicerReferenceExportService>();
        services.AddScoped<BackupService>();
        services.AddScoped<DataCutoverService>();
        services.AddScoped<ImportService>(provider => new ImportService(
            provider.GetRequiredService<StudentTrackerDbContext>(),
            provider.GetRequiredService<DisplayIdGenerator>(),
            provider.GetRequiredService<AuditService>()));
        services.AddScoped<MainViewModel>();
        services.AddScoped<DashboardViewModel>();
        services.AddScoped<StudentsViewModel>();
        services.AddScoped<CoursesViewModel>();
        services.AddScoped<DeliveriesViewModel>();
        services.AddScoped<DeliveryEditViewModel>();
        services.AddScoped<AllocationsViewModel>();
        services.AddScoped<AllocationEditViewModel>();
        services.AddScoped<CertificatesViewModel>();
        services.AddScoped<CertificateOrderEditViewModel>();
        services.AddScoped<CertificateDeliveryEditViewModel>();
        services.AddScoped<CreditsBudgetsViewModel>();
        services.AddScoped<CreditPoolEditViewModel>();
        services.AddScoped<DocumentsViewModel>();
        services.AddScoped<ReportsViewModel>();
        services.AddScoped<CompletionsViewModel>();
        services.AddScoped<PoolPositionViewModel>();
        services.AddScoped<InvoicerReferenceViewModel>();
        services.AddScoped<ImportExportViewModel>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<DataBrowserViewModel>();
        services.AddScoped<StudentOverviewViewModel>();
        services.AddScoped<CourseDeliveryOverviewViewModel>();
        services.AddScoped<ImportReviewQueueViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

