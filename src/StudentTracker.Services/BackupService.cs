using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class BackupService
{
    private readonly DataLocationService _dataLocation;
    private readonly StudentTrackerDbContext _context;
    private readonly AuditService _audit;

    public BackupService(DataLocationService dataLocation, StudentTrackerDbContext context, AuditService audit)
    {
        _dataLocation = dataLocation;
        _context = context;
        _audit = audit;
    }

    public string CreateBackup(string? label = null)
    {
        Directory.CreateDirectory(_dataLocation.BackupsPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"StudentTracker-backup-{timestamp}{(label != null ? "-" + label : "")}.zip";
        var path = Path.Combine(_dataLocation.BackupsPath, fileName);

        // Flush SQLite's WAL before taking the snapshot. Read with shared access because the
        // application's DbContext may legitimately keep the database handle open.
        _context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var databaseEntry = archive.CreateEntry("Database/student-tracker.db", CompressionLevel.Optimal);
            using (var source = new FileStream(_dataLocation.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var target = databaseEntry.Open())
                source.CopyTo(target);
            AddDirectory(archive, _dataLocation.DocumentsPath, "Documents");
            AddDirectory(archive, _dataLocation.TemplatesPath, "Templates");
        }

        _audit.Record("BackupCreated", "System", Guid.Empty, null, null, new { Path = path });
        _context.SaveChanges();
        return path;
    }

    private static void AddDirectory(ZipArchive archive, string sourceDir, string entryPrefix)
    {
        if (!Directory.Exists(sourceDir)) return;
        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            archive.CreateEntryFromFile(file, $"{entryPrefix}/{relative.Replace("\\", "/")}");
        }
    }

    public void RestoreBackup(string backupPath)
    {
        CreateBackup("pre-restore");
        using var archive = ZipFile.OpenRead(backupPath);
        var temp = Path.Combine(Path.GetTempPath(), $"st-restore-{Guid.NewGuid()}");
        Directory.CreateDirectory(temp);
        archive.ExtractToDirectory(temp, true);

        var dbSource = Path.Combine(temp, "Database", "student-tracker.db");
        if (File.Exists(dbSource))
        {
            File.Copy(dbSource, _dataLocation.DatabasePath, overwrite: true);
        }

        var docsSource = Path.Combine(temp, "Documents");
        if (Directory.Exists(docsSource))
        {
            Directory.CreateDirectory(_dataLocation.DocumentsPath);
            CopyDirectory(docsSource, _dataLocation.DocumentsPath);
        }

        _audit.Record("BackupRestored", "System", Guid.Empty);
        _context.SaveChanges();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
    }

    public void CleanupOldBackups(int daily = 7, int weekly = 8, int monthly = 12)
    {
        if (!Directory.Exists(_dataLocation.BackupsPath)) return;
        var files = Directory.GetFiles(_dataLocation.BackupsPath, "StudentTracker-backup-*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        var keep = new HashSet<string>();
        foreach (var f in files.Take(daily)) keep.Add(f.FullName);
        foreach (var f in files.GroupBy(f => f.CreationTime.ToString("yyyy-ww")).Select(g => g.OrderByDescending(x => x.CreationTime).First()).Take(weekly)) keep.Add(f.FullName);
        foreach (var f in files.GroupBy(f => f.CreationTime.ToString("yyyy-MM")).Select(g => g.OrderByDescending(x => x.CreationTime).First()).Take(monthly)) keep.Add(f.FullName);

        foreach (var f in files.Where(f => !keep.Contains(f.FullName)))
        {
            try { f.Delete(); } catch { }
        }
    }
}
