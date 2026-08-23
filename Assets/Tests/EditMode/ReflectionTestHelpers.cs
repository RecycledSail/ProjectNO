using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class ReflectionTestHelpers
{
    public static Type Find(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Missing runtime type {name}");
        return type;
    }

    public static object New(string name, params object[] args) =>
        Activator.CreateInstance(Find(name), args);

    public static object Get(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public);
        if (property != null) return property.GetValue(instance);
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Missing member {name} on {type.Name}");
        return field.GetValue(instance);
    }

    public static void Set(object instance, string name, object value)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public);
        if (property != null) { property.SetValue(instance, value); return; }
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Missing member {name} on {type.Name}");
        field.SetValue(instance, value);
    }

    public static T Call<T>(object instance, string name, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public)
            .Single(candidate => candidate.Name == name &&
                                 candidate.GetParameters().Length == args.Length);
        return (T)method.Invoke(instance, args);
    }
}

public static class TestEconomyFactory
{
    public static object NewNation(string name, long openingBalance)
    {
        object researches = Activator.CreateInstance(
            typeof(List<>).MakeGenericType(ReflectionTestHelpers.Find("ResearchNode")));
        return Activator.CreateInstance(ReflectionTestHelpers.Find("Nation"),
            new[] { (object)1, name, researches, openingBalance });
    }

    public static object NewProvince(int id, string name) =>
        Activator.CreateInstance(ReflectionTestHelpers.Find("Province"),
            id, name, Enum.Parse(ReflectionTestHelpers.Find("Topography"), "Plane"));

    public static object AddPop(object province, long property, double livingStandard)
    {
        object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
        ReflectionTestHelpers.Set(species, "name", "Human");
        object culture = ReflectionTestHelpers.New("Culture", "TestCulture");
        object group = ReflectionTestHelpers.New("EthnicGroup", species, culture);
        object pop = ReflectionTestHelpers.New("ProvinceEthnicPop", province, group,
            new List<int> { 0, 100, 0, 0 }, property, livingStandard);
        ((IList)ReflectionTestHelpers.Get(province, "provinceEthnicPops")).Add(pop);
        ReflectionTestHelpers.Call<object>(province, "InitializePopulation");
        return pop;
    }

    public static object AddBuilding(
        object province, string typeName, int level, long initialCapital)
    {
        object type = ReflectionTestHelpers.New("BuildingType", typeName);
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", typeName);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
        FieldInfo recipes = ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static);
        ((IDictionary)recipes.GetValue(null))[typeName] = recipe;
        MethodInfo create = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create");
        object building = create.Invoke(null, new[] { type, province, (object)level, 0L });
        ((IDictionary)ReflectionTestHelpers.Get(province, "buildings"))[type] = building;
        return recipe;
    }

    public static object GetOnlyBuilding(object province) =>
        ((IDictionary)ReflectionTestHelpers.Get(province, "buildings")).Values
            .Cast<object>().Single();

    public static object ListOf(string runtimeType, params object[] values)
    {
        IList list = (IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(ReflectionTestHelpers.Find(runtimeType)));
        foreach (object value in values) list.Add(value);
        return list;
    }
}
