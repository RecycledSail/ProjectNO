using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ConstructionMandateTests
{
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

    private static object New(string typeName, params object[] arguments)
    {
        Type type = Find(typeName);
        return Activator.CreateInstance(type, arguments);
    }

    private static object EnumValue(string typeName, string value)
    {
        return Enum.Parse(Find(typeName), value);
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
