using System;
using System.IO;
using Prismica.Core.Persistence;
using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.Persistence;

public sealed class IniLayoutSerializerTests
{
    private readonly IniLayoutSerializer _serializer = new();

    private (LayoutDocument doc, byte[] bytes) SerializeToBytes(LayoutDocument doc)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(doc, ms, LayoutFormat.Ini);
        return (doc, ms.ToArray());
    }

    [Fact]
    public void Serialize_And_Deserialize_RoundTrip()
    {
        var doc = new LayoutDocument(
            "0.1",
            new LayoutMetadata("Test", "Author", "Description", DateTime.UtcNow, DateTime.UtcNow, null),
            new[]
            {
                new ComponentInstance("inst1", "ClockCpu", new Rect(100, 200, 400, 300), 0, new Dictionary<string, object>(), true),
                new ComponentInstance("inst2", "Weather", new Rect(50, 50, 200, 150), 1, new Dictionary<string, object>(), true)
            });

        var (_, bytes) = SerializeToBytes(doc);
        using var stream = new MemoryStream(bytes);
        var loaded = _serializer.Deserialize(stream, LayoutFormat.Ini);

        Assert.Equal("0.1", loaded.Version);
        Assert.Equal("Test", loaded.Metadata.Name);
        Assert.Equal(2, loaded.Instances.Count);
        Assert.Equal("inst1", loaded.Instances[0].Id);
        Assert.Equal("ClockCpu", loaded.Instances[0].ComponentName);
        Assert.Equal(100, loaded.Instances[0].Bounds.X);
        Assert.Equal(200, loaded.Instances[0].Bounds.Y);
        Assert.Equal(400, loaded.Instances[0].Bounds.Width);
        Assert.Equal(300, loaded.Instances[0].Bounds.Height);
        Assert.Equal("inst2", loaded.Instances[1].Id);
        Assert.Equal("Weather", loaded.Instances[1].ComponentName);
    }

    [Fact]
    public void Serialize_EmptyInstances()
    {
        var doc = new LayoutDocument(
            "0.1",
            new LayoutMetadata("Empty", "", "", DateTime.UtcNow, DateTime.UtcNow, null),
            Array.Empty<ComponentInstance>());

        var (_, bytes) = SerializeToBytes(doc);
        using var stream = new MemoryStream(bytes);
        var loaded = _serializer.Deserialize(stream, LayoutFormat.Ini);

        Assert.Empty(loaded.Instances);
    }

    [Fact]
    public void Deserialize_InvalidContent_ReturnsEmptyDoc()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("this is not valid layout content");
        using var stream = new MemoryStream(bytes);

        var loaded = _serializer.Deserialize(stream, LayoutFormat.Ini);
        Assert.NotNull(loaded);
        Assert.Empty(loaded.Instances);
    }

    [Fact]
    public void Serialize_DisabledInstance()
    {
        var doc = new LayoutDocument(
            "0.1",
            new LayoutMetadata("Test", "", "", DateTime.UtcNow, DateTime.UtcNow, null),
            new[]
            {
                new ComponentInstance("inst1", "Clock", new Rect(0, 0, 100, 100), 0, new Dictionary<string, object>(), false)
            });

        var (_, bytes) = SerializeToBytes(doc);
        using var stream = new MemoryStream(bytes);
        var loaded = _serializer.Deserialize(stream, LayoutFormat.Ini);

        Assert.Single(loaded.Instances);
        Assert.False(loaded.Instances[0].Enabled);
    }
}
