using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class BuildingInputInventoryTests
{
    private const string Iron = "building-input-inventory-test-iron";
    private const string Wood = "building-input-inventory-test-wood";

    [Test]
    public void PreparedInputReceipt_CommitsAllInputsOnlyAfterPreparation()
    {
        object building = CreateBuilding();

        Assert.That(TryPrepare(building, new Dictionary<string, long>
        {
            [Iron] = 20L,
            [Wood] = 5L,
        }, out object receipt), Is.True);

        Assert.That(Inventory(building), Is.Empty);
        Assert.That(((IDictionary)ReflectionTestHelpers.Get(building, "InputInventory")).IsReadOnly,
            Is.True);

        Commit(receipt);

        Assert.That(Inventory(building), Is.EqualTo(new Dictionary<string, long>
        {
            [Iron] = 20L,
            [Wood] = 5L,
        }));
    }

    [Test]
    public void PreparedInputReceipt_LatestPreparationOwnsVersionAndCommitNeverThrows()
    {
        object building = CreateBuilding();
        Assert.That(TryPrepare(building,
            new Dictionary<string, long> { [Iron] = 2L }, out object superseded), Is.True);
        Assert.That(TryPrepare(building,
            new Dictionary<string, long> { [Wood] = 3L }, out object current), Is.True);

        Assert.DoesNotThrow(() => Commit(superseded));
        Assert.That(Inventory(building), Is.Empty);
        Assert.DoesNotThrow(() => Commit(current));
        Assert.DoesNotThrow(() => Commit(current));

        Assert.That(Inventory(building), Is.EqualTo(new Dictionary<string, long>
        {
            [Wood] = 3L,
        }));
    }

    [Test]
    public void PreparedInputReceipt_OverflowFailsWithoutMutation()
    {
        object building = CreateBuilding();
        Restore(building, new Dictionary<string, long> { [Iron] = long.MaxValue });

        Assert.That(TryPrepare(building,
            new Dictionary<string, long> { [Iron] = 1L }, out object receipt), Is.False);

        Assert.That(receipt, Is.Null);
        Assert.That(Inventory(building), Is.EqualTo(new Dictionary<string, long>
        {
            [Iron] = long.MaxValue,
        }));
    }

    [TestCase("negative")]
    [TestCase("blank")]
    [TestCase("unknown")]
    public void RestoreInputInventory_RejectsInvalidEntriesWithoutPartialMutation(string invalid)
    {
        object building = CreateBuilding();
        Restore(building, new Dictionary<string, long> { [Iron] = 7L });
        var quantities = new Dictionary<string, long> { [Wood] = 3L };
        switch (invalid)
        {
            case "negative": quantities[Wood] = -1L; break;
            case "blank": quantities[" "] = 1L; break;
            case "unknown": quantities["building-input-inventory-test-unknown"] = 1L; break;
        }

        var exception = Assert.Throws<TargetInvocationException>(() => Restore(building, quantities));

        Assert.That(exception.InnerException, Is.InstanceOf<ArgumentException>());
        Assert.That(Inventory(building), Is.EqualTo(new Dictionary<string, long>
        {
            [Iron] = 7L,
        }));
    }

    [Test]
    public void RestoreInputInventory_KeepsOnlyPositiveQuantitiesAndInvalidatesPreparedReceipt()
    {
        object building = CreateBuilding();
        Assert.That(TryPrepare(building,
            new Dictionary<string, long> { [Iron] = 2L }, out object stale), Is.True);

        Restore(building, new Dictionary<string, long>
        {
            [Iron] = 0L,
            [Wood] = 4L,
        });

        Assert.DoesNotThrow(() => Commit(stale));
        Assert.That(Inventory(building), Is.EqualTo(new Dictionary<string, long>
        {
            [Wood] = 4L,
        }));
    }

    private static object CreateBuilding()
    {
        object type = ReflectionTestHelpers.New("BuildingType", "BuildingInputInventoryTestBuilding");
        ReflectionTestHelpers.Set(type, "requireItems", new Dictionary<string, int>
        {
            [Iron] = 1,
            [Wood] = 1,
        });
        object province = TestEconomyFactory.NewProvince(1, "BuildingInputInventoryTestProvince");
        return ReflectionTestHelpers.New("Building", type, province);
    }

    private static bool TryPrepare(
        object building,
        IReadOnlyDictionary<string, long> quantities,
        out object receipt)
    {
        object[] arguments = { quantities, null };
        MethodInfo method = building.GetType().GetMethod("TryPrepareInputReceipt");
        Assert.That(method, Is.Not.Null, "Missing Building.TryPrepareInputReceipt");
        bool success = (bool)method.Invoke(building, arguments);
        receipt = arguments[1];
        return success;
    }

    private static void Commit(object receipt) => ReflectionTestHelpers.Find("IPreparedMarketReceipt")
        .GetMethod("Commit").Invoke(receipt, null);

    private static void Restore(object building, IReadOnlyDictionary<string, long> quantities)
    {
        MethodInfo method = building.GetType().GetMethod("RestoreInputInventory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing Building.RestoreInputInventory");
        method.Invoke(building, new object[] { quantities });
    }

    private static Dictionary<string, long> Inventory(object building) =>
        ((IEnumerable)ReflectionTestHelpers.Get(building, "InputInventory"))
            .Cast<object>()
            .ToDictionary(
                entry => (string)ReflectionTestHelpers.Get(entry, "Key"),
                entry => (long)ReflectionTestHelpers.Get(entry, "Value"));
}
