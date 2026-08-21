using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class ProvinceEthnicPopTests
{
    [Test]
    public void EmployablePopulation_AppliesParticipationRateForEveryAgeGroup()
    {
        object pop = CreatePop(new List<int> { 5000, 3000, 2000, 1000 });
        Type popType = pop.GetType();

        var employablePopulationProperty = popType.GetProperty("EmployablePopulation");

        Assert.That(
            employablePopulationProperty,
            Is.Not.Null,
            "ProvinceEthnicPop must expose its coefficient-adjusted employable population.");
        Assert.That(employablePopulationProperty.GetValue(pop), Is.EqualTo(5950L));
    }

    [Test]
    public void AdvanceAgeGroupsOneYear_MovesOnlyOriginalPopulationToNextGroup()
    {
        object pop = CreatePop(new List<int> { 1000, 1000, 1000, 1000 });
        var advanceMethod = pop.GetType().GetMethod("AdvanceAgeGroupsOneYear");

        Assert.That(advanceMethod, Is.Not.Null, "ProvinceEthnicPop must support annual age transitions.");
        advanceMethod.Invoke(pop, null);

        Assert.That(GetAgePopulations(pop), Is.EqualTo(new long[] { 950, 1010, 1010, 1030 }));
        Assert.That(GetPopulation(pop), Is.EqualTo(4000L));
    }

    [Test]
    public void PopulationGrowth_ScalesAgeGroupsToMatchTheNewTotal()
    {
        object ethnicGroup = CreateEthnicGroup(0.1);
        object pop = CreatePop(new List<int> { 10, 20, 30, 40 }, ethnicGroup);

        pop.GetType().GetMethod("PopulationGrowth").Invoke(pop, null);

        Assert.That(GetAgePopulations(pop), Is.EqualTo(new long[] { 11, 22, 33, 44 }));
        Assert.That(GetPopulation(pop), Is.EqualTo(110L));
    }

    [Test]
    public void AdvanceDay_WhenYearChanges_AdvancesEveryProvinceAgeGroup()
    {
        Type gameManagerType = FindLoadedType("GameManager");
        Type provinceType = FindLoadedType("Province");
        Type topographyType = FindLoadedType("Topography");
        var gameObject = new GameObject("GameManager annual population test");

        try
        {
            object gameManager = gameObject.AddComponent(gameManagerType);
            object province = provinceType
                .GetConstructor(new[] { typeof(int), typeof(string), topographyType })
                .Invoke(new[] { (object)1, "TestProvince", Enum.Parse(topographyType, "Plane") });
            object pop = CreatePop(new List<int> { 1000, 1000, 1000, 1000 }, province: province);
            var provincePops = (IList)provinceType.GetProperty("provinceEthnicPops").GetValue(province);
            provincePops.Add(pop);
            provinceType.GetMethod("InitializePopulation").Invoke(province, null);

            var provinces = (IDictionary)gameManagerType.GetField("provinces").GetValue(gameManager);
            provinces.Add("TestProvince", province);
            gameManagerType.GetProperty("year").SetValue(gameManager, 1836);
            gameManagerType.GetProperty("month").SetValue(gameManager, 12);
            gameManagerType.GetProperty("day").SetValue(gameManager, 31);

            gameManagerType.GetMethod("AdvanceDay").Invoke(gameManager, null);

            Assert.That(GetAgePopulations(pop), Is.EqualTo(new long[] { 950, 1010, 1010, 1030 }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static object CreatePop(List<int> populationCounts, object ethnicGroup = null, object province = null)
    {
        Type popType = FindLoadedType("ProvinceEthnicPop");
        Type provinceType = FindLoadedType("Province");
        Type ethnicGroupType = FindLoadedType("EthnicGroup");
        return popType
            .GetConstructor(new[] { provinceType, ethnicGroupType, typeof(List<int>) })
            .Invoke(new[] { province, ethnicGroup, populationCounts });
    }

    private static object CreateEthnicGroup(double birthRate)
    {
        Type speciesType = FindLoadedType("SpeciesSpec");
        Type cultureType = FindLoadedType("Culture");
        Type ethnicGroupType = FindLoadedType("EthnicGroup");
        object species = Activator.CreateInstance(speciesType);
        speciesType.GetField("baseBirthRate").SetValue(species, birthRate);
        object culture = Activator.CreateInstance(cultureType, "TestCulture");
        return Activator.CreateInstance(ethnicGroupType, species, culture);
    }

    private static long[] GetAgePopulations(object pop)
    {
        var ageGroups = (IEnumerable)pop.GetType().GetField("ageGroups").GetValue(pop);
        return ageGroups.Cast<object>()
            .Select(ageGroup => (long)ageGroup.GetType().GetField("agepopulation").GetValue(ageGroup))
            .ToArray();
    }

    private static long GetPopulation(object pop)
    {
        return (long)pop.GetType().GetField("population").GetValue(pop);
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
