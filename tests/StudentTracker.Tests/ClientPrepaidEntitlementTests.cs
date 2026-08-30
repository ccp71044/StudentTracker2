using StudentTracker.Core.Enums;
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

    [Fact]
    public async Task MP001_MultiplePools_RemainIndependent()
    {
        var (context, _, _, service) = CreateService();

        var poolA = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "A" });
        var poolB = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "B" });

        await service.AddPrepaidPlacesAsync(poolA.Id, 10m);
        await service.AddPrepaidPlacesAsync(poolB.Id, 10m);

        var course = new CourseDefinition { CourseCode = "GAS", CourseTitle = "Gas" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var student = new Student { FirstName = "S", LastName = "A", Email = "s@example.com" };
        context.Students.Add(student);
        var alloc = new Allocation { DisplayId = "ALL-0001", CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        // consume 8 from pool A
        for (int i = 0; i < 8; i++)
        {
            var a = new Allocation { DisplayId = $"ALL-{i + 2:0000}", CourseDeliveryId = delivery.Id, StudentId = student.Id };
            context.Allocations.Add(a);
            context.SaveChanges();
            await service.ReservePlaceAsync(poolA.Id, a.Id, 1m);
            await service.AssignPlaceAsync(poolA.Id, a.Id, 1m);
            await service.ConsumePlaceAsync(poolA.Id, a.Id, 1m);
        }

        var posA = await service.GetPoolPositionAsync(poolA.Id);
        var posB = await service.GetPoolPositionAsync(poolB.Id);

        Assert.Equal(2m, posA.UnassignedCarryForward);
        Assert.Equal(10m, posB.UnassignedCarryForward);
    }

    [Fact]
    public async Task MP008_Placeholder_ReserveAssignAndPreserveHistory()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 3m);

        var alloc = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, PlaceholderName = "Placeholder 1" };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, alloc.Id, 1m);
        var reserved = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, reserved.ReservedPlaceholders);
        Assert.Equal(0m, reserved.ReservedToNamedStudents);

        await service.AssignPlaceAsync(pool.Id, alloc.Id, 1m);
        var assigned = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, assigned.ReservedPlaceholders);
        Assert.Equal(0m, assigned.ReservedToNamedStudents);
    }

    [Fact]
    public async Task MP010_ConsumeThenRelease_BlockedOrDoesNotReturn()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 2m);

        var student = new Student { FirstName = "S", LastName = "A", Email = "s@example.com" };
        context.Students.Add(student);
        var alloc = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, alloc.Id, 1m);
        await service.AssignPlaceAsync(pool.Id, alloc.Id, 1m);
        await service.ConsumePlaceAsync(pool.Id, alloc.Id, 1m);

        var before = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, before.PlacesConsumed);
        Assert.Equal(1m, before.UnassignedCarryForward);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReleasePlaceAsync(pool.Id, alloc.Id, 1m));
    }

    [Fact]
    public async Task MP013_AdjustmentAndReversal_RecalculatesBalances()
    {
        var (context, _, _, service) = CreateService();

        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 10m);

        var txAdjust = new ClientPrepaidEntitlementTransaction
        {
            DisplayId = "CPT-0002",
            PoolId = pool.Id,
            TransactionType = ClientPrepaidEntitlementTransactionType.PlaceAdjustment,
            Quantity = 2m,
            Reason = "Adjustment",
            TransactionDate = DateTime.UtcNow
        };
        context.ClientPrepaidEntitlementTransactions.Add(txAdjust);

        var txReverse = new ClientPrepaidEntitlementTransaction
        {
            DisplayId = "CPT-0003",
            PoolId = pool.Id,
            TransactionType = ClientPrepaidEntitlementTransactionType.PlaceReversal,
            Quantity = -5m,
            Reason = "Reversal",
            TransactionDate = DateTime.UtcNow
        };
        context.ClientPrepaidEntitlementTransactions.Add(txReverse);
        await context.SaveChangesAsync();

        var position = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(7m, position.PrepaidPlacesLoaded); // 10 + 2 - 5 = 7 net loaded
        Assert.Equal(7m, position.UnassignedCarryForward);
    }

    [Fact]
    public async Task WF001_FullClientPrepaidFlow()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C First Aid" });
        await service.AddPrepaidPlacesAsync(pool.Id, 2m);

        // Request for six with two carried forward, top-up four, then complete all six.
        var funding = await service.CalculateFundingAsync(pool.Id, 6m, 4m);
        Assert.Equal(4m, funding.AdditionalFundingRequired);
        Assert.Equal(2m, funding.CoveredByCarryForward);
        Assert.Equal(0m, funding.ForecastCarryForward);

        await service.AddPrepaidPlacesAsync(pool.Id, 4m);

        for (int i = 0; i < 6; i++)
        {
            var student = new Student { FirstName = $"S{i}", LastName = "A", Email = $"s{i}@example.com" };
            context.Students.Add(student);
            var alloc = new Allocation { DisplayId = $"ALL-{i + 1:0000}", CourseDeliveryId = delivery.Id, StudentId = student.Id };
            context.Allocations.Add(alloc);
            context.SaveChanges();

            await service.ReservePlaceAsync(pool.Id, alloc.Id, 1m);
            await service.AssignPlaceAsync(pool.Id, alloc.Id, 1m);
            await service.ConsumePlaceAsync(pool.Id, alloc.Id, 1m);
        }

        var position = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(6m, position.PrepaidPlacesLoaded);
        Assert.Equal(6m, position.PlacesConsumed);
        Assert.Equal(0m, position.UnassignedCarryForward);
    }

    [Fact]
    public async Task MP009_Release_ReplenishesUnassignedAndCanBeReassigned()
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
        var reserved = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(0m, reserved.UnassignedCarryForward);

        await service.ReleasePlaceAsync(pool.Id, a1.Id, 1m);
        var released = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, released.UnassignedCarryForward);

        await service.ReservePlaceAsync(pool.Id, a2.Id, 1m);
        var reassign = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(0m, reassign.UnassignedCarryForward);
        Assert.Equal(1m, reassign.ReservedToNamedStudents);
    }

    [Fact]
    public async Task WF002_ReserveReleaseAndReassign_FullPlaceholderToNamedFlow()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var pool = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "T&C" });
        await service.AddPrepaidPlacesAsync(pool.Id, 2m);

        // Reserve as placeholder for team A
        var placeholder = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, PlaceholderName = "Team A" };
        context.Allocations.Add(placeholder);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, placeholder.Id, 1m);
        var reserved = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, reserved.ReservedPlaceholders);

        // Release placeholder and reserve for named student instead
        await service.ReleasePlaceAsync(pool.Id, placeholder.Id, 1m);

        var s1 = new Student { FirstName = "A", LastName = "A", Email = "a@example.com" };
        context.Students.Add(s1);
        var named = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = s1.Id };
        context.Allocations.Add(named);
        context.SaveChanges();

        await service.ReservePlaceAsync(pool.Id, named.Id, 1m);
        await service.AssignPlaceAsync(pool.Id, named.Id, 1m);
        await service.ConsumePlaceAsync(pool.Id, named.Id, 1m);

        var final = await service.GetPoolPositionAsync(pool.Id);
        Assert.Equal(1m, final.PlacesConsumed);
        Assert.Equal(1m, final.UnassignedCarryForward);
        Assert.Equal(0m, final.ReservedPlaceholders);
        Assert.Equal(0m, final.ReservedToNamedStudents);
    }

    [Fact]
    public async Task WF008_TransferUnassignedBetweenPools_ThenReserveAndAssign()
    {
        var (context, gen, _, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var source = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "Source" });
        var target = await service.CreatePoolAsync(new ClientPrepaidPool { Name = "Target" });
        await service.AddPrepaidPlacesAsync(source.Id, 3m);

        // Transfer two unassigned places to the target pool
        await service.TransferPlaceAsync(source.Id, target.Id, 2m);
        var sourceAfter = await service.GetPoolPositionAsync(source.Id);
        var targetAfter = await service.GetPoolPositionAsync(target.Id);
        Assert.Equal(1m, sourceAfter.UnassignedCarryForward);
        Assert.Equal(2m, targetAfter.UnassignedCarryForward);

        // Reserve and assign in target pool
        var s1 = new Student { FirstName = "A", LastName = "A", Email = "a@example.com" };
        context.Students.Add(s1);
        var a1 = new Allocation { DisplayId = gen.NextDisplayId<Allocation>("ALL"), CourseDeliveryId = delivery.Id, StudentId = s1.Id };
        context.Allocations.Add(a1);
        context.SaveChanges();

        await service.ReservePlaceAsync(target.Id, a1.Id, 1m);
        await service.AssignPlaceAsync(target.Id, a1.Id, 1m);
        await service.ConsumePlaceAsync(target.Id, a1.Id, 1m);

        var final = await service.GetPoolPositionAsync(target.Id);
        Assert.Equal(1m, final.PlacesConsumed);
        Assert.Equal(1m, final.UnassignedCarryForward);
    }
}
