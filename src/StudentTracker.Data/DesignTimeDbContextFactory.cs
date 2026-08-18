using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentTracker.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StudentTrackerDbContext>
{
    public StudentTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StudentTrackerDbContext>();
        optionsBuilder.UseSqlite("Data Source=student-tracker-design.db");
        return new StudentTrackerDbContext(optionsBuilder.Options);
    }
}
