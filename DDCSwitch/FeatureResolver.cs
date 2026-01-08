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
    /// <returns>True if parsing was successful and VCP code is in valid range (0x00-0xFF)</returns>
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
            if (byte.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vcpCode))
            {
                // VCP codes are inherently valid for byte range (0x00-0xFF)
                return true;
            }
            return false;
        }

        // Try decimal format - validate range for decimal input
        if (uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint decimalValue))
        {
            // Validate VCP code is in valid range (0x00-0xFF)
            if (decimalValue <= 255)
            {
                vcpCode = (byte)decimalValue;
                return true;
            }
        }

        return false;
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

        // Validate range (0-100%)
        return percentage <= 100;
    }

    /// <summary>
    /// Validates that a raw VCP value is within the monitor's supported range
    /// </summary>
    /// <param name="value">Raw VCP value to validate</param>
    /// <param name="maxValue">Maximum value supported by the monitor for this VCP code</param>
    /// <returns>True if the value is within the valid range (0 to maxValue)</returns>
    public static bool IsValidRawVcpValue(uint value, uint maxValue)
    {
        return value <= maxValue;
    }

    /// <summary>
    /// Validates that a percentage value is in the valid range
    /// </summary>
    /// <param name="percentage">Percentage value to validate</param>
    /// <returns>True if the percentage is between 0 and 100 inclusive</returns>
    public static bool IsValidPercentage(uint percentage)
    {
        return percentage <= 100;
    }

    /// <summary>
    /// Validates that a VCP code is in the valid range
    /// </summary>
    /// <param name="vcpCode">VCP code to validate</param>
    /// <returns>True if the VCP code is in the valid range (0x00-0xFF)</returns>
    public static bool IsValidVcpCode(byte vcpCode)
    {
        // All byte values are valid VCP codes (0x00-0xFF)
        return true;
    }

    /// <summary>
    /// Gets a descriptive error message for invalid percentage values
    /// </summary>
    /// <param name="input">The invalid input that was provided</param>
    /// <returns>Error message describing the validation failure</returns>
    public static string GetPercentageValidationError(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Percentage value cannot be empty";
        }

        var cleanInput = input.Trim();
        if (cleanInput.EndsWith("%"))
        {
            cleanInput = cleanInput.Substring(0, cleanInput.Length - 1).Trim();
        }

        if (!uint.TryParse(cleanInput, out uint value))
        {
            return $"'{input}' is not a valid percentage value. Expected format: 0-100 or 0%-100%";
        }

        if (value > 100)
        {
            return $"Percentage value {value}% is out of range. Valid range: 0-100%";
        }

        return $"'{input}' is not a valid percentage value";
    }

    /// <summary>
    /// Gets a descriptive error message for invalid VCP codes
    /// </summary>
    /// <param name="input">The invalid input that was provided</param>
    /// <returns>Error message describing the validation failure</returns>
    public static string GetVcpCodeValidationError(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "VCP code cannot be empty";
        }

        var cleanInput = input.Trim();

        if (cleanInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return $"'{input}' is not a valid VCP code. Expected hex format: 0x00-0xFF";
        }

        if (uint.TryParse(cleanInput, out uint value) && value > 255)
        {
            return $"VCP code {value} is out of range. Valid range: 0-255 (0x00-0xFF)";
        }

        return $"'{input}' is not a valid VCP code. Expected format: 0-255 or 0x00-0xFF";
    }

    /// <summary>
    /// Gets a descriptive error message for invalid raw VCP values
    /// </summary>
    /// <param name="value">The invalid value that was provided</param>
    /// <param name="maxValue">The maximum value supported by the monitor</param>
    /// <param name="featureName">The name of the VCP feature</param>
    /// <returns>Error message describing the validation failure</returns>
    public static string GetRawValueValidationError(uint value, uint maxValue, string featureName)
    {
        return $"Value {value} is out of range for {featureName}. Valid range: 0-{maxValue}";
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