using System;
using System.IO;
using Prismica.Core.Components;
using Xunit;

namespace Prismica.Core.Tests.Components;

public sealed class ComponentLibraryTests : IDisposable
{
    private readonly string _testDir;

    public ComponentLibraryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"prismica-cl-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public void GetAvailableComponents_EmptyDir_ReturnsEmpty()
    {
        var lib = new ComponentLibrary(_testDir);
        var result = lib.GetAvailableComponents();
        Assert.Empty(result);
    }

    [Fact]
    public void GetAvailableComponents_WithPriFiles_ReturnsComponents()
    {
        File.WriteAllText(Path.Combine(_testDir, "Test.pri"), @"
[Prismica]
Version=0.1
Name=TestComponent
Author=Test
Description=A test component
Width=200
Height=100

[MeterText]
Meter=String
Text=Hello
X=0 Y=0 W=200 H=50
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.GetAvailableComponents();

        Assert.Single(result);
        Assert.Equal("TestComponent", result[0].Name);
        Assert.Equal("Test", result[0].Author);
        Assert.Equal("A test component", result[0].Description);
        Assert.Equal(200, result[0].DefaultWidth);
        Assert.Equal(100, result[0].DefaultHeight);
    }

    [Fact]
    public void FindComponent_ExistingName_ReturnsComponent()
    {
        File.WriteAllText(Path.Combine(_testDir, "MyWidget.pri"), @"
[Prismica]
Version=0.1
Name=MyWidget
Width=300
Height=200
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.FindComponent("MyWidget");

        Assert.NotNull(result);
        Assert.Equal("MyWidget", result!.Name);
    }

    [Fact]
    public void FindComponent_NonExistingName_ReturnsNull()
    {
        var lib = new ComponentLibrary(_testDir);
        var result = lib.FindComponent("NonExistent");
        Assert.Null(result);
    }

    [Fact]
    public void FindComponent_CaseInsensitive_ReturnsComponent()
    {
        File.WriteAllText(Path.Combine(_testDir, "Widget.pri"), @"
[Prismica]
Version=0.1
Name=Widget
Width=100
Height=100
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.FindComponent("widget");
        Assert.NotNull(result);
    }

    [Fact]
    public void GetFilePath_ReturnsCorrectPath()
    {
        var lib = new ComponentLibrary(_testDir);
        var path = lib.GetFilePath("Test");
        Assert.Equal(Path.Combine(_testDir, "Test.pri"), path);
    }

    [Fact]
    public void GetAvailableComponents_SkipsInvalidFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "Empty.pri"), "");
        File.WriteAllText(Path.Combine(_testDir, "Valid.pri"), @"
[Prismica]
Version=0.1
Name=ValidComponent
Width=100
Height=100
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.GetAvailableComponents();

        // Empty file has no Prismica section, so no Name parsed → skipped
        Assert.Single(result);
        Assert.Equal("ValidComponent", result[0].Name);
    }

    [Fact]
    public void GetAvailableComponents_SortsByName()
    {
        File.WriteAllText(Path.Combine(_testDir, "Zebra.pri"), @"
[Prismica]
Version=0.1
Name=Zebra
Width=100
Height=100
");
        File.WriteAllText(Path.Combine(_testDir, "Alpha.pri"), @"
[Prismica]
Version=0.1
Name=Alpha
Width=100
Height=100
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.GetAvailableComponents();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Zebra", result[1].Name);
    }

    [Fact]
    public void GetAvailableComponents_OnlyScansPriFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "Test.txt"), "not a pri");
        File.WriteAllText(Path.Combine(_testDir, "Test.pri"), @"
[Prismica]
Version=0.1
Name=TestComponent
Width=100
Height=100
");

        var lib = new ComponentLibrary(_testDir);
        var result = lib.GetAvailableComponents();

        Assert.Single(result);
    }
}
