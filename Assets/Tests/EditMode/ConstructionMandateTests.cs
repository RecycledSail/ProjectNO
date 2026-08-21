using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ConstructionMandateTests
{
    [SetUp]
    public void ClearConstructionGlobals()
    {
        GetStaticDictionary("BUILDING_RECIPE").Clear();
        GetStaticDictionary("ADJACENT_PROVINCES").Clear();
    }

    [Test]
    public void AssignedMandate_CompletesAndIncrementsBuildingLevelOnce()
    {
        object buildingType = New("BuildingType", "WheatField");
        object target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        object companyType = New("BuildingType", "construcntionCompany");
        object companyProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        object company = New("ConstructionCompanyBuilding", companyType, companyProvince, 1);
        object mandate = New("ConstructionMandate", null, buildingType, target, 10d);

        Assert.That(Invoke<bool>(company, "TryAssign", mandate), Is.True);

        Invoke(company, "ProgressWeekly", 10d);

        Assert.That(GetProperty(mandate, "Status").ToString(), Is.EqualTo("Completed"));
        IDictionary buildings = (IDictionary)GetMember(target, "buildings");
        object completedBuilding = buildings[buildingType];
        Assert.That((int)GetMember(completedBuilding, "level"), Is.EqualTo(1));

        Invoke(company, "ProgressWeekly", 10d);
        Assert.That((int)GetMember(completedBuilding, "level"), Is.EqualTo(1));
    }

    [Test]
    public void CompletedMandate_ReleasesCompanySlot()
    {
        object buildingType = New("BuildingType", "WheatField");
        object target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        object companyType = New("BuildingType", "construcntionCompany");
        object companyProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        object company = New("ConstructionCompanyBuilding", companyType, companyProvince, 1);
        object mandate = New("ConstructionMandate", null, buildingType, target, 10d);

        Assert.That(Invoke<bool>(company, "TryAssign", mandate), Is.True);
        Invoke(company, "ProgressWeekly", 10d);

        ICollection activeProjects = (ICollection)GetProperty(company, "ActiveProjects");
        Assert.That(activeProjects.Count, Is.EqualTo(0));
        Assert.That((bool)GetProperty(company, "HasFreeSlot"), Is.True);
    }

    [Test]
    public void CancelledMandate_RejectsProgressAndReleasesCompanySlot()
    {
        object buildingType = New("BuildingType", "WheatField");
        object target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        object companyType = New("BuildingType", "construcntionCompany");
        object companyProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        object company = New("ConstructionCompanyBuilding", companyType, companyProvince, 1);
        object mandate = New("ConstructionMandate", null, buildingType, target, 10d);

        Assert.That(Invoke<bool>(company, "TryAssign", mandate), Is.True);
        Assert.That(Invoke<bool>(mandate, "Cancel"), Is.True);

        Invoke(company, "ProgressWeekly", 10d);

        Assert.That(GetProperty(mandate, "Status").ToString(), Is.EqualTo("Cancelled"));
        Assert.That((double)GetProperty(mandate, "RemainingManhours"), Is.EqualTo(10d));
        Assert.That(((ICollection)GetProperty(company, "ActiveProjects")).Count, Is.EqualTo(0));
        Assert.That(((IDictionary)GetMember(target, "buildings")).Count, Is.EqualTo(0));
    }

    [Test]
    public void NationPlacesOneMandate_AndCompanyReferencesSameObject()
    {
        object nation = NewNation("Nation1");
        object target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        object builderProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        Invoke<bool>(nation, "AddProvinces", target);
        Invoke<bool>(nation, "AddProvinces", builderProvince);

        object companyType = New("BuildingType", "construcntionCompany");
        object company = New("ConstructionCompanyBuilding", companyType, builderProvince, 1);
        ((IDictionary)GetMember(builderProvince, "buildings"))[companyType] = company;

        object buildingType = New("BuildingType", "WheatField");
        ConfigureRecipeAndAdjacency(buildingType, target, builderProvince, 20);

        object mandate = Invoke<object>(nation, "PlaceConstructionMandate", buildingType, target);

        Assert.That(mandate, Is.Not.Null);
        IList nationMandates = (IList)GetProperty(nation, "ConstructionMandates");
        IList companyProjects = (IList)GetProperty(company, "ActiveProjects");
        Assert.That(nationMandates.Count, Is.EqualTo(1));
        Assert.That(companyProjects.Count, Is.EqualTo(1));
        Assert.That(ReferenceEquals(mandate, companyProjects[0]), Is.True);
        Assert.That(Invoke<object>(nation, "PlaceConstructionMandate", buildingType, target), Is.Null);
        Assert.That(nationMandates.Count, Is.EqualTo(1));
    }

    [Test]
    public void RequestedMandate_IsAssignedWhenCompanyBecomesAvailable()
    {
        object nation = NewNation("Nation1");
        object target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        object builderProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        Invoke<bool>(nation, "AddProvinces", target);
        Invoke<bool>(nation, "AddProvinces", builderProvince);

        object buildingType = New("BuildingType", "WheatField");
        ConfigureRecipeAndAdjacency(buildingType, target, null, 20);
        object mandate = Invoke<object>(nation, "PlaceConstructionMandate", buildingType, target);

        Assert.That(GetProperty(mandate, "Status").ToString(), Is.EqualTo("Requested"));

        object companyType = New("BuildingType", "construcntionCompany");
        object company = New("ConstructionCompanyBuilding", companyType, builderProvince, 1);
        ((IDictionary)GetMember(builderProvince, "buildings"))[companyType] = company;
        ConfigureRecipeAndAdjacency(buildingType, target, builderProvince, 20);

        Invoke(nation, "RetryPendingConstructionMandates");

        Assert.That(GetProperty(mandate, "Status").ToString(), Is.EqualTo("Assigned"));
        Assert.That(ReferenceEquals(mandate,
            ((IList)GetProperty(company, "ActiveProjects"))[0]), Is.True);
    }

    [Test]
    public void BuildingFactory_CreatesConstructionCompanySubtype()
    {
        object companyType = New("BuildingType", "construcntionCompany");
        object province = New("Province", 1, "Bebino", EnumValue("Topography", "Plane"));
        MethodInfo create = Find("BuildingFactory").GetMethod(
            "Create", BindingFlags.Public | BindingFlags.Static);

        object building = create.Invoke(null,
            new[] { companyType, province, (object)1, 0L });

        Assert.That(building.GetType().Name, Is.EqualTo("ConstructionCompanyBuilding"));
        Assert.That((int)GetMember(building, "level"), Is.EqualTo(1));
    }

    [Test]
    public void ProvinceData_SeedsOneLevelOneCompanyInBebino()
    {
        string path = System.IO.Path.Combine(
            UnityEngine.Application.dataPath,
            "Resources",
            "Provinces.json");
        ProvinceJson wrapper = UnityEngine.JsonUtility.FromJson<ProvinceJson>(
            System.IO.File.ReadAllText(path));
        ProvinceJsonData bebino = wrapper.provinces
            .Single(province => province.name == "Bebino");

        Assert.That(bebino.buildings.Count(building =>
            building.buildingTypeName == "construcntionCompany" &&
            building.level == 1),
            Is.EqualTo(1));
    }

    [Serializable]
    private sealed class ProvinceJson
    {
        public ProvinceJsonData[] provinces;
    }

    [Serializable]
    private sealed class ProvinceJsonData
    {
        public string name;
        public BuildingJsonData[] buildings;
    }

    [Serializable]
    private sealed class BuildingJsonData
    {
        public string buildingTypeName;
        public int level;
    }

    private static object New(string typeName, params object[] arguments)
    {
        Type type = Find(typeName);
        return Activator.CreateInstance(type, arguments);
    }

    private static object EnumValue(string typeName, string value)
    {
        return Enum.Parse(Find(typeName), value);
    }

    private static object NewNation(string name)
    {
        Type researchType = Find("ResearchNode");
        object researches = Activator.CreateInstance(
            typeof(List<>).MakeGenericType(researchType));
        return Activator.CreateInstance(Find("Nation"),
            new[] { (object)1, name, researches });
    }

    private static void ConfigureRecipeAndAdjacency(
        object buildingType,
        object target,
        object builderProvince,
        int timeToBuild)
    {
        string buildingTypeName = (string)GetMember(buildingType, "name");
        object recipe = New("BuildingRecipe", buildingTypeName);
        SetProperty(recipe, "TimeToBuild", timeToBuild);
        GetStaticDictionary("BUILDING_RECIPE")[buildingTypeName] = recipe;

        IList neighbors = (IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(Find("Province")));
        if (builderProvince != null)
            neighbors.Add(builderProvince);
        string targetName = (string)GetMember(target, "name");
        GetStaticDictionary("ADJACENT_PROVINCES")[targetName] = neighbors;
    }

    private static IDictionary GetStaticDictionary(string fieldName)
    {
        FieldInfo field = Find("GlobalVariables").GetField(
            fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"Could not find GlobalVariables.{fieldName}");
        return (IDictionary)field.GetValue(null);
    }

    private static Type Find(string typeName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Could not find loaded type: {typeName}");
        return type;
    }

    private static object GetMember(object instance, string name)
    {
        Type type = instance.GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field != null) return field.GetValue(instance);
        return GetProperty(instance, name);
    }

    private static object GetProperty(object instance, string name)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null,
            $"Could not find property {name} on {instance.GetType().Name}");
        return property.GetValue(instance);
    }

    private static void SetProperty(object instance, string name, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null,
            $"Could not find property {name} on {instance.GetType().Name}");
        property.SetValue(instance, value);
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = FindMethod(instance, methodName, arguments.Length);
        method.Invoke(instance, arguments);
    }

    private static T Invoke<T>(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = FindMethod(instance, methodName, arguments.Length);
        return (T)method.Invoke(instance, arguments);
    }

    private static MethodInfo FindMethod(object instance, string methodName, int argumentCount)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(candidate => candidate.Name == methodName &&
                                          candidate.GetParameters().Length == argumentCount);
        Assert.That(method, Is.Not.Null,
            $"Could not find method {methodName} on {instance.GetType().Name}");
        return method;
    }
}
