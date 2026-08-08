using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace TextRecast.Infrastructure.Hardware;

internal sealed class WindowsHardwareProbe : IHardwareProbe
{
    public PhysicalMemorySnapshot ReadPhysicalMemory()
    {
        var status = new MemoryStatus
        {
            Length = checked((uint)Marshal.SizeOf<MemoryStatus>())
        };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PhysicalMemorySnapshot(
            checked((long)status.TotalPhysical),
            checked((long)status.AvailablePhysical));
    }

    public int ReadLogicalProcessorCount()
    {
        return Environment.ProcessorCount;
    }

    public Architecture ReadProcessArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture;
    }

    public bool ReadAvx2Support()
    {
        return Avx2.IsSupported;
    }

    public StorageSnapshot ReadStorage(string modelStoragePath)
    {
        var root = ResolveStorageRoot(modelStoragePath);
        var drive = new DriveInfo(root);
        return new StorageSnapshot(drive.AvailableFreeSpace, drive.RootDirectory.FullName);
    }

    internal static string ResolveStorageRoot(string modelStoragePath)
    {
        var fullPath = Path.GetFullPath(modelStoragePath);
        var root = Path.GetPathRoot(fullPath);
        return string.IsNullOrWhiteSpace(root)
            ? throw new IOException("The model-storage drive could not be determined.")
            : root;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
