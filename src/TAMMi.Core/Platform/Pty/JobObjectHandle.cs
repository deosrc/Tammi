using Microsoft.Win32.SafeHandles;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// A Windows Job Object, which groups a process with every process it starts so they can be ended together.
/// </summary>
internal sealed class JobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public JobObjectHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => WindowsNative.CloseHandle(handle);
}
