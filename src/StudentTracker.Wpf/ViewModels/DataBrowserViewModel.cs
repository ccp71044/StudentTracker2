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
    private Type? _entityType;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Data Browser";

    [ObservableProperty]
    private ObservableCollection<string> _tableNames = new();

    [ObservableProperty]
    private string? _selectedTable;

    [ObservableProperty]
    private ObservableCollection<object> _rows = new();

    [ObservableProperty]
    private object? _selectedRow;

    [ObservableProperty]
    private string _statusText = "Select a table to view and edit its rows.";

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
            Rows = new ObservableCollection<object>();
            _entityType = null;
            StatusText = "Select a table to view and edit its rows.";
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

            _entityType = property.PropertyType.GetGenericArguments().FirstOrDefault();
            if (_entityType == null)
            {
                StatusText = $"Cannot determine entity type for '{tableName}'.";
                return;
            }

            var set = _context.GetType().GetMethod(nameof(_context.Set), Type.EmptyTypes)!
                .MakeGenericMethod(_entityType)
                .Invoke(_context, null);

            var queryable = (IQueryable)set!;
            var listMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
                .First(m => m.GetGenericArguments().Length == 1)
                .MakeGenericMethod(_entityType);

            var task = (Task)listMethod.Invoke(null, new object[] { queryable, CancellationToken.None })!;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            var list = (IEnumerable)resultProperty!.GetValue(task)!;

            Rows = new ObservableCollection<object>(list.Cast<object>());
            StatusText = $"{Rows.Count} row(s) in {tableName}. Use Save to persist cell edits.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading {tableName}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        if (_entityType == null)
            return;

        try
        {
            var newItem = Activator.CreateInstance(_entityType);
            if (newItem == null)
                return;

            _context.Add(newItem);
            Rows.Add(newItem);
            SelectedRow = newItem;
            StatusText = "New row added. Edit the row, then Save.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not add row: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteRow()
    {
        if (SelectedRow == null)
            return;

        try
        {
            _context.Remove(SelectedRow);
            Rows.Remove(SelectedRow);
            await _context.SaveChangesAsync();
            SelectedRow = null;
            StatusText = "Row deleted.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not delete row: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveChanges()
    {
        try
        {
            var saved = await _context.SaveChangesAsync();
            StatusText = $"Saved {saved} change(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (SelectedTable != null)
            await LoadRowsAsync(SelectedTable);
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
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
}
