using System;
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
