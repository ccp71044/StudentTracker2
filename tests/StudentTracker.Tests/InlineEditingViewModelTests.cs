using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;
using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Tests;

public class InlineEditingViewModelTests
{
    private static StudentTrackerDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<StudentTrackerDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var context = new StudentTrackerDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static StudentService CreateStudentService(StudentTrackerDbContext context)
    {
        return new StudentService(context, new DisplayIdGenerator(context), new AuditService(context));
    }

    private static CourseService CreateCourseService(StudentTrackerDbContext context)
    {
        return new CourseService(context, new DisplayIdGenerator(context), new AuditService(context));
    }

    private class FakeDialogService : IDialogService
    {
        public List<(string Message, Exception? Exception)> Errors { get; } = new();

        public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase => false;

        public bool Confirm(string message, string title = "Confirm action") => false;

        public void ShowError(string message, Exception? exception = null, string title = "Student Tracker")
        {
            Errors.Add((message, exception));
        }
    }

    private static async Task WaitForAnyItemAsync<T>(Func<IEnumerable<T>> getItems)
    {
        for (var i = 0; i < 50; i++)
        {
            if (getItems().Any()) return;
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task InlineEditing_IsDisabledByDefault()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateStudentService(context);
        var dialog = new FakeDialogService();
        var vm = new StudentsViewModel(service, null!, null!, null!, null!, dialog);

        Assert.False(vm.IsInlineEditingEnabled);

        // Give the view-model's background load a moment to finish before disposing the context.
        await Task.Delay(200);
    }

    [Fact]
    public async Task StudentsViewModel_RowEditEnding_UpdatesStudentAndCreatesAudit()
    {
        var dbName = Guid.NewGuid().ToString();

        using var seedContext = CreateContext(dbName);
        seedContext.AppSettings.Add(new AppSettings());
        await seedContext.SaveChangesAsync();
        var seedService = CreateStudentService(seedContext);
        await seedService.CreateAsync(new Student { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" });

        using var vmContext = CreateContext(dbName);
        var vmService = CreateStudentService(vmContext);
        var dialog = new FakeDialogService();
        var vm = new StudentsViewModel(vmService, null!, null!, null!, null!, dialog);
        await WaitForAnyItemAsync(() => vm.Students);

        var student = vm.Students.First();
        student.FirstName = "Janet";
        await vm.StudentRowEditEndingCommand.ExecuteAsync(student);

        Assert.Equal("Janet", vmContext.Students.First().FirstName);
        Assert.Contains(vmContext.AuditLogs, a => a.Action == "Updated" && a.EntityId == student.Id);
        Assert.Empty(dialog.Errors);
    }

    [Fact]
    public async Task StudentsViewModel_RowEditEnding_NullParameter_DoesNothing()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateStudentService(context);
        var dialog = new FakeDialogService();
        var vm = new StudentsViewModel(service, null!, null!, null!, null!, dialog);

        await vm.StudentRowEditEndingCommand.ExecuteAsync(null);

        Assert.Empty(dialog.Errors);
        Assert.Empty(context.AuditLogs);
    }

    [Fact]
    public async Task CoursesViewModel_RowEditEnding_UpdatesCourseAndCreatesAudit()
    {
        var dbName = Guid.NewGuid().ToString();

        using var seedContext = CreateContext(dbName);
        var seedService = CreateCourseService(seedContext);
        await seedService.CreateDefinitionAsync(new CourseDefinition { CourseCode = "C-101", CourseTitle = "Old Title" });

        using var vmContext = CreateContext(dbName);
        var vmService = CreateCourseService(vmContext);
        var dialog = new FakeDialogService();
        var vm = new CoursesViewModel(vmService, dialog);
        await WaitForAnyItemAsync(() => vm.Courses);

        var course = vm.Courses.First();
        course.CourseTitle = "New Title";
        await vm.CourseRowEditEndingCommand.ExecuteAsync(course);

        Assert.Equal("New Title", vmContext.CourseDefinitions.First().CourseTitle);
        Assert.Contains(vmContext.AuditLogs, a => a.Action == "Updated" && a.EntityId == course.Id);
        Assert.Empty(dialog.Errors);
    }

    [Fact]
    public async Task DeliveriesViewModel_RowEditEnding_UpdatesDeliveryAndCreatesAudit()
    {
        var dbName = Guid.NewGuid().ToString();

        using var seedContext = CreateContext(dbName);
        seedContext.AppSettings.Add(new AppSettings());
        await seedContext.SaveChangesAsync();
        var seedService = CreateCourseService(seedContext);
        var course = await seedService.CreateDefinitionAsync(new CourseDefinition { CourseCode = "C-202", CourseTitle = "Course" });
        await seedService.CreateDeliveryAsync(new CourseDelivery
        {
            CourseDefinitionId = course.Id,
            Location = "Sydney",
            Capacity = 10
        });

        using var vmContext = CreateContext(dbName);
        var vmService = CreateCourseService(vmContext);
        var dialog = new FakeDialogService();
        var vm = new DeliveriesViewModel(vmService, null!, null!, null!, null!, null!, null!, null!, dialog);
        await WaitForAnyItemAsync(() => vm.Deliveries);

        var delivery = vm.Deliveries.First();
        delivery.Location = "Melbourne";
        await vm.DeliveryRowEditEndingCommand.ExecuteAsync(delivery);

        Assert.Equal("Melbourne", vmContext.CourseDeliveries.First().Location);
        Assert.Contains(vmContext.AuditLogs, a => a.Action == "Updated" && a.EntityId == delivery.Id);
        Assert.Empty(dialog.Errors);
    }
}
