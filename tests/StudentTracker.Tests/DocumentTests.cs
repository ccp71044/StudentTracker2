using StudentTracker.Core.Enums;
using StudentTracker.Services;

namespace StudentTracker.Tests;

/// <summary>
/// Managed document storage: checksums, versioning and missing-file detection (design section 13).
/// </summary>
public class DocumentTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly DocumentService _documents;
    private readonly string _incoming;

    public DocumentTests()
    {
        _documents = _harness.Documents;
        _incoming = Path.Combine(_harness.DataRoot, "incoming");
        Directory.CreateDirectory(_incoming);
    }

    private string WriteSource(string name, string content)
    {
        var path = Path.Combine(_incoming, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task AddDocument_CopiesIntoTheManagedStoreAndRecordsAChecksum()
    {
        var doc = await _documents.AddDocumentAsync(WriteSource("signoff.pdf", "original"), "SignOffs");

        Assert.True(File.Exists(_documents.GetFullPath(doc)));
        Assert.Equal("signoff.pdf", doc.OriginalFileName);
        Assert.NotNull(doc.Sha256);
        Assert.True(await _documents.VerifyChecksumAsync(doc.Id));
    }

    [Fact]
    public async Task VerifyChecksum_FailsWhenTheManagedFileIsEditedOutsideTheApp()
    {
        var doc = await _documents.AddDocumentAsync(WriteSource("signoff.pdf", "original"), "SignOffs");

        File.WriteAllText(_documents.GetFullPath(doc), "tampered");

        Assert.False(await _documents.VerifyChecksumAsync(doc.Id));
    }

    [Fact]
    public async Task AddVersion_SupersedesThePreviousVersionAndMovesItsLinks()
    {
        var student = _harness.AddStudent();
        var v1 = await _documents.AddDocumentAsync(WriteSource("signoff.pdf", "v1"), "SignOffs");
        await _documents.LinkDocumentAsync(v1.Id, "Student", student.Id, "Sign-off");

        var v2 = await _documents.AddVersionAsync(v1.Id, WriteSource("signoff-signed.pdf", "v2"));

        Assert.Equal(2, v2.Version);
        Assert.Equal(v1.Id, v2.SupersedesDocumentId);
        Assert.Equal(DocumentStatus.Superseded, v1.Status);
        // The original file is retained, but only the current version is surfaced.
        Assert.True(File.Exists(_documents.GetFullPath(v1)));
        var linked = await _documents.GetDocumentsForEntityAsync("Student", student.Id);
        Assert.Equal(new[] { v2.Id }, linked.Select(d => d.Id));
    }

    [Fact]
    public async Task CheckMissingFiles_FlagsDeletedFilesAndClearsTheFlagWhenTheyReturn()
    {
        var doc = await _documents.AddDocumentAsync(WriteSource("certificate.pdf", "data"), "Certificates");
        var managedPath = _documents.GetFullPath(doc);
        var backup = File.ReadAllBytes(managedPath);

        File.Delete(managedPath);
        Assert.Equal(new[] { doc.Id }, (await _documents.CheckMissingFilesAsync()).Select(d => d.Id));
        Assert.Equal(DocumentStatus.Missing, doc.Status);
        Assert.Single(await _harness.Reports.GetMissingDocumentsAsync());

        File.WriteAllBytes(managedPath, backup);
        Assert.Empty(await _documents.CheckMissingFilesAsync());
        Assert.Equal(DocumentStatus.Active, doc.Status);
        Assert.Empty(await _harness.Reports.GetMissingDocumentsAsync());
    }

    [Fact]
    public async Task DeleteLink_ArchivesADocumentThatIsNoLongerLinkedToAnything()
    {
        var student = _harness.AddStudent();
        var doc = await _documents.AddDocumentAsync(WriteSource("note.pdf", "data"), "Other");
        await _documents.LinkDocumentAsync(doc.Id, "Student", student.Id);

        await _documents.DeleteLinkAsync(doc.Id, "Student", student.Id);

        Assert.Equal(DocumentStatus.Archived, doc.Status);
        Assert.Empty(await _documents.GetDocumentsForEntityAsync("Student", student.Id));
    }

    public void Dispose() => _harness.Dispose();
}
