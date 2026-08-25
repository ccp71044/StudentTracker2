using Microsoft.EntityFrameworkCore;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Runs a multi-step workflow so that either every change is saved or none of them are
/// (design section 12.3 and 18). Nested calls join the outermost transaction.
/// </summary>
public static class DbTransactionScope
{
    public static async Task<T> RunAsync<T>(StudentTrackerDbContext context, Func<Task<T>> work)
    {
        if (context.Database.CurrentTransaction is not null || !context.Database.IsRelational())
            return await work();

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var result = await work();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public static Task RunAsync(StudentTrackerDbContext context, Func<Task> work) =>
        RunAsync(context, async () => { await work(); return true; });
}
