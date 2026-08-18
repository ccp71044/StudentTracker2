using Microsoft.EntityFrameworkCore;
using StudentTracker.Data;

namespace StudentTracker.Tests;

public static class TestDbContextFactory
{
    public static StudentTrackerDbContext Create()
    {
        var options = new DbContextOptionsBuilder<StudentTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new StudentTrackerDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
