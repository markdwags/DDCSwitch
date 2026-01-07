namespace DDCSwitch;

/// <summary>
/// Represents a physical monitor with DDC/CI capabilities
/// </summary>
public class Monitor(int index, string name, string deviceName, bool isPrimary, IntPtr handle)
    : IDisposable
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    public string DeviceName { get; } = deviceName;
    public bool IsPrimary { get; } = isPrimary;

    private IntPtr Handle { get; } = handle;
    private bool _disposed;

    public bool TryGetInputSource(out uint currentValue, out uint maxValue)
    {
        currentValue = 0;
        maxValue = 0;

        if (_disposed || Handle == IntPtr.Zero)
        {
            return false;
        }

        return NativeMethods.GetVCPFeatureAndVCPFeatureReply(
            Handle,
            InputSource.VcpInputSource,
            out _,
            out currentValue,
            out maxValue);
    }

    public bool TrySetInputSource(uint value)
    {
        if (_disposed || Handle == IntPtr.Zero)
        {
            return false;
        }

        return NativeMethods.SetVCPFeature(Handle, InputSource.VcpInputSource, value);
    }

    public void Dispose()
    {
        if (!_disposed && Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyPhysicalMonitor(Handle);
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    ~Monitor()
    {
        Dispose();
    }

    public override string ToString()
    {
        return $"[{Index}] {Name} ({DeviceName}){(IsPrimary ? " *PRIMARY*" : "")}";
    }
}