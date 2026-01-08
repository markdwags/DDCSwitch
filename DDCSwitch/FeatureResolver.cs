using System.Globalization;

namespace DDCSwitch;

/// <summary>
/// Resolves feature names to VCP codes and handles value conversions
/// </summary>
public static class FeatureResolver
{
    private static readonly Dictionary<string, VcpFeature> FeatureMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "brightness", VcpFeature.Brightness },
        { "contrast", VcpFeature.Contrast },
        { "input", VcpFeature.InputSource }
    };

    /// <summary>
    /// Attempts to resolve a feature name or VCP code to a VcpFeature
    /// </summary>
    /// <param name="input">Feature name (brightness, contrast, input) or VCP code (0x10, 0x12, etc.)</param>
    /// <param name="feature">The resolved VcpFeature if successful</param>
    /// <returns>True if the feature was resolved successfully</returns>
    public static bool TryResolveFeature(string input, out VcpFeature? feature)
    {
        feature = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // First try to resolve as a known feature name
        if (FeatureMap.TryGetValue(input.Trim(), out feature))
        {
            return true;
        }

        // Try to parse as a VCP code
        if (TryParseVcpCode(input, out byte vcpCode))
        {
            // Create a generic VCP feature for unknown codes
            feature = new VcpFeature(vcpCode, $"VCP_{vcpCode:X2}", VcpFeatureType.ReadWrite, false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts a percentage value (0-100) to raw VCP value based on the maximum value
    /// </summary>
    /// <param name="percentage">Percentage value (0-100)</param>
    /// <param name="maxValue">Maximum raw value supported by the monitor</param>
    /// <returns>Raw VCP value</returns>
    public static uint ConvertPercentageToRaw(uint percentage, uint maxValue)
    {
        if (percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0 and 100");
        }

        if (maxValue == 0)
        {
            return 0;
        }

        // Convert percentage to raw value with proper rounding
        return (uint)Math.Round((double)percentage * maxValue / 100.0);
    }

    /// <summary>
    /// Converts a raw VCP value to percentage based on the maximum value
    /// </summary>
    /// <param name="rawValue">Raw VCP value</param>
    /// <param name="maxValue">Maximum raw value supported by the monitor</param>
    /// <returns>Percentage value (0-100)</returns>
    public static uint ConvertRawToPercentage(uint rawValue, uint maxValue)
    {
        if (maxValue == 0)
        {
            return 0;
        }

        if (rawValue > maxValue)
        {
            rawValue = maxValue;
        }

        // Convert raw value to percentage with proper rounding
        return (uint)Math.Round((double)rawValue * 100.0 / maxValue);
    }

    /// <summary>
    /// Attempts to parse a VCP code from a string (supports hex format like 0x10 or decimal)
    /// </summary>
    /// <param name="input">Input string containing VCP code</param>
    /// <param name="vcpCode">Parsed VCP code if successful</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParseVcpCode(string input, out byte vcpCode)
    {
        vcpCode = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        // Try hex format (0x10, 0X10)
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            var hexPart = input.Substring(2);
            return byte.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vcpCode);
        }

        // Try decimal format
        return byte.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out vcpCode);
    }

    /// <summary>
    /// Attempts to parse a percentage value from a string (supports % suffix)
    /// </summary>
    /// <param name="input">Input string containing percentage value</param>
    /// <param name="percentage">Parsed percentage value if successful</param>
    /// <returns>True if parsing was successful and value is in valid range (0-100)</returns>
    public static bool TryParsePercentage(string input, out uint percentage)
    {
        percentage = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        // Remove % suffix if present
        if (input.EndsWith("%"))
        {
            input = input.Substring(0, input.Length - 1).Trim();
        }

        if (!uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out percentage))
        {
            return false;
        }

        // Validate range
        return percentage <= 100;
    }

    /// <summary>
    /// Gets all known feature names
    /// </summary>
    /// <returns>Collection of known feature names</returns>
    public static IEnumerable<string> GetKnownFeatureNames()
    {
        return FeatureMap.Keys;
    }

    /// <summary>
    /// Gets all predefined VCP features
    /// </summary>
    /// <returns>Collection of predefined VCP features</returns>
    public static IEnumerable<VcpFeature> GetPredefinedFeatures()
    {
        return FeatureMap.Values;
    }
}