using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Data;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DataBrowserViewModel : ViewModelBase, ICloseable
{
    private readonly StudentTrackerDbContext _context;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Data Browser";

    [ObservableProperty]
    private ObservableCollection<string> _tableNames = new();

    [ObservableProperty]
    private string? _selectedTable;

    [ObservableProperty]
    private IList _rows = new ArrayList();

    [ObservableProperty]
    private string _statusText = "Select a table to view its rows.";

    public DataBrowserViewModel(StudentTrackerDbContext context)
    {
        _context = context;
        _tableNames = new ObservableCollection<string>(GetDbSetPropertyNames());
    }

    partial void OnSelectedTableChanged(string? value)
    {
        _ = LoadRowsAsync(value);
    }

    private async Task LoadRowsAsync(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            Rows = new ArrayList();
            StatusText = "Select a table to view its rows.";
            return;
        }

        try
        {
            var property = typeof(StudentTrackerDbContext).GetProperty(tableName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                StatusText = $"Table '{tableName}' not found.";
                return;
            }

            var entityType = property.PropertyType.GetGenericArguments().FirstOrDefault();
            if (entityType == null)
            {
                StatusText = $"Cannot determine entity type for '{tableName}'.";
                return;
            }

            var set = _context.GetType().GetMethod(nameof(_context.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType)
                .Invoke(_context, null);

            var queryable = (IQueryable)set!;
            var listMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
                .First(m => m.GetGenericArguments().Length == 1)
                .MakeGenericMethod(entityType);

            var task = (Task)listMethod.Invoke(null, new object[] { queryable, CancellationToken.None })!;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            var list = (IList)resultProperty!.GetValue(task)!;

            Rows = list;
            StatusText = $"{list.Count} row(s) in {tableName}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading {tableName}: {ex.Message}";
        }
    }

    private static List<string> GetDbSetPropertyNames()
    {
        return typeof(StudentTrackerDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
    }
}
