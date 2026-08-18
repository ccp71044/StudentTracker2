using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class DatabaseBootstrap
{
    private readonly DataLocationService _dataLocationService;

    public DatabaseBootstrap(DataLocationService dataLocationService)
    {
        _dataLocationService = dataLocationService;
    }

    public StudentTrackerDbContext CreateContext()
    {
        _dataLocationService.EnsureDirectories();
        var optionsBuilder = new DbContextOptionsBuilder<StudentTrackerDbContext>();
        optionsBuilder.UseSqlite(_dataLocationService.GetConnectionString());
        return new StudentTrackerDbContext(optionsBuilder.Options);
    }

    public void EnsureMigrated(StudentTrackerDbContext context)
    {
        var pending = context.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            context.Database.Migrate();
        }
    }

    public AppSettings GetOrCreateSettings(StudentTrackerDbContext context)
    {
        var settings = context.AppSettings.FirstOrDefault();
        if (settings == null)
        {
            settings = new AppSettings();
            context.AppSettings.Add(settings);
            context.SaveChanges();
        }
        return settings;
    }

    public void CompactDatabase(StudentTrackerDbContext context)
    {
        context.Database.ExecuteSqlRaw("VACUUM;");
    }
}
