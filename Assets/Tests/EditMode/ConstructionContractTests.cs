using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ConstructionContractTests
{
    private const string TestRecipeName = "ConstructionContractTestRecipe";

    [TearDown]
    public void RemoveLoadedTestRecipe()
    {
        Recipes().Remove(TestRecipeName);
    }

    [Test]
    public void NewRecipe_DefaultsToThirtyPercentStartThreshold()
    {
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", "Test");

        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(3000));
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(0L));
    }

    [Test]
    public void LoadBuildingRecipes_MapsExplicitConstructionContractSettings()
    {
        LoadRecipe(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10," +
            "\"constructionFee\":25,\"startMaterialBasisPoints\":4500}]}");

        object recipe = Recipes()[TestRecipeName];
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(25L));
        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(4500));
    }

    [Test]
    public void LoadBuildingRecipes_MissingConstructionSettingsUseCompatibleDefaults()
    {
        LoadRecipe(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10}]}");

        object recipe = Recipes()[TestRecipeName];
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(0L));
        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(3000));
    }

    [Test]
    public void LoadBuildingRecipes_NegativeConstructionFeeIsRejectedBeforeRegistration()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10,\"constructionFee\":-1}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("constructionFee"));
        Assert.That(Recipes().Contains(TestRecipeName), Is.False);
    }

    [TestCase(0)]
    [TestCase(10001)]
    public void LoadBuildingRecipes_StartMaterialThresholdOutsideContractRangeIsRejected(
        int basisPoints)
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            $"\"TimeToBuild\":4,\"initialCapital\":10," +
            $"\"startMaterialBasisPoints\":{basisPoints}}}]}}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("startMaterialBasisPoints"));
        Assert.That(Recipes().Contains(TestRecipeName), Is.False);
    }

    [Test]
    public void LoadBuildingRecipes_NonpositiveRequiredMaterialIsRejected()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":0}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("Wood"));
        Assert.That(exception.Message, Does.Contain("amount"));
    }

    [Test]
    public void LoadBuildingRecipes_NonpositiveManhoursIsRejected()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[],\"TimeToBuild\":0,\"initialCapital\":10}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("TimeToBuild"));
    }

    [Test]
    public void ResourceRecipes_UsePositiveAuthoredFeesMatchingInitialTuning()
    {
        RecipeWrapper data = JsonUtility.FromJson<RecipeWrapper>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Resources", "BuildingRecipes.json")));

        Assert.That(data.buildingrecipes, Is.Not.Empty);
        Assert.That(data.buildingrecipes, Has.All.Matches<RecipeData>(recipe =>
            recipe.constructionFee > 0 && recipe.constructionFee == recipe.TimeToBuild));
    }

    private static void LoadRecipe(string json)
    {
        Type wrapperType = ReflectionTestHelpers.Find(
            "GlobalVariables+GameDataFormat+BuildingrecipesWrapper");
        object data = JsonUtility.FromJson(json, wrapperType);
        MethodInfo loader = ReflectionTestHelpers.Find("GlobalVariables").GetMethod(
            "LoadBuildingRecipes",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { wrapperType },
            null);
        Assert.That(loader, Is.Not.Null);
        loader.Invoke(null, new[] { data });
    }

    private static InvalidOperationException AssertRecipeRejected(string json)
    {
        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => LoadRecipe(json));
        Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
        return (InvalidOperationException)invocation.InnerException;
    }

    private static IDictionary Recipes() =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    [Serializable]
    private sealed class RecipeWrapper
    {
        public RecipeData[] buildingrecipes;
    }

    [Serializable]
    private sealed class RecipeData
    {
        public int TimeToBuild;
        public long constructionFee;
    }
}
