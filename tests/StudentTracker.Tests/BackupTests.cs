using System.IO.Compression;

namespace StudentTracker.Tests;

/// <summary>
/// Backup and restore of the database plus the managed document store (design section 16).
/// </summary>
[Collection("SerilogGlobal")]
public class BackupTests : IDisposable
{
    private readonly TestHarness _harness = new();

    /// <summary>
    /// The test context is an in-memory SQLite database, so stand in a file at the configured
    /// database path to exercise the archive/restore mechanics.
    /// </summary>
    private string SeedDatabaseFile(string contents)
    {
        var path = Path.Combine(_harness.DataRoot, "Database", "student-tracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public async Task Backup_ContainsTheDatabaseAndEveryManagedDocument()
    {
        SeedDatabaseFile("database-v1");
        var source = Path.Combine(_harness.DataRoot, "signoff.pdf");
        File.WriteAllText(source, "signed");
        var doc = await _harness.Documents.AddDocumentAsync(source, "SignOffs");

        var backup = _harness.Backups.CreateBackup();

        using var archive = ZipFile.OpenRead(backup);
        Assert.Contains(archive.Entries, e => e.FullName == "Database/student-tracker.db");
        Assert.Contains(archive.Entries, e => e.FullName == $"Documents/{doc.RelativePath.Replace("\\", "/")}");
    }

    [Fact]
    public async Task Restore_PutsBackDeletedDocumentsAndTakesAPreRestoreBackupFirst()
    {
        var dbPath = SeedDatabaseFile("database-v1");
        var source = Path.Combine(_harness.DataRoot, "signoff.pdf");
        File.WriteAllText(source, "signed");
        var doc = await _harness.Documents.AddDocumentAsync(source, "SignOffs");
        var backup = _harness.Backups.CreateBackup();

        File.Delete(_harness.Documents.GetFullPath(doc));
        File.WriteAllText(dbPath, "database-v2");

        _harness.Backups.RestoreBackup(backup);

        Assert.Equal("database-v1", File.ReadAllText(dbPath));
        Assert.Equal("signed", File.ReadAllText(_harness.Documents.GetFullPath(doc)));
        var backups = Directory.GetFiles(Path.Combine(_harness.DataRoot, "Backups"));
        Assert.Contains(backups, b => b.Contains("pre-restore"));
    }

    [Fact]
    public void ConsecutiveBackups_DoNotOverwriteEachOther()
    {
        SeedDatabaseFile("database-v1");

        var first = _harness.Backups.CreateBackup();
        var second = _harness.Backups.CreateBackup();

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void Backup_FailsLoudlyWhenThereIsNoDatabase()
    {
        Assert.Throws<InvalidOperationException>(() => _harness.Backups.CreateBackup());
    }

    public void Dispose() => _harness.Dispose();
}
