namespace DDCSwitch;

/// <summary>
/// Defines the access type for a VCP feature
/// </summary>
public enum VcpFeatureType
{
    /// <summary>
    /// Feature can only be read
    /// </summary>
    ReadOnly,
    
    /// <summary>
    /// Feature can only be written
    /// </summary>
    WriteOnly,
    
    /// <summary>
    /// Feature can be both read and written
    /// </summary>
    ReadWrite
}

/// <summary>
/// Represents a VCP (Virtual Control Panel) feature with its properties
/// </summary>
public class VcpFeature
{
    public byte Code { get; }
    public string Name { get; }
    public VcpFeatureType Type { get; }
    public bool SupportsPercentage { get; }

    public VcpFeature(byte code, string name, VcpFeatureType type, bool supportsPercentage)
    {
        Code = code;
        Name = name;
        Type = type;
        SupportsPercentage = supportsPercentage;
    }

    /// <summary>
    /// Brightness control (VCP 0x10)
    /// </summary>
    public static VcpFeature Brightness => new(0x10, "brightness", VcpFeatureType.ReadWrite, true);

    /// <summary>
    /// Contrast control (VCP 0x12)
    /// </summary>
    public static VcpFeature Contrast => new(0x12, "contrast", VcpFeatureType.ReadWrite, true);

    /// <summary>
    /// Input source selection (VCP 0x60)
    /// </summary>
    public static VcpFeature InputSource => new(0x60, "input", VcpFeatureType.ReadWrite, false);

    public override string ToString()
    {
        return $"{Name} (0x{Code:X2})";
    }

    public override bool Equals(object? obj)
    {
        return obj is VcpFeature other && Code == other.Code;
    }

    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }
}

/// <summary>
/// Information about a VCP feature discovered during scanning
/// </summary>
public record VcpFeatureInfo(
    byte Code,
    string Name,
    VcpFeatureType Type,
    uint CurrentValue,
    uint MaxValue,
    bool IsSupported
);