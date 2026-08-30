using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class ClientPrepaidEntitlementTests
{
    private static (StudentTrackerDbContext Context, DisplayIdGenerator Gen, AuditService Audit, ClientPrepaidEntitlementService Service) CreateService()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new ClientPrepaidEntitlementService(context, gen, audit);
        return (context, gen, audit, service);
    }

    [Fact]
    public async Task MP002_TenPrepaid_EightComplete_TwoCarryForward()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "Provide First Aid", Category = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool
        {
            Name = "T&C First Aid",
            Client = "T&C",
            RestrictedToCourseDefinitionId = course.Id
        });

        await service.AddPrepaidPlacesAsync(pool.Id, 10m);

        var beforeReserve = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(10m, beforeReserve.UnassignedCarryForward);

        for (int i = 0; i < 8; i++)
        {
            var student = new Student { FirstName = $"S{i}", LastName = "A", Email = $"s{i}@example.com" };
            context.Students.Add(student);
            var alloc = new Allocation
            {
                DisplayId = gen.NextDisplayId<Allocation>("ALL"),
                CourseDeliveryId = delivery.Id,
                StudentId = student.Id
            };
            context.Allocations.Add(alloc);
            context.SaveChanges();

            await service.ReservePlaceAsync(pool.Id, alloc.Id, 1m);
            await service.AssignPlaceAsync(pool.Id, alloc.Id, 1m);
            await service.ConsumePlaceAsync(pool.Id, alloc.Id, 1m);
        }

        var position = await service.GetPoolPositionAsync(pool.Id);

        Assert.Equal(10m, position.PrepaidPlacesLoaded);
        Assert.Equal(8m, position.PlacesConsumed);
        Assert.Equal(2m, position.TotalUnconsumed);
        Assert.Equal(0m, position.ReservedToNamedStudents);
        Assert.Equal(0m, position.ReservedPlaceholders);
        Assert.Equal(2m, position.UnassignedCarryForward);
    }

    [Fact]
    public async Task MP003_CarryTwo_RequestSix_FourRequireFunding()
    {
        var (context, _, _, service) = CreateService();

        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C First Aid", Client = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 2m);

        var need = await service.CalculateFundingAsync(pool.Id, 6m, 0m);
        Assert.Equal(2m, need.CoveredByCarryForward);
        Assert.Equal(4m, need.AdditionalFundingRequired);
        Assert.Equal(0m, need.ForecastCarryForward);
    }

    [Fact]
    public async Task MP004_Overfund_CarryTwo_RequestSix_AddTen_ForecastSix()
    {
        var (context, _, _, service) = CreateService();

        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C First Aid", Client = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 2m);

        var need = await service.CalculateFundingAsync(pool.Id, 6m, 10m);
        Assert.Equal(2m, need.CoveredByCarryForward);
        Assert.Equal(4m, need.AdditionalFundingRequired); // the immediate requirement is still 4 before the 10 are added
        Assert.Equal(6m, need.ForecastCarryForward);
    }

    [Fact]
    public async Task ReserveRelease_ReplenishesUnassigned()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 5m);

        var student = new Student { FirstName = "S", LastName = "A", Email = "s@example.com" };
        context.Students.Add(student);
        var alloc = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, alloc.Id, 1m);
        var reserved = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, reserved.ReservedToNamedStudents);
        Assert.Equal(4m, reserved.UnassignedCarryForward);

        await service.ReleasePlaceAsync(pool.Id, alloc.Id, 1m);
        var released = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(0m, released.ReservedToNamedStudents);
        Assert.Equal(5m, released.UnassignedCarryForward);
    }

    [Fact]
    public async Task MP006_CourseRestriction_BlocksWrongCourse()
    {
        var (context, gen, _, service) = CreateService();

        var firstAid = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid", Category = "First Aid" };
        var gasTest = new CourseDefinition { CourseCode = "GAS", CourseTitle = "Gas Test", Category = "Gas" };
        context.CourseDefinitions.AddRange(firstAid, gasTest);
        var delivery = new CourseDelivery { CourseDefinitionId = gasTest.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool
        {
            Name = "T&C First Aid",
            Client = "T&C",
            RestrictedToCourseDefinitionId = firstAid.Id
        });

        var student = new Student { FirstName = "S", LastName = "A", Email = "s@example.com" };
        context.Students.Add(student);
        var alloc = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReservePlaceAsync(pool.Id, alloc.Id, 1m));
    }

    [Fact]
    public async Task Transfer_UnassignedBetweenPools()
    {
        var (context, _, _, service) = CreateService();

        var source = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "Source" });
        var target = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "Target" });
        await service.AddPrepaidPlacesAsync(source.Id, 3m);

        await service.TransferPlaceAsync(source.Id, target.Id, 2m);

        var sourcePosition = await service.GetPoolPositionAsync(source.Id);
        var targetPosition = await service.GetPoolPositionAsync(target.Id);

        Assert.Equal(1m, sourcePosition.UnassignedCarryForward);
        Assert.Equal(2m, targetPosition.UnassignedCarryForward);
    }

    [Fact]
    public async Task InsufficientUnassigned_ReserveBlocked()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 1m);

        var s1 = new Student { FirstName = "A", LastName = "A", Email = "a@example.com" };
        var s2 = new Student { FirstName = "B", LastName = "B", Email = "b@example.com" };
        context.Students.AddRange(s1, s2);
        var a1 = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = s1.Id };
        var a2 = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = s2.Id };
        context.Allocations.AddRange(a1, a2);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, a1.Id, 1m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReservePlaceAsync(pool.Id, a2.Id, 1m));
    }
}
