namespace DDCSwitch;
/// <summary>
/// Represents a physical monitor with DDC/CI capabilities
/// </summary>
public class Monitor : IDisposable
{
    public int Index { get; }
    public string Name { get; }
    public string DeviceName { get; }
    public bool IsPrimary { get; }
    public IntPtr Handle { get; }
    private bool _disposed;
    public Monitor(int index, string name, string deviceName, bool isPrimary, IntPtr handle)
    {
        Index = index;
        Name = name;
        DeviceName = deviceName;
        IsPrimary = isPrimary;
        Handle = handle;
    }
    public bool TryGetInputSource(out uint currentValue, out uint maxValue)
    {
        currentValue = 0;
        maxValue = 0;
        if (_disposed || Handle == IntPtr.Zero)
            return false;
        return NativeMethods.GetVCPFeatureAndVCPFeatureReply(
            Handle,
            InputSource.VCP_INPUT_SOURCE,
            out _,
            out currentValue,
            out maxValue);
    }
    public bool TrySetInputSource(uint value)
    {
        if (_disposed || Handle == IntPtr.Zero)
            return false;
        return NativeMethods.SetVCPFeature(Handle, InputSource.VCP_INPUT_SOURCE, value);
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