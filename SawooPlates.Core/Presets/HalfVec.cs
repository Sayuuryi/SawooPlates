namespace SawooPlates.Core.Presets;

/// <summary>Two half-precision floats, laid out like FFXIVClientStructs' HalfVector2.</summary>
public record struct HalfVec2(Half X, Half Y);

/// <summary>Four half-precision floats, laid out like FFXIVClientStructs' HalfVector4.</summary>
public record struct HalfVec4(Half X, Half Y, Half Z, Half W);
