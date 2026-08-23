using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ProportionalAllocatorTests
{
    [Test]
    public void Allocate_UsesWeightsAndStableRemainders()
    {
        Type allocator = ReflectionTestHelpers.Find("ProportionalAllocator");
        MethodInfo method = allocator.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "Allocate")
            .MakeGenericMethod(typeof(string));
        var weights = new Dictionary<string, long> { ["B"] = 1, ["A"] = 1 };
        var result = (Dictionary<string, long>)method.Invoke(null,
            new object[] { 3L, weights, (Func<string, string>)(value => value) });

        Assert.That(result["A"], Is.EqualTo(2L));
        Assert.That(result["B"], Is.EqualTo(1L));
        Assert.That(result.Values.Sum(), Is.EqualTo(3L));
    }

    [Test]
    public void Allocate_ReturnsZeroSharesWhenTotalOrWeightIsZero()
    {
        Type allocator = ReflectionTestHelpers.Find("ProportionalAllocator");
        MethodInfo method = allocator.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "Allocate")
            .MakeGenericMethod(typeof(string));
        var weights = new Dictionary<string, long> { ["A"] = 3, ["B"] = 0 };

        var zeroTotal = (Dictionary<string, long>)method.Invoke(null,
            new object[] { 0L, weights, (Func<string, string>)(value => value) });
        var zeroWeights = (Dictionary<string, long>)method.Invoke(null,
            new object[] { 5L, new Dictionary<string, long> { ["A"] = 0L },
                (Func<string, string>)(value => value) });

        Assert.That(zeroTotal.Values, Is.All.EqualTo(0L));
        Assert.That(zeroWeights["A"], Is.EqualTo(0L));
    }
}
