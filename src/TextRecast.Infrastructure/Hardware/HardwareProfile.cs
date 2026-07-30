using System.Runtime.InteropServices;

namespace TextRecast.Infrastructure.Hardware;

public sealed record HardwareProfile(
    long TotalPhysicalMemoryBytes,
    long AvailablePhysicalMemoryBytes,
    int LogicalProcessorCount,
    Architecture ProcessArchitecture,
    bool SupportsAvx2,
    long AvailableModelStorageBytes,
    string ModelStorageRoot);

public sealed class HardwareInspectionException : Exception
{
    public HardwareInspectionException(string message)
        : base(message)
    {
    }

    public HardwareInspectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
