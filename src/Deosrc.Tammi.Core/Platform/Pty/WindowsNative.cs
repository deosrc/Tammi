using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// The Win32 functions and constants used by <see cref="WindowsPtyService"/>.
/// </summary>
internal static unsafe partial class WindowsNative
{
    private const string Kernel32 = "kernel32.dll";

    public const uint ExtendedStartupInfoPresent = 0x00080000;
    public const uint CreateUnicodeEnvironment = 0x00000400;
    public const int StartFUseStdHandles = 0x00000100;
    public const nint ProcThreadAttributePseudoConsole = 0x00020016;
    public const nint ProcThreadAttributeJobList = 0x0002000D;

    [StructLayout(LayoutKind.Sequential)]
    public struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInfo
    {
        public int Cb;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public nint Reserved2;
        public nint StdInput;
        public nint StdOutput;
        public nint StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [LibraryImport(Kernel32, EntryPoint = "CreatePipe", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreatePipe(
        out SafeFileHandle readPipe,
        out SafeFileHandle writePipe,
        nint pipeAttributes,
        int size);

    [LibraryImport(Kernel32, EntryPoint = "CreatePseudoConsole")]
    public static partial int CreatePseudoConsole(
        Coord size,
        SafeFileHandle input,
        SafeFileHandle output,
        uint flags,
        out nint pseudoConsole);

    [LibraryImport(Kernel32, EntryPoint = "ResizePseudoConsole")]
    public static partial int ResizePseudoConsole(nint pseudoConsole, Coord size);

    [LibraryImport(Kernel32, EntryPoint = "ClosePseudoConsole")]
    public static partial void ClosePseudoConsole(nint pseudoConsole);

    [LibraryImport(Kernel32, EntryPoint = "InitializeProcThreadAttributeList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InitializeProcThreadAttributeList(
        nint attributeList,
        int attributeCount,
        int flags,
        ref nint size);

    [LibraryImport(Kernel32, EntryPoint = "UpdateProcThreadAttribute", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateProcThreadAttribute(
        nint attributeList,
        uint flags,
        nint attribute,
        nint value,
        nint size,
        nint previousValue,
        nint returnSize);

    [LibraryImport(Kernel32, EntryPoint = "DeleteProcThreadAttributeList")]
    public static partial void DeleteProcThreadAttributeList(nint attributeList);

    [LibraryImport(
        Kernel32,
        EntryPoint = "CreateProcessW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcess(
        string? applicationName,
        char* commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        char* environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport(Kernel32, EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport(Kernel32, EntryPoint = "TerminateProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);

    [LibraryImport(Kernel32, EntryPoint = "CreateJobObjectW", SetLastError = true)]
    public static partial JobObjectHandle CreateJobObject(nint jobAttributes, nint name);

    [LibraryImport(Kernel32, EntryPoint = "TerminateJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateJobObject(JobObjectHandle job, uint exitCode);

    [LibraryImport(Kernel32, EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);
}
