using System.IO;
using System.Runtime.InteropServices;
using TextRecast.Infrastructure.Hardware;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class HardwareInspectorTests
{
    [TestMethod]
    public void InspectMapsKnownHardwareSignals()
    {
        var probe = new StubHardwareProbe
        {
            Memory = new PhysicalMemorySnapshot(16_000, 8_000),
            LogicalProcessors = 12,
            Architecture = Architecture.X64,
            SupportsAvx2 = true,
            Storage = new StorageSnapshot(40_000, @"D:\")
        };

        var profile = new HardwareInspector(probe).Inspect(@"D:\Models");

        Assert.AreEqual(16_000, profile.TotalPhysicalMemoryBytes);
        Assert.AreEqual(8_000, profile.AvailablePhysicalMemoryBytes);
        Assert.AreEqual(12, profile.LogicalProcessorCount);
        Assert.AreEqual(Architecture.X64, profile.ProcessArchitecture);
        Assert.IsTrue(profile.SupportsAvx2);
        Assert.AreEqual(40_000, profile.AvailableModelStorageBytes);
        Assert.AreEqual(@"D:\", profile.ModelStorageRoot);
        Assert.AreEqual(@"D:\Models", probe.RequestedStoragePath);
    }

    [TestMethod]
    public void InspectAllowsExhaustedButAvailableMemoryAndStorageSignals()
    {
        var probe = new StubHardwareProbe
        {
            Memory = new PhysicalMemorySnapshot(8_000, 0),
            Storage = new StorageSnapshot(0, @"C:\")
        };

        var profile = new HardwareInspector(probe).Inspect(@"C:\Models");

        Assert.AreEqual(0, profile.AvailablePhysicalMemoryBytes);
        Assert.AreEqual(0, profile.AvailableModelStorageBytes);
    }

    [TestMethod]
    public void InspectRejectsUnavailableHardwareSignals()
    {
        var missingMemory = new StubHardwareProbe
        {
            Memory = new PhysicalMemorySnapshot(0, 0)
        };
        var missingProcessors = new StubHardwareProbe
        {
            LogicalProcessors = 0
        };
        var missingStorage = new StubHardwareProbe
        {
            Storage = new StorageSnapshot(-1, string.Empty)
        };

        StringAssert.Contains(
            Assert.ThrowsExactly<HardwareInspectionException>(
                () => new HardwareInspector(missingMemory).Inspect(@"C:\Models")).Message,
            "total physical memory");
        StringAssert.Contains(
            Assert.ThrowsExactly<HardwareInspectionException>(
                () => new HardwareInspector(missingProcessors).Inspect(@"C:\Models")).Message,
            "processor count");
        StringAssert.Contains(
            Assert.ThrowsExactly<HardwareInspectionException>(
                () => new HardwareInspector(missingStorage).Inspect(@"C:\Models")).Message,
            "model directory");
    }

    [TestMethod]
    public void ResolveStorageRootUsesTheModelDirectoryDrive()
    {
        var modelDirectory = Path.Combine(Path.GetTempPath(), "TextRecast", "Models");

        var root = WindowsHardwareProbe.ResolveStorageRoot(modelDirectory);

        Assert.AreEqual(
            Path.GetPathRoot(Path.GetFullPath(modelDirectory)),
            root);
    }

    private sealed class StubHardwareProbe : IHardwareProbe
    {
        public PhysicalMemorySnapshot Memory { get; init; } = new(16_000, 8_000);
        public int LogicalProcessors { get; init; } = 8;
        public Architecture Architecture { get; init; } = Architecture.X64;
        public bool SupportsAvx2 { get; init; } = true;
        public StorageSnapshot Storage { get; init; } = new(40_000, @"C:\");
        public string? RequestedStoragePath { get; private set; }

        public PhysicalMemorySnapshot ReadPhysicalMemory() => Memory;

        public int ReadLogicalProcessorCount() => LogicalProcessors;

        public Architecture ReadProcessArchitecture() => Architecture;

        public bool ReadAvx2Support() => SupportsAvx2;

        public StorageSnapshot ReadStorage(string modelStoragePath)
        {
            RequestedStoragePath = modelStoragePath;
            return Storage;
        }
    }
}
