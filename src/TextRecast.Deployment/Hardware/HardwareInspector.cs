using System.IO;
using System.Runtime.InteropServices;

namespace TextRecast.Infrastructure.Hardware;

public sealed class HardwareInspector
{
    private readonly IHardwareProbe _probe;

    public HardwareInspector()
        : this(new WindowsHardwareProbe())
    {
    }

    internal HardwareInspector(IHardwareProbe probe)
    {
        _probe = probe;
    }

    public HardwareProfile Inspect(string modelStoragePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelStoragePath);

        try
        {
            var memory = _probe.ReadPhysicalMemory();
            var logicalProcessors = _probe.ReadLogicalProcessorCount();
            var architecture = _probe.ReadProcessArchitecture();
            var supportsAvx2 = _probe.ReadAvx2Support();
            var storage = _probe.ReadStorage(modelStoragePath);

            if (memory.TotalBytes <= 0)
            {
                throw new HardwareInspectionException(
                    "Windows did not report the computer's total physical memory.");
            }

            if (memory.AvailableBytes < 0 || memory.AvailableBytes > memory.TotalBytes)
            {
                throw new HardwareInspectionException(
                    "Windows reported an invalid available physical-memory value.");
            }

            if (logicalProcessors <= 0)
            {
                throw new HardwareInspectionException(
                    "The logical processor count is unavailable.");
            }

            if (storage.AvailableBytes < 0 || string.IsNullOrWhiteSpace(storage.RootPath))
            {
                throw new HardwareInspectionException(
                    "Available storage for the model directory is unavailable.");
            }

            return new HardwareProfile(
                memory.TotalBytes,
                memory.AvailableBytes,
                logicalProcessors,
                architecture,
                supportsAvx2,
                storage.AvailableBytes,
                storage.RootPath);
        }
        catch (HardwareInspectionException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            throw new HardwareInspectionException(
                "TextRecast could not inspect this computer's hardware capabilities.",
                exception);
        }
    }
}

internal interface IHardwareProbe
{
    PhysicalMemorySnapshot ReadPhysicalMemory();

    int ReadLogicalProcessorCount();

    Architecture ReadProcessArchitecture();

    bool ReadAvx2Support();

    StorageSnapshot ReadStorage(string modelStoragePath);
}

internal readonly record struct PhysicalMemorySnapshot(long TotalBytes, long AvailableBytes);

internal readonly record struct StorageSnapshot(long AvailableBytes, string RootPath);
