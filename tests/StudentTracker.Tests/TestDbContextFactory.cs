using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Data;

namespace StudentTracker.Tests;

/// <summary>
/// Builds a throwaway SQLite database for tests. SQLite is used rather than the in-memory
/// provider so that transactions, rollback and relational constraints behave as they do in
/// the shipped application.
/// </summary>
public static class TestDbContextFactory
{
    public static StudentTrackerDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<StudentTrackerDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new StudentTrackerDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
