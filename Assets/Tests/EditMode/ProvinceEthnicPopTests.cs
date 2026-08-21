using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class ProvinceEthnicPopTests
{
    [Test]
    public void EmployablePopulation_AppliesParticipationRateForEveryAgeGroup()
    {
        Type popType = FindLoadedType("ProvinceEthnicPop");
        Type provinceType = FindLoadedType("Province");
        Type ethnicGroupType = FindLoadedType("EthnicGroup");
        object pop = popType
            .GetConstructor(new[] { provinceType, ethnicGroupType, typeof(List<int>) })
            .Invoke(new object[] { null, null, new List<int> { 5000, 3000, 2000, 1000 } });

        var employablePopulationProperty = popType.GetProperty("EmployablePopulation");

        Assert.That(
            employablePopulationProperty,
            Is.Not.Null,
            "ProvinceEthnicPop must expose its coefficient-adjusted employable population.");
        Assert.That(employablePopulationProperty.GetValue(pop), Is.EqualTo(5950L));
    }

    private static Type FindLoadedType(string typeName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(candidate => candidate != null);

        Assert.That(type, Is.Not.Null, $"Could not find loaded type: {typeName}");
        return type;
    }
}
