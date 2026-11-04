using System.Runtime.InteropServices;

namespace Deosrc.Tammi.Core.Platform.Pty;

/// <summary>
/// The glibc functions and constants used by <see cref="LinuxPtyService"/>. The constants are the values for x64
/// and Arm64 Linux.
/// </summary>
internal static unsafe partial class LinuxNative
{
    private const string LibC = "libc";

    public const int ORdWr = 0x2;
    public const int ONoCtty = 0x100;
    public const int ONonBlock = 0x800;
    public const int OCloExec = 0x80000;

    public const int FGetFl = 3;
    public const int FSetFl = 4;

    public const int EIntr = 4;
    public const int EAgain = 11;

    public const short PollIn = 0x1;
    public const short PollOut = 0x4;
    public const short PollErr = 0x8;
    public const short PollHup = 0x10;

    public const nuint TiocSWinSz = 0x5414;

    public const short PosixSpawnSetSigDef = 0x04;
    public const short PosixSpawnSetSigMask = 0x08;
    public const short PosixSpawnSetSid = 0x80;

    public const int SigHup = 1;
    public const int SigKill = 9;

    // posix_spawn_file_actions_t, posix_spawnattr_t and sigset_t are opaque. glibc's are 80, 336 and 128 bytes, so
    // this leaves plenty of room.
    public const int OpaqueStructSize = 1024;

    [StructLayout(LayoutKind.Sequential)]
    public struct WinSize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort XPixels;
        public ushort YPixels;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public int Fd;
        public short Events;
        public short REvents;
    }

    [LibraryImport(LibC, EntryPoint = "posix_openpt", SetLastError = true)]
    public static partial int PosixOpenPt(int flags);

    [LibraryImport(LibC, EntryPoint = "grantpt", SetLastError = true)]
    public static partial int GrantPt(int fd);

    [LibraryImport(LibC, EntryPoint = "unlockpt", SetLastError = true)]
    public static partial int UnlockPt(int fd);

    [LibraryImport(LibC, EntryPoint = "ptsname_r")]
    public static partial int PtsNameR(int fd, byte* buffer, nuint length);

    [LibraryImport(LibC, EntryPoint = "fcntl", SetLastError = true)]
    public static partial int Fcntl(int fd, int command, int argument);

    [LibraryImport(LibC, EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, WinSize* size);

    [LibraryImport(LibC, EntryPoint = "read", SetLastError = true)]
    public static partial nint Read(int fd, byte* buffer, nuint count);

    [LibraryImport(LibC, EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int fd, byte* buffer, nuint count);

    [LibraryImport(LibC, EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(PollFd* fds, nuint count, int timeoutMilliseconds);

    [LibraryImport(LibC, EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);

    [LibraryImport(LibC, EntryPoint = "kill", SetLastError = true)]
    public static partial int Kill(int pid, int signal);

    [LibraryImport(LibC, EntryPoint = "waitpid", SetLastError = true)]
    public static partial int WaitPid(int pid, int* status, int options);

    [LibraryImport(LibC, EntryPoint = "posix_spawn")]
    public static partial int PosixSpawn(
        int* pid,
        byte* path,
        void* fileActions,
        void* attributes,
        byte** argv,
        byte** envp);

    [LibraryImport(LibC, EntryPoint = "posix_spawn_file_actions_init")]
    public static partial int FileActionsInit(void* fileActions);

    [LibraryImport(LibC, EntryPoint = "posix_spawn_file_actions_destroy")]
    public static partial int FileActionsDestroy(void* fileActions);

    [LibraryImport(LibC, EntryPoint = "posix_spawn_file_actions_addopen")]
    public static partial int FileActionsAddOpen(void* fileActions, int fd, byte* path, int flags, uint mode);

    [LibraryImport(LibC, EntryPoint = "posix_spawn_file_actions_adddup2")]
    public static partial int FileActionsAddDup2(void* fileActions, int fd, int newFd);

    [LibraryImport(LibC, EntryPoint = "posix_spawn_file_actions_addchdir_np")]
    public static partial int FileActionsAddChdir(void* fileActions, byte* path);

    [LibraryImport(LibC, EntryPoint = "posix_spawnattr_init")]
    public static partial int SpawnAttrInit(void* attributes);

    [LibraryImport(LibC, EntryPoint = "posix_spawnattr_destroy")]
    public static partial int SpawnAttrDestroy(void* attributes);

    [LibraryImport(LibC, EntryPoint = "posix_spawnattr_setflags")]
    public static partial int SpawnAttrSetFlags(void* attributes, short flags);

    [LibraryImport(LibC, EntryPoint = "posix_spawnattr_setsigmask")]
    public static partial int SpawnAttrSetSigMask(void* attributes, void* signals);

    [LibraryImport(LibC, EntryPoint = "posix_spawnattr_setsigdefault")]
    public static partial int SpawnAttrSetSigDefault(void* attributes, void* signals);

    [LibraryImport(LibC, EntryPoint = "sigemptyset")]
    public static partial int SigEmptySet(void* signals);

    [LibraryImport(LibC, EntryPoint = "sigfillset")]
    public static partial int SigFillSet(void* signals);

    /// <summary>
    /// Throws an <see cref="IOException"/> for the last error, if a call failed.
    /// </summary>
    public static void ThrowIfFailed(int result, string operation)
    {
        if (result < 0)
        {
            ThrowError(Marshal.GetLastPInvokeError(), operation);
        }
    }

    /// <summary>
    /// Throws an <see cref="IOException"/> for a function that returns its error number (e.g. <c>posix_spawn</c>).
    /// </summary>
    public static void ThrowIfError(int error, string operation)
    {
        if (error != 0)
        {
            ThrowError(error, operation);
        }
    }

    private static void ThrowError(int error, string operation) =>
        throw new IOException($"{operation} failed: {Marshal.GetPInvokeErrorMessage(error)} (errno {error}).");
}
