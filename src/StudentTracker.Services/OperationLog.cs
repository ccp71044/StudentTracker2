using Serilog;

namespace StudentTracker.Services;

/// <summary>
/// Records the outcome of the operations that touch files, external exports or the database in
/// bulk. Release 1 logged nothing below the window layer, so a failed import or restore left no
/// trace once the message box was dismissed.
/// </summary>
public static class OperationLog
{
    public static T Run<T>(string operation, Func<T> action, object? context = null)
    {
        var logger = Log.ForContext("Operation", operation);
        if (context != null)
            logger = logger.ForContext("Context", context, destructureObjects: true);

        try
        {
            var result = action();
            logger.Information("{Operation} completed", operation);
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Operation} failed", operation);
            throw;
        }
    }

    public static void Run(string operation, Action action, object? context = null) =>
        Run(operation, () => { action(); return true; }, context);

    public static async Task<T> RunAsync<T>(string operation, Func<Task<T>> action, object? context = null)
    {
        var logger = Log.ForContext("Operation", operation);
        if (context != null)
            logger = logger.ForContext("Context", context, destructureObjects: true);

        try
        {
            var result = await action();
            logger.Information("{Operation} completed", operation);
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Operation} failed", operation);
            throw;
        }
    }

    /// <summary>Logs a failure that is handled and reported to the user rather than thrown on.</summary>
    public static void Failure(string operation, Exception exception, object? context = null)
    {
        var logger = Log.ForContext("Operation", operation);
        if (context != null)
            logger = logger.ForContext("Context", context, destructureObjects: true);

        logger.Error(exception, "{Operation} failed", operation);
    }
}
