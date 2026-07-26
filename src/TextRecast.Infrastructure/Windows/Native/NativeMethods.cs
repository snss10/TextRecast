using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TextRecast.Infrastructure.Windows.Native;

public static class NativeMethods
{
    private const int GwlExstyle = -20;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExAppwindow = 0x00040000;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;
    private const int InputKeyboard = 1;
    private const ushort VkControl = 0x11;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint CfUnicodeText = 13;

    public static void EnableNoActivateToolWindow(IntPtr handle)
    {
        var style = GetWindowLong(handle, GwlExstyle);
        style |= WsExNoactivate | WsExToolwindow;
        style &= ~WsExAppwindow;
        var previous = SetWindowLong(handle, GwlExstyle, style);
        LogNativeFailure(previous != 0 || Marshal.GetLastWin32Error() == 0, nameof(SetWindowLong));
    }

    public static void KeepWindowTopMostNoActivate(IntPtr handle)
    {
        var success = SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
        LogNativeFailure(success, nameof(SetWindowPos));
    }

    public static uint GetClipboardSequence()
    {
        return GetClipboardSequenceNumber();
    }

    public static IntPtr GetForegroundWindowHandle()
    {
        return GetForegroundWindow();
    }

    public static bool TrySetForegroundWindowHandle(IntPtr handle)
    {
        var success = SetForegroundWindow(handle);
        LogNativeFailure(success, nameof(SetForegroundWindow));
        return success;
    }

    public static bool IsWindowHandle(IntPtr handle)
    {
        return handle != IntPtr.Zero && IsWindow(handle);
    }

    public static uint GetWindowProcessId(IntPtr handle)
    {
        _ = GetWindowThreadProcessId(handle, out var processId);
        return processId;
    }

    public static bool TryReadClipboardUnicodeText(
        int maxCharacters,
        out string text,
        out bool exceededLimit)
    {
        text = string.Empty;
        exceededLimit = false;

        if (maxCharacters <= 0 || !OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var byteCount = GlobalSize(handle).ToUInt64();
            if (byteCount < sizeof(char))
            {
                return true;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var availableCharacters = (int)Math.Min(byteCount / sizeof(char), int.MaxValue);
                var readCharacters = Math.Min(availableCharacters, maxCharacters + 1);
                var value = Marshal.PtrToStringUni(pointer, readCharacters) ?? string.Empty;
                var nullIndex = value.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    value = value[..nullIndex];
                }

                exceededLimit = value.Length > maxCharacters ||
                                (nullIndex < 0 && availableCharacters > maxCharacters);
                text = value.Length > maxCharacters ? value[..maxCharacters] : value;
                return true;
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    public static bool SendCtrlC() => SendCtrlShortcut(VkC);

    public static bool SendCtrlV() => SendCtrlShortcut(VkV);

    private static bool SendCtrlShortcut(ushort key)
    {
        if (TrySendCtrlShortcutWithSendInput(key))
        {
            return true;
        }

        SendCtrlShortcutWithKeybdEvent(key);
        return true;
    }

    private static bool TrySendCtrlShortcutWithSendInput(ushort key)
    {
        var inputs = new Input[]
        {
            CreateKeyInput(VkControl, 0),
            CreateKeyInput(key, 0),
            CreateKeyInput(key, KeyeventfKeyup),
            CreateKeyInput(VkControl, KeyeventfKeyup)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        var success = sent == inputs.Length;
        LogNativeFailure(success, nameof(SendInput));
        return success;
    }

    private static void SendCtrlShortcutWithKeybdEvent(ushort key)
    {
        keybd_event((byte)VkControl, 0, 0, UIntPtr.Zero);
        keybd_event((byte)key, 0, 0, UIntPtr.Zero);
        keybd_event((byte)key, 0, KeyeventfKeyup, UIntPtr.Zero);
        keybd_event((byte)VkControl, 0, KeyeventfKeyup, UIntPtr.Zero);
    }

    [Conditional("DEBUG")]
    private static void LogNativeFailure(bool success, string apiName)
    {
        if (!success)
        {
            Debug.WriteLine($"{apiName} failed. Win32Error={Marshal.GetLastWin32Error()}");
        }
    }

    private static Input CreateKeyInput(ushort key, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = key,
                    Flags = flags
                }
            }
        };
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr handle, int index, int newLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr GlobalSize(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
